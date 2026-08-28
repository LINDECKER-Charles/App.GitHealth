import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, switchMap, timer } from 'rxjs';
import { ApiError } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import {
  AnalysisPhase,
  AnalysisStatusResponse,
  ActivityStatus,
  BranchRelationship,
  BranchTopology,
  ProjectResponse,
  RecommendationKind,
  SnapshotPageResponse,
  SnapshotQuery,
  SnapshotSort,
  SortDirection,
} from '../../core/api/api.models';
import {
  analysisPhases,
  displayReference,
  phaseLabel,
  referenceSource,
  relativeAge,
  topologyTone,
} from './dashboard.helpers';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, FormsModule, RouterLink],
  selector: 'app-dashboard',
  styleUrls: ['./dashboard.scss', './dashboard-table.scss'],
  templateUrl: './dashboard.html',
})
export class Dashboard {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly projectId = this.route.snapshot.paramMap.get('projectId') ?? '';
  private readonly analysisId = this.route.snapshot.paramMap.get('analysisId');
  private readonly cursors: Array<string | null> = [null];
  private pageIndex = 0;
  private polling?: Subscription;

  readonly project = signal<ProjectResponse | null>(null);
  readonly page = signal<SnapshotPageResponse | null>(null);
  readonly status = signal<AnalysisStatusResponse | null>(null);
  readonly error = signal<string | null>(null);
  readonly isLoading = signal(true);
  readonly isLaunching = signal(false);
  readonly search = signal(this.route.snapshot.queryParamMap.get('search') ?? '');
  readonly relationship = signal<BranchRelationship | ''>(
    parseRelationship(this.route.snapshot.queryParamMap.get('relationship')),
  );
  readonly sort = signal<SnapshotSort>(parseSort(this.route.snapshot.queryParamMap.get('sort')));
  readonly direction = signal<SortDirection>(
    this.route.snapshot.queryParamMap.get('direction') === 'desc' ? 'desc' : 'asc',
  );
  readonly topology = signal<BranchTopology | ''>(
    parseTopology(this.route.snapshot.queryParamMap.get('topology')),
  );
  readonly activity = signal<ActivityStatus | ''>(
    parseActivity(this.route.snapshot.queryParamMap.get('activity')),
  );
  readonly recommendation = signal<RecommendationKind | ''>(
    parseRecommendation(this.route.snapshot.queryParamMap.get('recommendation')),
  );
  readonly phases = analysisPhases;
  readonly hasPrevious = signal(false);
  readonly currentPage = signal(1);
  readonly hasSnapshot = computed(() => this.page() !== null);
  readonly isHistorical = this.analysisId !== null;
  readonly csvUrl = computed(() => this.api.branchCsvUrl(this.projectId, this.filterQuery()));
  readonly isRunning = computed(() => {
    const value = this.status()?.status;
    return value === 'Running' || this.isLaunching();
  });

  readonly displayReference = displayReference;
  readonly phaseLabel = phaseLabel;
  readonly referenceSource = referenceSource;
  readonly relativeAge = relativeAge;
  readonly topologyTone = topologyTone;

  constructor() {
    this.loadProject();
    this.loadSnapshots();
  }

  launchAnalysis(): void {
    this.isLaunching.set(true);
    this.error.set(null);
    this.api.launchAnalysis(this.projectId).subscribe({
      next: (launch) => {
        this.isLaunching.set(false);
        this.startPolling(launch.analysisId);
      },
      error: (error: unknown) => {
        this.isLaunching.set(false);
        this.error.set(errorMessage(error));
      },
    });
  }

  applyFilters(): void {
    this.cursors.splice(1);
    this.pageIndex = 0;
    this.updateNavigationState();
    this.loadSnapshots();
  }

  resetFilters(): void {
    this.search.set('');
    this.relationship.set('');
    this.sort.set('name');
    this.direction.set('asc');
    this.topology.set('');
    this.activity.set('');
    this.recommendation.set('');
    this.applyFilters();
  }

  applyQuickFilter(recommendation: RecommendationKind | ''): void {
    this.recommendation.set(recommendation);
    this.applyFilters();
  }

  nextPage(): void {
    const cursor = this.page()?.nextCursor;
    if (cursor === null || cursor === undefined) {
      return;
    }

    this.pageIndex += 1;
    this.cursors[this.pageIndex] = cursor;
    this.loadSnapshots(cursor);
  }

