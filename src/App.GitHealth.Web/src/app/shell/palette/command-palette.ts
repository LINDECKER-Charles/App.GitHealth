import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { ProjectResponse } from '../../core/api/api.models';
import { displayReference, recommendationLabels } from '../../core/branches/branch-labels';
import { SnapshotExporter } from '../../core/branches/snapshot-export';
import { pluralMessage } from '../../core/i18n/plural-message';
import { ProjectOrganizer } from '../../core/organization/project-organizer';
import { ProjectsStore } from '../../core/workspace/projects-store';
import { ThemeService } from '../../core/workspace/theme';
import { WorkspaceDialogs } from '../../core/workspace/workspace-dialogs';
import { ProjectContext } from '../../features/project/project-context';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsKbd } from '../../ui/core/ds-kbd';
import { IconName } from '../../ui/icon-name';

interface PaletteItem {
  readonly icon: IconName;
  readonly label: string;
  readonly meta: string;
  readonly mono: boolean;
  readonly run: () => void;
}

interface PaletteGroup {
  readonly title: string;
  readonly items: readonly PaletteItem[];
}

const maximumBranchResults = 6;
const maximumProjectResults = 4;

/** One field to reach a branch, a repository or an action. Everything is filtered locally. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsIcon, DsKbd],
  selector: 'app-command-palette',
  styleUrl: './command-palette.scss',
  templateUrl: './command-palette.html',
})
export class CommandPalette {
  private readonly context = inject(ProjectContext);
  private readonly exporter = inject(SnapshotExporter);
  private readonly organizer = inject(ProjectOrganizer);
  private readonly router = inject(Router);
  private readonly store = inject(ProjectsStore);
  private readonly theme = inject(ThemeService);
  protected readonly dialogs = inject(WorkspaceDialogs);
  private readonly searchField = viewChild.required<ElementRef<HTMLInputElement>>('search');

  protected readonly query = signal('');
  protected readonly highlighted = signal(0);

  protected readonly groups = computed<readonly PaletteGroup[]>(() =>
    [
      { title: $localize`:@@palette.group.branches:Branches`, items: this.branchItems() },
      {
        title: $localize`:@@palette.group.repositories:Repositories`,
        items: this.projectItems(),
      },
      { title: $localize`:@@palette.group.actions:Actions`, items: this.actionItems() },
    ].filter((group) => group.items.length > 0),
  );

  protected readonly flatItems = computed(() => this.groups().flatMap((group) => group.items));
  protected readonly isEmpty = computed(() => this.flatItems().length === 0);

  constructor() {
    afterNextRender(() => this.searchField().nativeElement.focus());
  }

  protected onQuery(value: string): void {
    this.query.set(value);
    this.highlighted.set(0);
  }

  protected move(offset: number): void {
    const count = this.flatItems().length;
    if (count > 0) {
      this.highlighted.update((index) => (index + offset + count) % count);
    }
  }

  protected runHighlighted(): void {
    this.flatItems()[this.highlighted()]?.run();
  }

  protected indexOf(item: PaletteItem): number {
    return this.flatItems().indexOf(item);
  }

  private needle(): string {
    return this.query().trim().toLowerCase();
  }

  private branchItems(): readonly PaletteItem[] {
    const snapshot = this.context.snapshot();
    const project = this.context.project();
    if (snapshot === null || project === null) {
      return [];
    }

    return snapshot.branches
      .filter((branch) => matches(displayReference(branch.referenceName), this.needle()))
      .slice(0, maximumBranchResults)
      .map((branch) => ({
        icon: 'git-branch' as const,
        label: displayReference(branch.referenceName),
        meta: recommendationLabels[branch.recommendation],
        mono: true,
        run: () => this.go(['/projects', project.id], { branch: branch.id }),
      }));
  }

  private projectItems(): readonly PaletteItem[] {
    return this.store
      .projects()
      .filter((project) => matches(project.displayName, this.needle()))
      .slice(0, maximumProjectResults)
      .map((project) => ({
        icon: 'folder' as const,
        label: project.displayName,
        meta:
          project.lastSuccessfulAnalysisId === null
            ? $localize`:@@palette.project.neverAnalysed:never analysed`
            : $localize`:@@palette.project.hasSnapshot:snapshot available`,
        mono: false,
        run: () => this.go(['/projects', project.id], {}),
      }));
  }

  private actionItems(): readonly PaletteItem[] {
    const project = this.context.project();
    const actions = project === null ? [] : this.projectActions(project);
    return [...actions, ...this.globalActions()].filter((item) =>
      matches(item.label, this.needle()),
    );
  }

  private projectActions(project: ProjectResponse): readonly PaletteItem[] {
    const snapshot = this.context.snapshot();
    const exportAction =
      snapshot === null
        ? []
        : [
            action(
              'download',
              $localize`:@@palette.action.exportCsv:Export the snapshot as CSV`,
              () => {
                this.dialogs.closePalette();
                this.exporter.export(project.displayName, snapshot.branches);
              },
              branchCountMeta(snapshot.branches.length),
            ),
          ];

    return [
      action('refresh-cw', $localize`:@@palette.action.analyse:Run an analysis`, () => {
        this.dialogs.closePalette();
        this.context.launchAnalysis();
      }),
      ...exportAction,
      ...this.organizationActions(project),
      action('settings', $localize`:@@palette.action.policies:Open the policies`, () =>
        this.go(['/projects', project.id, 'settings'], {}),
      ),
      action('clock', $localize`:@@palette.action.history:Open the history`, () =>
        this.go(['/projects', project.id, 'history'], {}),
      ),
    ];
  }

  /** The grouping is always read from the stored version: the rail may have just written it. */
  private organizationActions(project: ProjectResponse): readonly PaletteItem[] {
    const current =
      this.store.projects().find((candidate) => candidate.id === project.id) ?? project;
    return [
      action(
        current.isFavorite ? 'star-filled' : 'star',
        current.isFavorite
          ? $localize`:@@palette.action.unfavourite:Remove from favourites`
          : $localize`:@@palette.action.favourite:Add to favourites`,
        () => {
          this.dialogs.closePalette();
          this.organizer.toggleFavorite(current);
        },
      ),
      action(
        'folder-open',
        $localize`:@@palette.action.moveToGroup:Move to a group`,
        () => this.dialogs.openProjectGroup(current.id),
        current.groupName ?? $localize`:@@palette.action.ungrouped:ungrouped`,
      ),
    ];
  }

  private globalActions(): readonly PaletteItem[] {
    return [
      action(
        this.theme.isDark() ? 'sun' : 'moon',
        $localize`:@@palette.action.toggleTheme:Toggle the theme`,
        () => {
          this.theme.toggle();
          this.dialogs.closePalette();
        },
      ),
      action('plus', $localize`:@@palette.action.addRepository:Add a repository`, () => {
        this.dialogs.closePalette();
        this.dialogs.openAddRepository();
      }),
      action('folder', $localize`:@@palette.action.scanFolder:Scan a folder`, () => {
        this.dialogs.closePalette();
        this.dialogs.openScanFolder();
      }),
    ];
  }

  private go(commands: readonly string[], queryParams: Record<string, string>): void {
    this.dialogs.closePalette();
    void this.router.navigate(commands, { queryParams });
  }
}

function action(icon: IconName, label: string, run: () => void, meta = ''): PaletteItem {
  return { icon, label, meta, mono: false, run };
}

function matches(candidate: string, needle: string): boolean {
  return candidate.toLowerCase().includes(needle);
}

function branchCountMeta(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@palette.branches.one:${count}:count: branch`,
    other: $localize`:@@palette.branches.many:${count}:count: branches`,
  });
}
