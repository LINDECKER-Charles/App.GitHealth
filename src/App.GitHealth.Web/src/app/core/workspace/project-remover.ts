import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { ProjectContext } from '../../features/project/project-context';
import { apiErrorMessage } from '../api/api-error';
import { GitHealthApiClient } from '../api/git-health-api-client';
import { ProjectsStore } from './projects-store';
import { ToastService } from './toast';

const failureMessage = $localize`:@@apiError.project.remove:The repository could not be removed.`;

/**
 * Stops observing a repository: the project and every capture it holds are erased, the Git
 * repository on disk is not touched. The open repository is released before leaving its route,
 * otherwise the shell would keep showing a project the API no longer knows.
 */
@Injectable({ providedIn: 'root' })
export class ProjectRemover {
  private readonly api = inject(GitHealthApiClient);
  private readonly context = inject(ProjectContext);
  private readonly projects = inject(ProjectsStore);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  remove(projectId: string): void {
    this.api
      .deleteProject(projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => this.forget(projectId),
        error: (error: unknown) => this.toast.show(apiErrorMessage(error, failureMessage)),
      });
  }

  private forget(projectId: string): void {
    this.projects.remove(projectId);
    this.context.reset();
    this.toast.show($localize`:@@workspace.toast.projectRemoved:Repository removed · no Git write`);
    void this.router.navigate(['/']);
  }
}
