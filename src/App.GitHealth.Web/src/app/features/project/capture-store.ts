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

const historyFailure = 'L’historique des captures ne peut pas être lu.';
const captureFailure = 'Cette capture ne peut pas être relue.';

/**
 * Quelle capture le dépôt montre, pour toutes ses vues à la fois. La plus récente réutilise
 * le snapshot déjà en mémoire — l'API la classe avec la politique et l'horloge du jour —
 * tandis qu'une capture passée est relue avec la politique et l'horloge figées à sa date.
 * C'est pourquoi les vues doivent dire laquelle des deux elles montrent.
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

  /** `null` désigne la plus récente : la vue suit alors les analyses suivantes. */
  readonly requestedId = computed<string | null>(
    () => this.queryParams()[captureQueryParam] ?? null,
  );

  readonly latest = computed<CaptureOption | null>(() => this.captures().at(-1) ?? null);
  /** Une seule capture se montre quand même : le lecteur doit savoir laquelle il regarde. */
  readonly hasCaptures = computed(() => this.captures().length > 0);

  /**
   * L'historique et le contexte apprennent la dernière analyse chacun de leur côté : le
   * premier des deux qui la connaît suffit à reconnaître un lien pointant déjà dessus.
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

  /** Paramètres d'URL menant à la capture regardée, pour les liens qui doivent la garder. */
  captureLink(): Params {
    const requested = this.requestedId();
    return requested === null ? {} : { [captureQueryParam]: requested };
  }

  /** Revenir sur la plus récente relâche la sélection : elle doit suivre la prochaine analyse. */
  select(analysisId: string): void {
    this.show(this.latest()?.analysisId === analysisId ? null : analysisId);
  }

  /** Ce qu'on veut voir après avoir relancé une analyse : le présent, pas la capture figée. */
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

  /** Une analyse de plus allonge l'historique ; un autre dépôt le remplace entièrement. */
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

  /** La plus récente est déjà en mémoire ; les autres se relisent une seule fois. */
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
