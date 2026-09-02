import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { apiErrorMessage } from '../api/api-error';
import { GitHealthApiClient } from '../api/git-health-api-client';
import { ProjectResponse, RuntimeInfo } from '../api/api.models';

const loadFailureMessage = $localize`:@@apiError.projects.load:The observed repositories could not be loaded. Check that GitHealth is running.`;

/** Single source for the repository list: rail, palette and views read the same state. */
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

  /** Feeds an updated project back without re-reading the whole list. */
  upsert(project: ProjectResponse): void {
    const known = this.projects().some((candidate) => candidate.id === project.id);
    this.projects.update((projects) =>
      known
        ? projects.map((candidate) => (candidate.id === project.id ? project : candidate))
        : [...projects, project],
    );
  }

  /** Mirror of `upsert`: one call updates the rail, the palette and the group dialog. */
  remove(projectId: string): void {
    this.projects.update((projects) => projects.filter((project) => project.id !== projectId));
  }
}
