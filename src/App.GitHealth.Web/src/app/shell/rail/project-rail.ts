import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProjectResponse } from '../../core/api/api.models';
import { ProjectOrganizer } from '../../core/organization/project-organizer';
import { ProjectSection, buildProjectSections } from '../../core/organization/project-sections';
import { SectionCollapseStore } from '../../core/organization/section-collapse-store';
import { FolderScanStore } from '../../core/scan/folder-scan-store';
import { ProjectsStore } from '../../core/workspace/projects-store';
import { WorkspaceDialogs } from '../../core/workspace/workspace-dialogs';
import { ProjectContext } from '../../features/project/project-context';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsIconButton } from '../../ui/core/ds-icon-button';
import { DsStatusDot } from '../../ui/core/ds-status-dot';
import { DsInput } from '../../ui/forms/ds-input';
import { IconName, Tone } from '../../ui/icon-name';

interface RailEntry {
  readonly project: ProjectResponse;
  readonly tone: Tone;
  readonly branchCount: string;
}

interface RailSection {
  readonly key: string;
  readonly kind: ProjectSection['kind'];
  readonly title: string;
  readonly icon: IconName;
  readonly entries: readonly RailEntry[];
  readonly isCollapsed: boolean;
}

const unknownBranchCount = '—';

const sectionIcons: Record<ProjectSection['kind'], IconName> = {
  favorites: 'star-filled',
  group: 'folder-open',
  ungrouped: 'list',
};

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsButton, DsIcon, DsIconButton, DsInput, DsStatusDot, RouterLink],
  selector: 'app-project-rail',
  styleUrl: './project-rail.scss',
  templateUrl: './project-rail.html',
})
export class ProjectRail {
  private readonly context = inject(ProjectContext);
  private readonly collapse = inject(SectionCollapseStore);

  protected readonly store = inject(ProjectsStore);
  protected readonly dialogs = inject(WorkspaceDialogs);
  protected readonly organizer = inject(ProjectOrganizer);
  protected readonly scan = inject(FolderScanStore);
  protected readonly filter = signal('');

  protected readonly sections = computed<readonly RailSection[]>(() =>
    buildProjectSections(this.store.projects(), this.filter()).map((section) => ({
      key: section.key,
      kind: section.kind,
      title: section.title,
      icon: sectionIcons[section.kind],
      entries: section.projects.map((project) => this.toEntry(project)),
      isCollapsed: this.collapse.collapsedKeys().has(section.key),
    })),
  );

  /** A rail with no group and no favourite shows no heading: the flat list is enough. */
  protected readonly hasHeadings = computed(() => {
    const sections = this.sections();
    return sections.length > 1 || (sections.length === 1 && sections[0].kind !== 'ungrouped');
  });

  protected readonly isEmpty = computed(() => this.sections().length === 0);

  protected isExpanded(section: RailSection): boolean {
    return !this.hasHeadings() || !section.isCollapsed;
  }

  protected sectionToggleLabel(section: RailSection): string {
    return this.isExpanded(section)
      ? $localize`:@@rail.section.collapse:Collapse ${section.title}:title:`
      : $localize`:@@rail.section.expand:Expand ${section.title}:title:`;
  }

  protected toggleSection(key: string): void {
    this.collapse.toggle(key);
  }

  protected isSelected(project: ProjectResponse): boolean {
    return this.context.project()?.id === project.id;
  }

  protected favoriteIcon(project: ProjectResponse): IconName {
    return project.isFavorite ? 'star-filled' : 'star';
  }

  protected favoriteLabel(project: ProjectResponse): string {
    return project.isFavorite
      ? $localize`:@@rail.entry.unfavourite:Remove from favourites`
      : $localize`:@@rail.entry.favourite:Add to favourites`;
  }

  private toEntry(project: ProjectResponse): RailEntry {
    return {
      project,
      tone: project.isRepositoryAccessible ? ('success' as const) : ('danger' as const),
      branchCount: this.branchCount(project),
    };
  }

  /** The branch count is only known for the repository whose snapshot is loaded. */
  private branchCount(project: ProjectResponse): string {
    const snapshot = this.context.latestSnapshot();
    return this.context.project()?.id === project.id && snapshot !== null
      ? String(snapshot.branches.length)
      : unknownBranchCount;
  }
}
