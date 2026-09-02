import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { AnalysisHistoryResponse } from '../../core/api/api.models';
import { pluralMessage } from '../../core/i18n/plural-message';
import { DsBadge } from '../../ui/core/ds-badge';
import { DsButton } from '../../ui/core/ds-button';
import { DsIconButton } from '../../ui/core/ds-icon-button';
import { DsStatusDot } from '../../ui/core/ds-status-dot';
import { DsTag } from '../../ui/core/ds-tag';
import { DsCallout } from '../../ui/surfaces/ds-callout';
import { DsEmptyState } from '../../ui/surfaces/ds-empty-state';
import { CaptureStore } from '../project/capture-store';
import { AnalysisRun, toRuns } from './analysis-run';

const historyPageSize = 100;
const deleteFailure = $localize`:@@history.delete.error:This analysis could not be deleted.`;

/** History of runs: each one keeps its baseline and its rules. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DsBadge,
    DsButton,
    DsCallout,
    DsEmptyState,
    DsIconButton,
    DsStatusDot,
    DsTag,
    RouterLink,
  ],
  selector: 'app-analysis-history',
  styleUrl: './analysis-history.scss',
  templateUrl: './analysis-history.html',
})
export class AnalysisHistory {
  private readonly api = inject(GitHealthApiClient);
  private readonly captures = inject(CaptureStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly params = toSignal(this.route.parent?.paramMap ?? this.route.paramMap, {
    requireSync: true,
  });
  private readonly history = signal<AnalysisHistoryResponse | null>(null);

  protected readonly isLoading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly expandedId = signal<string | null>(null);
  /** The row being confirmed: this action belongs to its card, not to a global modal. */
  protected readonly pendingDeleteId = signal<string | null>(null);
  protected readonly isDeleting = signal(false);

  protected readonly hideLabel = $localize`:@@history.run.hide:Hide`;
  protected readonly policyLabel = $localize`:@@history.run.policy:Policy`;
  protected readonly interruptedTitle = $localize`:@@history.run.interrupted:Analysis interrupted`;
  protected readonly noFailureDetail = $localize`:@@history.run.noDetail:No further detail.`;

  protected readonly projectId = computed(() => this.params().get('projectId') ?? '');
  protected readonly runs = computed<readonly AnalysisRun[]>(() =>
    toRuns(this.history()?.items ?? []),
  );

  protected readonly hiddenCount = computed(() => {
    const response = this.history();
    return response === null ? 0 : Math.max(0, response.totalCount - response.items.length);
  });

  /** What the deletion takes with it, read from the run being confirmed. */
  protected readonly deletePrompt = computed(() => {
    const runId = this.pendingDeleteId();
    const item = this.history()?.items.find((candidate) => candidate.analysisId === runId);
    return item === undefined ? '' : deletePromptMessage(item.branchCount);
  });

  constructor() {
    effect(() => this.load(this.projectId()));
  }

  protected toggle(runId: string): void {
    this.expandedId.update((current) => (current === runId ? null : runId));
  }

  protected deleteLabel(shortId: string): string {
    return $localize`:@@history.delete.action:Delete analysis ${shortId}:analysis:`;
  }

  protected askDelete(runId: string): void {
    this.pendingDeleteId.set(runId);
  }

  protected cancelDelete(): void {
    this.pendingDeleteId.set(null);
  }

  protected confirmDelete(runId: string): void {
    if (this.isDeleting()) {
      return;
    }

    this.isDeleting.set(true);
    this.api
      .deleteAnalysis(runId)
      .pipe(
        finalize(() => this.isDeleting.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: () => this.forget(runId),
        error: (error: unknown) => {
          this.pendingDeleteId.set(null);
          this.error.set(apiErrorMessage(error, deleteFailure));
        },
      });
  }

  protected load(projectId: string): void {
    if (projectId.length === 0) {
      return;
    }

    this.isLoading.set(true);
    this.error.set(null);
    this.api
      .getAnalysisHistory(projectId, historyPageSize)
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (history) => this.history.set(history),
        error: (error: unknown) =>
          this.error.set(
            apiErrorMessage(error, $localize`:@@history.error.read:The history could not be read.`),
          ),
      });
  }

  /**
   * A deleted capture must not stay in the URL: the dashboard would then read `?capture=` as
   * "nothing measured yet", which would be a lie.
   */
  private forget(runId: string): void {
    const wasSelected = this.captures.selectedId() === runId;
    this.pendingDeleteId.set(null);
    this.load(this.projectId());
    this.captures.refresh();
    if (wasSelected) {
      this.captures.followLatest();
    }
  }
}

/** Each count carries its whole sentence: word order around a number is not universal. */
function deletePromptMessage(branchCount: number): string {
  if (branchCount === 0) {
    return $localize`:@@history.delete.confirmNone:Delete this analysis? It holds no branch measurement.`;
  }

  return pluralMessage(branchCount, {
    one: $localize`:@@history.delete.confirmOne:Delete this analysis and the ${branchCount}:count: branch measurement it holds?`,
    other: $localize`:@@history.delete.confirmMany:Delete this analysis and the ${branchCount}:count: branch measurements it holds?`,
  });
}
