import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, finalize, switchMap, timer } from 'rxjs';
import { ApiError, apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import {
  AnalysisLaunchResponse,
  AnalysisStatusResponse,
  PolicyUpdateRequest,
  ProjectResponse,
} from '../../core/api/api.models';
import { ProjectsStore } from '../../core/workspace/projects-store';
import { ToastService } from '../../core/workspace/toast';
import {
  LoadedSnapshot,
  loadEntireSnapshot,
  snapshotPageSize,
} from '../../core/branches/snapshot-loader';

const noResultCode = 'analysis.no_successful_result';
const pollIntervalMs = 800;

/**
 * Shared state of an observed repository: the project, its last complete capture and the
 * running analysis. Only one repository is open at a time: `open()` resets the state. Which
 * capture the views show is not decided here but in `CaptureStore`, which builds on this.
 */
@Injectable({ providedIn: 'root' })
export class ProjectContext {
  private readonly api = inject(GitHealthApiClient);
  private readonly projects = inject(ProjectsStore);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private polling?: Subscription;
  private snapshotLoad?: Subscription;
  private projectId = '';

  readonly project = signal<ProjectResponse | null>(null);
  readonly latestSnapshot = signal<LoadedSnapshot | null>(null);
  readonly analysis = signal<AnalysisStatusResponse | null>(null);
  readonly error = signal<string | null>(null);
  readonly isLoadingLatest = signal(true);
  readonly isLaunching = signal(false);
  readonly isSavingPolicy = signal(false);
  readonly isSavingBaselines = signal(false);

  /** Baseline the views are measured against; `null` is the primary one of the project. */
  readonly baseline = signal<string | null>(null);

  /** Current order of the displayed branches: the card uses it for "Next". */
  readonly visibleBranchIds = signal<readonly string[]>([]);

  readonly isRunning = computed(() => this.isLaunching() || this.analysis()?.status === 'Running');

  open(projectId: string): void {
    if (this.projectId === projectId) {
      return;
    }

    this.projectId = projectId;
    this.baseline.set(null);
    this.latestSnapshot.set(null);
    this.analysis.set(null);
    this.loadProject();
    this.loadLatestSnapshot();
  }

  /**
   * Another baseline, same repository: `open()` would recognise the project and do nothing,
   * so the snapshot is reloaded here on its own.
   */
  setBaseline(reference: string | null): void {
    if (this.baseline() === reference) {
      return;
    }

    this.baseline.set(reference);
    this.loadLatestSnapshot();
  }

  /** Forgets the open repository, including its id: reopening it must read it again. */
  reset(): void {
    this.projectId = '';
    this.project.set(null);
    this.latestSnapshot.set(null);
    this.analysis.set(null);
    this.error.set(null);
  }

  clearError(): void {
    this.error.set(null);
  }

  launchAnalysis(): void {
    if (this.isRunning()) {
      return;
    }

    this.isLaunching.set(true);
    this.error.set(null);
    this.api
      .launchAnalysis(this.projectId)
      .pipe(
        finalize(() => this.isLaunching.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (launch) => this.pollAnalysis(trackedRun(launch, this.baseline())),
        error: (error: unknown) =>
          this.fail(error, $localize`:@@project.error.launch:The analysis could not be started.`),
      });
  }

  saveBaselines(referenceNames: readonly string[], onSaved?: () => void): void {
    this.isSavingBaselines.set(true);
    this.error.set(null);
    this.api
      .updateBaselines(this.projectId, referenceNames)
      .pipe(
        finalize(() => this.isSavingBaselines.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (project) => {
          this.applyProject(project);
          this.loadLatestSnapshot();
          this.toast.show($localize`:@@project.toast.baselines:Baselines saved · no Git write`);
          onSaved?.();
        },
        error: (error: unknown) =>
          this.fail(
            error,
            $localize`:@@project.error.saveBaselines:The baselines could not be saved.`,
          ),
      });
  }

  savePolicy(policy: PolicyUpdateRequest, message: string, onSaved?: () => void): void {
    this.isSavingPolicy.set(true);
    this.error.set(null);
    this.api
      .updatePolicy(this.projectId, policy)
      .pipe(
        finalize(() => this.isSavingPolicy.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (project) => {
          this.applyProject(project);
          this.loadLatestSnapshot();
          this.toast.show(message);
          onSaved?.();
        },
        error: (error: unknown) =>
          this.fail(error, $localize`:@@project.error.savePolicy:The policy could not be saved.`),
      });
  }

  relocate(repositoryPath: string, onDone: (succeeded: boolean) => void): void {
    this.error.set(null);
    this.api
      .relocateProject(this.projectId, { repositoryPath })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (project) => {
          this.applyProject(project);
          this.toast.show(
            $localize`:@@project.toast.relocated:Path verified · baseline and last commit found`,
          );
          onDone(true);
        },
        error: (error: unknown) => {
          this.error.set(
            apiErrorMessage(
              error,
              $localize`:@@project.error.relocate:The repository could not be relocated.`,
            ),
          );
          onDone(false);
        },
      });
  }

  /** One read at a time: a baseline switch must not be overtaken by the read it replaces. */
  loadLatestSnapshot(): void {
    this.snapshotLoad?.unsubscribe();
    this.isLoadingLatest.set(true);
    this.snapshotLoad = loadEntireSnapshot((cursor) =>
      this.api.getLatestSnapshots(this.projectId, {
        baseline: this.baseline() ?? undefined,
        cursor: cursor ?? undefined,
        pageSize: snapshotPageSize,
        sort: 'activity',
        direction: 'desc',
      }),
    )
      .pipe(
        finalize(() => this.isLoadingLatest.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (snapshot) => this.latestSnapshot.set(snapshot),
        error: (error: unknown) => {
          this.latestSnapshot.set(null);
          if (!(error instanceof ApiError && error.code === noResultCode)) {
            this.fail(
              error,
              $localize`:@@project.error.snapshot:The last snapshot cannot be read.`,
            );
          }
        },
      });
  }

  private loadProject(): void {
    this.api
      .getProject(this.projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (project) => this.applyProject(project),
        error: (error: unknown) =>
          this.fail(error, $localize`:@@project.error.load:This repository cannot be loaded.`),
      });
  }

  private pollAnalysis(analysisId: string): void {
    this.polling?.unsubscribe();
    this.polling = timer(0, pollIntervalMs)
      .pipe(
        switchMap(() => this.api.getAnalysis(analysisId)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (status) => this.handleStatus(status),
        error: (error: unknown) =>
          this.fail(
            error,
            $localize`:@@project.error.tracking:Tracking of the analysis was interrupted.`,
          ),
      });
  }

  private handleStatus(status: AnalysisStatusResponse): void {
    this.analysis.set(status);
    if (status.status === 'Running') {
      return;
    }

    this.polling?.unsubscribe();
    this.loadProject();
    this.loadLatestSnapshot();
    if (status.failureMessage !== null) {
      this.error.set(status.failureMessage);
      return;
    }

    this.toast.show($localize`:@@project.toast.analysisDone:Analysis finished · no Git write`);
  }

  private applyProject(project: ProjectResponse): void {
    this.project.set(project);
    this.projects.upsert(project);
  }

  private fail(error: unknown, fallback: string): void {
    this.error.set(apiErrorMessage(error, fallback));
  }
}

/** A launch starts one run per baseline: the one worth following is the one being read. */
function trackedRun(launch: AnalysisLaunchResponse, baseline: string | null): string {
  const tracked = launch.analyses.find((item) => item.referenceName === baseline);
  return tracked?.analysisId ?? launch.analysisId;
}