  previousPage(): void {
    if (this.pageIndex === 0) {
      return;
    }

    this.pageIndex -= 1;
    this.loadSnapshots(this.cursors[this.pageIndex]);
  }

  isPhaseComplete(phase: AnalysisPhase): boolean {
    const current = this.status()?.phase;
    return (
      current === 'Finished' ||
      (current !== undefined && this.phases.indexOf(phase) <= this.phases.indexOf(current))
    );
  }

  private loadProject(): void {
    this.api.getProject(this.projectId).subscribe({
      next: (project) => this.project.set(project),
      error: (error: unknown) => this.error.set(errorMessage(error)),
    });
  }

  private loadSnapshots(cursor: string | null = null): void {
    this.isLoading.set(true);
    const request =
      this.analysisId === null
        ? this.api.getLatestSnapshots(this.projectId, this.pageQuery(cursor))
        : this.api.getAnalysisSnapshots(this.analysisId, this.pageQuery(cursor));
    request.subscribe({
      next: (page) => {
        this.page.set(page);
        this.finishPageLoad();
      },
      error: (error: unknown) => {
        if (!(error instanceof ApiError && error.code === 'analysis.no_successful_result')) {
          this.error.set(errorMessage(error));
        }
        this.finishPageLoad();
      },
    });
  }

  private finishPageLoad(): void {
    this.isLoading.set(false);
    this.hasPrevious.set(this.pageIndex > 0);
    this.currentPage.set(this.pageIndex + 1);
  }

  private pageQuery(cursor: string | null): SnapshotQuery {
    return {
      ...this.filterQuery(),
      cursor: cursor ?? undefined,
      pageSize: 50,
    };
  }

  private filterQuery(): SnapshotQuery {
    return {
      direction: this.direction(),
      relationship: this.relationship() || undefined,
      search: this.search() || undefined,
      sort: this.sort(),
      topology: this.topology() || undefined,
      activity: this.activity() || undefined,
      recommendation: this.recommendation() || undefined,
    };
  }

  private startPolling(analysisId: string): void {
    this.polling?.unsubscribe();
    this.polling = timer(0, 800)
      .pipe(
        switchMap(() => this.api.getAnalysis(analysisId)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (status) => this.handleStatus(status),
        error: (error: unknown) => this.error.set(errorMessage(error)),
      });
  }

  private handleStatus(status: AnalysisStatusResponse): void {
    this.status.set(status);
    if (status.status === 'Running') {
      return;
    }

    this.polling?.unsubscribe();
    this.loadProject();
    this.loadSnapshots();
    if (status.failureMessage) {
      this.error.set(status.failureMessage);
    }
  }

  private updateNavigationState(): void {
    void this.router.navigate([], {
      queryParams: {
        direction: this.direction(),
        activity: this.activity() || null,
        relationship: this.relationship() || null,
        recommendation: this.recommendation() || null,
        search: this.search() || null,
        sort: this.sort(),
        topology: this.topology() || null,
      },
      relativeTo: this.route,
      replaceUrl: true,
    });
  }
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Une erreur inattendue est survenue.';
}

function parseRelationship(value: string | null): BranchRelationship | '' {
  const allowed: readonly BranchRelationship[] = [
    'SameCommit',
    'CommonAncestor',
    'BranchIsAncestorOfReference',
    'NoCommonAncestor',
  ];
  return allowed.includes(value as BranchRelationship) ? (value as BranchRelationship) : '';
}

function parseSort(value: string | null): SnapshotSort {
  const allowed: readonly SnapshotSort[] = ['name', 'ahead', 'behind', 'activity'];
  return allowed.includes(value as SnapshotSort) ? (value as SnapshotSort) : 'name';
}

function parseTopology(value: string | null): BranchTopology | '' {
  const allowed: readonly BranchTopology[] = [
    'Synchronized',
    'Ahead',
    'Merged',
    'Diverged',
    'Unrelated',
  ];
  return allowed.includes(value as BranchTopology) ? (value as BranchTopology) : '';
}

function parseActivity(value: string | null): ActivityStatus | '' {
  const allowed: readonly ActivityStatus[] = ['Active', 'Aging', 'Inactive', 'Unknown'];
  return allowed.includes(value as ActivityStatus) ? (value as ActivityStatus) : '';
}

function parseRecommendation(value: string | null): RecommendationKind | '' {
  const allowed: readonly RecommendationKind[] = ['Keep', 'Review', 'CleanupCandidate', 'Excluded'];
  return allowed.includes(value as RecommendationKind) ? (value as RecommendationKind) : '';
}
