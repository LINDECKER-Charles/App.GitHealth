import { DestroyRef, Injectable, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { Params, Router } from '@angular/router';
import { Subscription, finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { ProjectResponse } from '../../core/api/api.models';
import {
  LoadedSnapshot,
  loadEntireSnapshot,
  snapshotPageSize,
} from '../../core/branches/snapshot-loader';
import { SelectOption } from '../../ui/forms/ds-select';
import { ProjectContext } from './project-context';
import {
  CaptureOption,
  captureHistoryPageSize,
  captureQueryParam,
  comparableAnalyses,
  toCaptureOptions,
} from './capture-history';

const historyFailure = $localize`:@@capture.error.history:The capture history cannot be read.`;
const captureFailure = $localize`:@@capture.error.replay:This capture cannot be replayed.`;

/**
 * Which capture the repository shows, for all of its views at once. The most recent one
 * reuses the snapshot already in memory — the API classifies it with today's policy and
 * clock — while a past capture is read back with the policy and the clock frozen at its
 * own date. That is why the views must say which of the two they are showing.
 */
@Injectable({ providedIn: 'root' })
export class CaptureStore {
  private readonly api = inject(GitHealthApiClient);
  private readonly context = inject(ProjectContext);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private history?: Subscription;
  private capture?: Subscription;
  private loadedFor = '';

  readonly captures = signal<readonly CaptureOption[]>([]);
  readonly error = signal<string | null>(null);

  private readonly archived = signal<LoadedSnapshot | null>(null);
  private readonly isLoadingArchived = signal(false);

  private readonly queryParams = toSignal(this.router.routerState.root.queryParams, {
    initialValue: this.router.routerState.root.snapshot.queryParams as Params,
  });

  /** `null` means the most recent one: the view then follows the analyses that come after. */
  readonly requestedId = computed<string | null>(
    () => this.queryParams()[captureQueryParam] ?? null,
  );

  readonly latest = computed<CaptureOption | null>(() => this.captures().at(-1) ?? null);
  /** A single capture is still shown: the reader has to know which one they are reading. */
  readonly hasCaptures = computed(() => this.captures().length > 0);

  /**
   * The history and the context each learn the last analysis on their own side: whichever
   * of the two knows it first is enough to recognise a link already pointing at it.
   */
  private readonly latestId = computed<string | null>(
    () => this.latest()?.analysisId ?? this.context.latestSnapshot()?.analysisId ?? null,
  );

  readonly isLatestSelected = computed(() => {
    const requested = this.requestedId();
    return requested === null || requested === this.latestId();
  });

  readonly selectedId = computed(() => this.requestedId() ?? this.latestId() ?? '');

  readonly selected = computed<CaptureOption | null>(() => {
    const id = this.selectedId();
    return this.captures().find((capture) => capture.analysisId === id) ?? null;
  });

  readonly options = computed<readonly SelectOption[]>(() =>
    this.captures()
      .slice()
      .reverse()
      .map((capture) => ({ value: capture.analysisId, label: capture.label })),
  );

  readonly snapshot = computed<LoadedSnapshot | null>(() => {
    const latest = this.context.latestSnapshot();
    if (this.isLatestSelected() || latest?.analysisId === this.requestedId()) {
      return latest;
    }

    const archived = this.archived();
    return archived?.analysisId === this.requestedId() ? archived : null;
  });

  readonly hasSnapshot = computed(() => this.snapshot() !== null);

  readonly isLoading = computed(() =>
    this.isLatestSelected() ? this.context.isLoadingLatest() : this.isLoadingArchived(),
  );

  constructor() {
    effect(() => this.follow(this.context.project()));
    effect(() => this.reveal(this.requestedId()));
  }

  /** URL parameters leading to the capture being read, for the links that must keep it. */
  captureLink(): Params {
    const requested = this.requestedId();
    return requested === null ? {} : { [captureQueryParam]: requested };
  }

  /** Returning to the most recent one releases the selection: it must follow the next run. */
  select(analysisId: string): void {
    this.show(this.latest()?.analysisId === analysisId ? null : analysisId);
  }

  /** What we want to see after starting an analysis: the present, not the frozen capture. */
  followLatest(): void {
    this.show(null);
  }

  private show(analysisId: string | null): void {
    if (analysisId === this.requestedId()) {
      return;
    }

    void this.router.navigate([], {
      queryParams: { [captureQueryParam]: analysisId },
      queryParamsHandling: 'merge',
    });
  }

  /** One more analysis extends the history; another repository replaces it entirely. */
  private follow(project: ProjectResponse | null): void {
    if (project === null) {
      return;
    }

    const key = `${project.id}:${project.lastSuccessfulAnalysisId}`;
    if (key === this.loadedFor) {
      return;
    }

    if (!this.loadedFor.startsWith(`${project.id}:`)) {
      this.captures.set([]);
      this.archived.set(null);
    }

    this.loadedFor = key;
    this.loadHistory(project.id);
  }

  /** The most recent one is already in memory; the others are read back only once. */
  private reveal(analysisId: string | null): void {
    if (analysisId === null || analysisId === this.latestId()) {
      return;
    }

    if (this.archived()?.analysisId !== analysisId) {
      this.loadArchived(analysisId);
    }
  }

  private loadHistory(projectId: string): void {
    this.history?.unsubscribe();
    this.error.set(null);
    this.history = this.api
      .getAnalysisHistory(projectId, captureHistoryPageSize)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (history) =>
          this.captures.set(toCaptureOptions(comparableAnalyses(history.items), new Date())),
        error: (error: unknown) => this.error.set(apiErrorMessage(error, historyFailure)),
      });
  }

  private loadArchived(analysisId: string): void {
    this.capture?.unsubscribe();
    this.error.set(null);
    this.isLoadingArchived.set(true);
    this.capture = loadEntireSnapshot((cursor) =>
      this.api.getAnalysisSnapshots(analysisId, {
        cursor: cursor ?? undefined,
        pageSize: snapshotPageSize,
        sort: 'activity',
        direction: 'desc',
      }),
    )
      .pipe(
        finalize(() => this.isLoadingArchived.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (snapshot) => this.archived.set(snapshot),
        error: (error: unknown) => this.error.set(apiErrorMessage(error, captureFailure)),
      });
  }
}
