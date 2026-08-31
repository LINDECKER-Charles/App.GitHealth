import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  Observable,
  Subscription,
  catchError,
  concatMap,
  forkJoin,
  from,
  map,
  of,
  switchMap,
  tap,
  timer,
} from 'rxjs';
import { ApiError, apiErrorMessage } from '../api/api-error';
import { GitHealthApiClient } from '../api/git-health-api-client';
import { AnalysisStatusResponse, Uuid } from '../api/api.models';
import { ProjectsStore } from '../workspace/projects-store';
import {
  FolderScanJob,
  FolderScanJobState,
  FolderScanSummary,
  FolderScanTarget,
  isTerminalScanState,
} from './folder-scan.models';
import { defaultProjectSettings } from './project-defaults';

/** Re-read cadence for the running analyses, shared with the tests. */
export const scanPollIntervalMs = 900;

const queueFullCode = 'analysis.queue_full';
const registerFailureMessage = $localize`:@@apiError.scan.save:This repository could not be saved.`;
const launchFailureMessage = $localize`:@@apiError.scan.launch:The analysis could not be started.`;
const queueSaturatedMessage = $localize`:@@apiError.scan.queueFull:The analysis queue stayed full.`;
const missingReferenceMessage = $localize`:@@apiError.scan.noBaseline:No readable baseline in this repository.`;

/**
 * Bulk scan of a selection of repositories: each one is saved if needed, then queued.
 * The parallelism stays the host's — the queue accepts what it can, and the refused
 * repositories go back as soon as an analysis finishes. The scan continues once closed.
 */
@Injectable({ providedIn: 'root' })
export class FolderScanStore {
  private readonly api = inject(GitHealthApiClient);
  private readonly projects = inject(ProjectsStore);
  private readonly destroyRef = inject(DestroyRef);
  private polling?: Subscription;
  private isLaunching = false;

  readonly jobs = signal<readonly FolderScanJob[]>([]);

  readonly summary = computed<FolderScanSummary>(() => {
    const jobs = this.jobs();
    return {
      total: jobs.length,
      done: count(jobs, 'done'),
      failed: count(jobs, 'failed'),
      active: count(jobs, 'queued') + count(jobs, 'running'),
    };
  });

  readonly isRunning = computed(() => this.jobs().some((job) => !isTerminalScanState(job.state)));

  readonly hasJobs = computed(() => this.jobs().length > 0);

  start(targets: readonly FolderScanTarget[]): void {
    this.stopPolling();
    this.jobs.set(targets.map(toPendingJob));
    this.registerMissingProjects();
  }

  reset(): void {
    this.stopPolling();
    this.isLaunching = false;
    this.jobs.set([]);
  }

