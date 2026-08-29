import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ProjectsStore } from '../../core/workspace/projects-store';
import { WorkspaceDialogs } from '../../core/workspace/workspace-dialogs';
import { DsButton } from '../../ui/core/ds-button';
import { DsCallout } from '../../ui/surfaces/ds-callout';
import { DsEmptyState } from '../../ui/surfaces/ds-empty-state';

/** Écran d'accueil : il ouvre le premier dépôt observé, ou invite à en ajouter un. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsButton, DsCallout, DsEmptyState],
  selector: 'app-workspace-home',
  styleUrl: './workspace-home.scss',
  templateUrl: './workspace-home.html',
})
export class WorkspaceHome {
  private readonly router = inject(Router);

  protected readonly store = inject(ProjectsStore);
  protected readonly dialogs = inject(WorkspaceDialogs);

  constructor() {
    effect(() => {
      const first = this.store.projects()[0];
      if (first !== undefined) {
        void this.router.navigate(['/projects', first.id], { replaceUrl: true });
      }
    });
  }
}
