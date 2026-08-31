import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subscription, finalize, forkJoin, map, of, switchMap } from 'rxjs';
import { apiErrorMessage } from '../../../core/api/api-error';
import { GitHealthApiClient } from '../../../core/api/git-health-api-client';
import {
  LoadedSnapshot,
  loadEntireSnapshot,
  snapshotPageSize,
} from '../../../core/branches/snapshot-loader';
import {
  CompletedAnalysis,
  captureHistoryPageSize,
  captureLabel,
  comparableAnalyses,
  shortCaptureDate,
} from '../../project/capture-history';
import { DriftCapture, driftCaptureLimit, hasTruncatedBranchList } from './snapshot-drift';

interface CaptureHistory {
  readonly captures: readonly DriftCapture[];
  readonly isTruncated: boolean;
}

const historyFailure = $localize`:@@drift.store.historyFailure:The capture history cannot be read.`;

/**
 * Loads the comparable captures and their branches. Both sides of the drift go through
 * `getAnalysisSnapshots`: `latest` would reclassify with today's policy and today's clock,
 * which would manufacture verdict changes that never happened.
 */
@Injectable()
export class DriftStore {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private loading?: Subscription;

  readonly captures = signal<readonly DriftCapture[]>([]);
  readonly isLoading = signal(true);
  /** The history is bounded to the recent captures; unrelated to the cut below. */
  readonly isTruncated = signal(false);
  /** At least one capture has more branches than the read cap: the drift is incomplete. */
  readonly isBranchListTruncated = computed(() => hasTruncatedBranchList(this.captures()));
  readonly error = signal<string | null>(null);

  /** The project context changes identity at every analysis: the chain in flight goes stale. */
  load(projectId: string): void {
    this.loading?.unsubscribe();
    this.isLoading.set(true);
    this.error.set(null);
    this.loading = this.api
      .getAnalysisHistory(projectId, captureHistoryPageSize)
      .pipe(
        switchMap((history) => this.loadCaptures(comparableAnalyses(history.items))),
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (history) => {
          this.captures.set(history.captures);
          this.isTruncated.set(history.isTruncated);
        },
        error: (error: unknown) => this.error.set(apiErrorMessage(error, historyFailure)),
      });
  }

  private loadCaptures(analyses: readonly CompletedAnalysis[]): Observable<CaptureHistory> {
    const kept = analyses.slice(-driftCaptureLimit);
    const isTruncated = analyses.length > kept.length;
    if (kept.length === 0) {
      return of({ captures: [], isTruncated });
    }

    return forkJoin(kept.map((analysis) => this.loadCapture(analysis))).pipe(
      map((captures) => ({ captures, isTruncated })),
    );
  }

  private loadCapture(analysis: CompletedAnalysis): Observable<DriftCapture> {
    return loadEntireSnapshot((cursor) =>
      this.api.getAnalysisSnapshots(analysis.analysisId, {
        cursor: cursor ?? undefined,
        pageSize: snapshotPageSize,
        sort: 'name',
        direction: 'asc',
      }),
    ).pipe(map((snapshot) => toCapture(analysis, snapshot)));
  }
}

/** Indexed by reference name: the snapshot identifier changes at every analysis. */
function toCapture(analysis: CompletedAnalysis, snapshot: LoadedSnapshot): DriftCapture {
  const short = shortCaptureDate(analysis.capturedAtUtc, new Date());
  return {
    analysisId: analysis.analysisId,
    short,
    label: captureLabel(short, analysis.referenceCommit),
    branches: new Map(snapshot.branches.map((branch) => [branch.referenceName, branch])),
    isBranchListTruncated: snapshot.isTruncated,
  };
}