  private registerMissingProjects(): void {
    const missing = this.jobs().filter((job) => job.projectId === null);
    if (missing.length === 0) {
      this.launchNext();
      return;
    }

    from(missing)
      .pipe(
        concatMap((job) => this.register(job)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({ complete: () => this.launchNext() });
  }

  private register(job: FolderScanJob): Observable<unknown> {
    if (job.referenceName === null) {
      this.patch(job.key, { state: 'failed', message: missingReferenceMessage });
      return of(null);
    }

    this.patch(job.key, { state: 'registering' });
    return this.api
      .createProject({
        displayName: job.name,
        repositoryPath: job.key,
        settings: defaultProjectSettings(job.referenceName),
      })
      .pipe(
        tap((project) => {
          this.projects.upsert(project);
          this.patch(job.key, { projectId: project.id, state: 'pending' });
          // The repository is analysed without waiting for the whole selection to be saved.
          this.launchNext();
        }),
        catchError((error: unknown) => {
          this.patch(job.key, {
            state: 'failed',
            message: apiErrorMessage(error, registerFailureMessage),
          });
          return of(null);
        }),
      );
  }

  /**
   * Queues one ready repository after another, until refusal: the host sets the pace.
   * Only one queueing call is in flight at a time, otherwise a registration and a polling
   * round happening together would launch the same repository twice.
   */
  private launchNext(): void {
    const next = this.jobs().find((job) => job.state === 'pending');
    const projectId = next?.projectId ?? null;
    if (this.isLaunching || next === undefined || projectId === null) {
      this.refreshWhenIdle();
      return;
    }

    this.isLaunching = true;
    this.api
      .launchAnalysis(projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (launch) => {
          this.isLaunching = false;
          this.patch(next.key, { state: 'queued', analysisId: launch.analysisId });
          this.ensurePolling();
          this.launchNext();
        },
        error: (error: unknown) => {
          this.isLaunching = false;
          this.failLaunch(next, error);
        },
      });
  }

  private failLaunch(job: FolderScanJob, error: unknown): void {
    if (isQueueFull(error) && this.summary().active > 0) {
      // Queue saturated: this repository goes back as soon as a running analysis frees a slot.
      this.ensurePolling();
      return;
    }

    this.patch(job.key, {
      state: 'failed',
      message: isQueueFull(error)
        ? queueSaturatedMessage
        : apiErrorMessage(error, launchFailureMessage),
    });
    this.launchNext();
  }

  private ensurePolling(): void {
    if (this.polling !== undefined) {
      return;
    }

    this.polling = timer(scanPollIntervalMs, scanPollIntervalMs)
      .pipe(
        switchMap(() => this.readActiveStatuses()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((statuses) => this.applyStatuses(statuses));
  }

  private readActiveStatuses(): Observable<readonly AnalysisStatusResponse[]> {
    const active = this.jobs()
      .filter((job) => !isTerminalScanState(job.state))
      .map((job) => job.analysisId)
      .filter((analysisId): analysisId is Uuid => analysisId !== null);
    if (active.length === 0) {
      return of([]);
    }

    return forkJoin(
      active.map((analysisId) => this.api.getAnalysis(analysisId).pipe(catchError(() => of(null)))),
    ).pipe(map((statuses) => statuses.filter((status) => status !== null)));
  }

  private applyStatuses(statuses: readonly AnalysisStatusResponse[]): void {
    for (const status of statuses) {
      this.applyStatus(status);
    }

    this.launchNext();
  }

  private applyStatus(status: AnalysisStatusResponse): void {
    const job = this.jobs().find((candidate) => candidate.analysisId === status.analysisId);
    if (job === undefined || isTerminalScanState(job.state)) {
      return;
    }

    this.patch(job.key, {
      state: toJobState(status),
      phase: status.phase,
      message: status.failureMessage,
    });
  }

  /** Nothing left to follow: the repository list then reflects the new snapshots. */
  private refreshWhenIdle(): void {
    if (this.isRunning()) {
      return;
    }

    this.stopPolling();
    if (this.hasJobs()) {
      this.projects.load();
    }
  }

  private stopPolling(): void {
    this.polling?.unsubscribe();
    this.polling = undefined;
  }

  private patch(key: string, change: Partial<FolderScanJob>): void {
    this.jobs.update((jobs) => jobs.map((job) => (job.key === key ? { ...job, ...change } : job)));
  }
}

function toPendingJob(target: FolderScanTarget): FolderScanJob {
  return {
    key: target.canonicalPath,
    name: target.name,
    referenceName: target.referenceName,
    projectId: target.projectId,
    analysisId: null,
    state: 'pending',
    phase: null,
    message: null,
  };
}

function toJobState(status: AnalysisStatusResponse): FolderScanJobState {
  if (status.status === 'Completed') {
    return 'done';
  }

  if (status.status !== 'Running') {
    return 'failed';
  }

  return status.phase === 'Waiting' ? 'queued' : 'running';
}

function count(jobs: readonly FolderScanJob[], state: FolderScanJobState): number {
  return jobs.filter((job) => job.state === state).length;
}

function isQueueFull(error: unknown): boolean {
  return error instanceof ApiError && error.code === queueFullCode;
}
