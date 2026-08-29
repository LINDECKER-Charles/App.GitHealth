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
import { DsBadge } from '../../ui/core/ds-badge';
import { DsButton } from '../../ui/core/ds-button';
import { DsStatusDot } from '../../ui/core/ds-status-dot';
import { DsTag } from '../../ui/core/ds-tag';
import { DsCallout } from '../../ui/surfaces/ds-callout';
import { DsEmptyState } from '../../ui/surfaces/ds-empty-state';
import { AnalysisRun, toRuns } from './analysis-run';

const historyPageSize = 100;

/** Journal des passages : chacun conserve sa référence et ses règles. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DsBadge, DsButton, DsCallout, DsEmptyState, DsStatusDot, DsTag, RouterLink],
  selector: 'app-analysis-history',
  styleUrl: './analysis-history.scss',
  templateUrl: './analysis-history.html',
})
export class AnalysisHistory {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly params = toSignal(this.route.parent?.paramMap ?? this.route.paramMap, {
    requireSync: true,
  });
  private readonly history = signal<AnalysisHistoryResponse | null>(null);

  protected readonly isLoading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly expandedId = signal<string | null>(null);

  protected readonly projectId = computed(() => this.params().get('projectId') ?? '');
  protected readonly runs = computed<readonly AnalysisRun[]>(() =>
    toRuns(this.history()?.items ?? []),
  );

  protected readonly hiddenCount = computed(() => {
    const response = this.history();
    return response === null ? 0 : Math.max(0, response.totalCount - response.items.length);
  });

  constructor() {
    effect(() => this.load(this.projectId()));
  }

  protected toggle(runId: string): void {
    this.expandedId.update((current) => (current === runId ? null : runId));
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
          this.error.set(apiErrorMessage(error, 'Le journal n’a pas pu être lu.')),
      });
  }
}
