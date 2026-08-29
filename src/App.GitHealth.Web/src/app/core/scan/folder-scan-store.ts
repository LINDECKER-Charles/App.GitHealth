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

/** Cadence de relecture des analyses en cours, partagée avec les tests. */
export const scanPollIntervalMs = 900;

const queueFullCode = 'analysis.queue_full';
const registerFailureMessage = 'Ce dépôt n’a pas pu être enregistré.';
const launchFailureMessage = 'L’analyse n’a pas pu être lancée.';
const queueSaturatedMessage = 'La file d’analyses est restée pleine.';
const missingReferenceMessage = 'Aucune référence de comparaison lisible dans ce dépôt.';

/**
 * Scan groupé d'une sélection de dépôts : chacun est enregistré si besoin, puis mis en file.
 * Le parallélisme reste celui de l'hôte — la file accepte ce qu'elle peut, et les dépôts
 * refusés repartent dès qu'une analyse se termine. Le scan se poursuit dialogue fermé.
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
          // Le dépôt part en analyse sans attendre l'enregistrement de toute la sélection.
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
   * Met en file un dépôt prêt après l'autre, jusqu'au refus : l'hôte reste maître du rythme.
   * Une seule mise en file circule à la fois, sinon un enregistrement et un tour de suivi
   * simultanés lanceraient deux fois le même dépôt.
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
      // File saturée : ce dépôt repart dès qu'une analyse en cours libère sa place.
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

  /** Plus rien à suivre : la liste des dépôts reflète alors les nouveaux snapshots. */
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
