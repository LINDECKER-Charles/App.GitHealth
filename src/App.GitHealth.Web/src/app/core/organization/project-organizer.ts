import { DestroyRef, Injectable, computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { apiErrorMessage } from '../api/api-error';
import { GitHealthApiClient } from '../api/git-health-api-client';
import { ProjectResponse } from '../api/api.models';
import { ProjectsStore } from '../workspace/projects-store';
import { ToastService } from '../workspace/toast';
import { knownGroupNames } from './project-sections';

interface OrganizationChange {
  readonly project: ProjectResponse;
  readonly isFavorite: boolean;
  readonly groupName: string | null;
  readonly message: string;
}

const failureMessage = 'Le rangement du dépôt n’a pas pu être enregistré.';

/** Écrit le rangement d'un dépôt — favori et groupe — puis réinjecte le projet mis à jour. */
@Injectable({ providedIn: 'root' })
export class ProjectOrganizer {
  private readonly api = inject(GitHealthApiClient);
  private readonly projects = inject(ProjectsStore);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly groupNames = computed(() => knownGroupNames(this.projects.projects()));

  toggleFavorite(project: ProjectResponse): void {
    const isFavorite = !project.isFavorite;
    this.save({
      project,
      isFavorite,
      groupName: project.groupName,
      message: favoriteMessage(project, isFavorite),
    });
  }

  moveToGroup(project: ProjectResponse, groupName: string | null): void {
    this.save({
      project,
      isFavorite: project.isFavorite,
      groupName,
      message: groupMessage(project, groupName),
    });
  }

  private save(change: OrganizationChange): void {
    this.api
      .updateProjectOrganization(change.project.id, {
        isFavorite: change.isFavorite,
        groupName: change.groupName,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.projects.upsert(updated);
          this.toast.show(change.message);
        },
        error: (error: unknown) => this.toast.show(apiErrorMessage(error, failureMessage)),
      });
  }
}

function favoriteMessage(project: ProjectResponse, isFavorite: boolean): string {
  return isFavorite
    ? `${project.displayName} · épinglé dans les favoris`
    : `${project.displayName} · retiré des favoris`;
}

function groupMessage(project: ProjectResponse, groupName: string | null): string {
  return groupName === null
    ? `${project.displayName} · sorti de son groupe`
    : `${project.displayName} · rangé dans ${groupName}`;
}
