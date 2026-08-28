import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { AnalysisHistoryItem, AnalysisRunStatus } from '../../core/api/api.models';

interface StatusPresentation {
  readonly label: string;
  readonly mark: string;
  readonly tone: string;
}

const statusPresentations: Record<AnalysisRunStatus, StatusPresentation> = {
  Running: { label: 'En cours', mark: '…', tone: 'running' },
  Completed: { label: 'Réussie', mark: '✓', tone: 'success' },
  Failed: { label: 'Échec', mark: '×', tone: 'failed' },
  Cancelled: { label: 'Annulée', mark: '—', tone: 'cancelled' },
};

const missingProjectMessage = 'Le projet demandé est absent de l’adresse.';
const loadingErrorMessage = 'Impossible de charger l’historique des analyses.';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, RouterLink],
  selector: 'app-analysis-history',
  styleUrl: './analysis-history.scss',
  templateUrl: './analysis-history.html',
})
export class AnalysisHistory {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);

  protected readonly projectId = this.route.snapshot.paramMap.get('projectId') ?? '';
  protected readonly items = signal<readonly AnalysisHistoryItem[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.loadHistory();
  }

  protected loadHistory(): void {
    if (this.projectId.length === 0) {
      this.error.set(missingProjectMessage);
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.error.set(null);
    this.api
      .getAnalysisHistory(this.projectId)
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (history) => this.items.set(history.items),
        error: (error: unknown) => this.error.set(apiErrorMessage(error, loadingErrorMessage)),
      });
  }

  protected statusLabel(status: AnalysisRunStatus): string {
    return statusPresentations[status].label;
  }

  protected statusMark(status: AnalysisRunStatus): string {
    return statusPresentations[status].mark;
  }

  protected statusTone(status: AnalysisRunStatus): string {
    return statusPresentations[status].tone;
  }

  protected displayReference(referenceName: string): string {
    return referenceName.replace(/^refs\/heads\//, '').replace(/^refs\/remotes\//, '');
  }
}
