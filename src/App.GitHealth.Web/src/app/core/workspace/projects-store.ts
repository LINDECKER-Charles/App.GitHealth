import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { apiErrorMessage } from '../api/api-error';
import { GitHealthApiClient } from '../api/git-health-api-client';
import { ProjectResponse, RuntimeInfo } from '../api/api.models';

const loadFailureMessage =
  'Impossible de charger les dépôts observés. Vérifie que GitHealth est démarré.';

/** Source unique de la liste des dépôts : le rail, la palette et les vues lisent le même état. */
@Injectable({ providedIn: 'root' })
export class ProjectsStore {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);

  readonly projects = signal<readonly ProjectResponse[]>([]);
  readonly runtime = signal<RuntimeInfo | null>(null);
  readonly isLoading = signal(true);
  readonly error = signal<string | null>(null);
  readonly isEmpty = computed(() => !this.isLoading() && this.projects().length === 0);

  load(): void {
    this.isLoading.set(true);
    this.error.set(null);
    forkJoin({ projects: this.api.listProjects(), runtime: this.api.getRuntime() })
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ projects, runtime }) => {
          this.projects.set(projects);
          this.runtime.set(runtime);
        },
        error: (error: unknown) => this.error.set(apiErrorMessage(error, loadFailureMessage)),
      });
  }

  /** Réinjecte un projet mis à jour sans relire toute la liste. */
  upsert(project: ProjectResponse): void {
    const known = this.projects().some((candidate) => candidate.id === project.id);
    this.projects.update((projects) =>
      known
        ? projects.map((candidate) => (candidate.id === project.id ? project : candidate))
        : [...projects, project],
    );
  }
}
