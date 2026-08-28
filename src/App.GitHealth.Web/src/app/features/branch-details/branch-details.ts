import { DatePipe, Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import {
  ActivityStatus,
  BranchTopology,
  RecommendationKind,
  SnapshotDetailResponse,
} from '../../core/api/api.models';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  selector: 'app-branch-details',
  styleUrl: './branch-details.scss',
  templateUrl: './branch-details.html',
})
export class BranchDetails {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly location = inject(Location);
  private readonly snapshotId = inject(ActivatedRoute).snapshot.paramMap.get('snapshotId') ?? '';

  protected readonly detail = signal<SnapshotDetailResponse | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly loading = signal(true);

  protected readonly activityLabel = activityLabel;
  protected readonly displayReference = displayReference;
  protected readonly recommendationLabel = recommendationLabel;
  protected readonly topologyLabel = topologyLabel;

  constructor() {
    this.load();
  }

  protected load(): void {
    if (this.snapshotId.length === 0) {
      this.loading.set(false);
      this.error.set('Aucun snapshot de branche n’a été indiqué.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.api
      .getSnapshot(this.snapshotId)
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (detail) => this.detail.set(detail),
        error: (error: unknown) =>
          this.error.set(
            apiErrorMessage(error, 'Le détail de cette branche ne peut pas être chargé.'),
          ),
      });
  }

  protected goBack(): void {
    this.location.back();
  }

  protected commitLabel(count: number): string {
    return count + ' commit' + (count > 1 ? 's' : '');
  }

  protected recommendationTone(recommendation: RecommendationKind): string {
    return recommendation === 'CleanupCandidate' || recommendation === 'Review'
      ? 'attention'
      : recommendation === 'Excluded'
        ? 'muted'
        : 'stable';
  }
}

function displayReference(referenceName: string): string {
  return referenceName.replace('refs/heads/', '').replace('refs/remotes/', '');
}

function topologyLabel(topology: BranchTopology): string {
  const labels: Record<BranchTopology, string> = {
    Ahead: 'En avance',
    Diverged: 'Divergente',
    Merged: 'Fusionnée',
    Synchronized: 'Synchronisée',
    Unrelated: 'Sans ancêtre commun',
  };
  return labels[topology];
}

function activityLabel(activity: ActivityStatus): string {
  const labels: Record<ActivityStatus, string> = {
    Active: 'Active',
    Aging: 'Vieillissante',
    Inactive: 'Inactive',
    Unknown: 'Inconnue',
  };
  return labels[activity];
}

function recommendationLabel(recommendation: RecommendationKind): string {
  const labels: Record<RecommendationKind, string> = {
    CleanupCandidate: 'Candidate au nettoyage',
    Excluded: 'Exclue',
    Keep: 'Conserver',
    Review: 'À examiner',
  };
  return labels[recommendation];
}
