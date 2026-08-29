import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProjectResponse } from '../../core/api/api.models';
import { ProjectsStore } from '../../core/workspace/projects-store';
import { WorkspaceDialogs } from '../../core/workspace/workspace-dialogs';
import { ProjectContext } from '../../features/project/project-context';
import { DsButton } from '../../ui/core/ds-button';
import { DsIconButton } from '../../ui/core/ds-icon-button';
import { DsStatusDot } from '../../ui/core/ds-status-dot';
import { DsInput } from '../../ui/forms/ds-input';
import { Tone } from '../../ui/icon-name';

interface RailEntry {
  readonly project: ProjectResponse;
  readonly tone: Tone;
  readonly branchCount: string;
}

const unknownBranchCount = '—';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsButton, DsIconButton, DsInput, DsStatusDot, RouterLink],
  selector: 'app-project-rail',
  styleUrl: './project-rail.scss',
  templateUrl: './project-rail.html',
})
export class ProjectRail {
  private readonly context = inject(ProjectContext);

  protected readonly store = inject(ProjectsStore);
  protected readonly dialogs = inject(WorkspaceDialogs);
  protected readonly filter = signal('');

  protected readonly entries = computed<readonly RailEntry[]>(() => {
    const needle = this.filter().trim().toLowerCase();
    return this.store
      .projects()
      .filter((project) => project.displayName.toLowerCase().includes(needle))
      .map((project) => ({
        project,
        tone: project.isRepositoryAccessible ? ('success' as const) : ('danger' as const),
        branchCount: this.branchCount(project),
      }));
  });

  protected readonly isEmpty = computed(() => this.entries().length === 0);

  protected isSelected(project: ProjectResponse): boolean {
    return this.context.project()?.id === project.id;
  }

  /** Le nombre de branches n'est connu que pour le dépôt dont le snapshot est chargé. */
  private branchCount(project: ProjectResponse): string {
    const snapshot = this.context.snapshot();
    return this.context.project()?.id === project.id && snapshot !== null
      ? String(snapshot.branches.length)
      : unknownBranchCount;
  }
}
