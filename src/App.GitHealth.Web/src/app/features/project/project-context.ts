import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, finalize, switchMap, timer } from 'rxjs';
import { ApiError, apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import {
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
 * État partagé d'un dépôt observé : le projet, sa dernière capture complète et l'analyse
 * en cours. Un seul dépôt est ouvert à la fois : `open()` réinitialise l'état. Quelle capture
 * les vues montrent ne se décide pas ici mais dans `CaptureStore`, qui s'appuie dessus.
 */
@Injectable({ providedIn: 'root' })
export class ProjectContext {
  private readonly api = inject(GitHealthApiClient);
  private readonly projects = inject(ProjectsStore);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private polling?: Subscription;
  private projectId = '';

  readonly project = signal<ProjectResponse | null>(null);
  readonly latestSnapshot = signal<LoadedSnapshot | null>(null);
  readonly analysis = signal<AnalysisStatusResponse | null>(null);
  readonly error = signal<string | null>(null);
  readonly isLoadingLatest = signal(true);
  readonly isLaunching = signal(false);
  readonly isSavingPolicy = signal(false);

  /** Ordre courant des branches affichées : la fiche s'en sert pour « Suivante ». */
  readonly visibleBranchIds = signal<readonly string[]>([]);

  readonly isRunning = computed(() => this.isLaunching() || this.analysis()?.status === 'Running');

  open(projectId: string): void {
    if (this.projectId === projectId) {
      return;
    }

    this.projectId = projectId;
    this.latestSnapshot.set(null);
    this.analysis.set(null);
    this.loadProject();
    this.loadLatestSnapshot();
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
        next: (launch) => this.pollAnalysis(launch.analysisId),
        error: (error: unknown) => this.fail(error, 'L’analyse n’a pas pu être lancée.'),
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
        error: (error: unknown) => this.fail(error, 'La politique n’a pas pu être enregistrée.'),
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
          this.toast.show('Chemin vérifié · référence et dernier commit retrouvés');
          onDone(true);
        },
        error: (error: unknown) => {
          this.error.set(apiErrorMessage(error, 'Le dépôt n’a pas pu être relocalisé.'));
          onDone(false);
        },
      });
  }

  loadLatestSnapshot(): void {
    this.isLoadingLatest.set(true);
    loadEntireSnapshot((cursor) =>
      this.api.getLatestSnapshots(this.projectId, {
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
            this.fail(error, 'Le dernier snapshot ne peut pas être lu.');
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
        error: (error: unknown) => this.fail(error, 'Ce dépôt ne peut pas être chargé.'),
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
        error: (error: unknown) => this.fail(error, 'Le suivi de l’analyse s’est interrompu.'),
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

    this.toast.show('Analyse terminée · aucune écriture Git');
  }

  private applyProject(project: ProjectResponse): void {
    this.project.set(project);
    this.projects.upsert(project);
  }

  private fail(error: unknown, fallback: string): void {
    this.error.set(apiErrorMessage(error, fallback));
  }
}
