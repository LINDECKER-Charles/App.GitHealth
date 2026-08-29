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
import { ProjectsStore } from '../../core/workspace/projects-store';
import { ThemeService } from '../../core/workspace/theme';
import { plural } from '../../core/workspace/plural';
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

/** Une seule saisie pour aller à une branche, un dépôt ou une action. Tout est filtré localement. */
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
  private readonly router = inject(Router);
  private readonly store = inject(ProjectsStore);
  private readonly theme = inject(ThemeService);
  protected readonly dialogs = inject(WorkspaceDialogs);
  private readonly searchField = viewChild.required<ElementRef<HTMLInputElement>>('search');

  protected readonly query = signal('');
  protected readonly highlighted = signal(0);

  protected readonly groups = computed<readonly PaletteGroup[]>(() =>
    [
      { title: 'Branches', items: this.branchItems() },
      { title: 'Dépôts', items: this.projectItems() },
      { title: 'Actions', items: this.actionItems() },
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
        meta: project.lastSuccessfulAnalysisId === null ? 'jamais analysé' : 'snapshot disponible',
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
              'Exporter le snapshot en CSV',
              () => {
                this.dialogs.closePalette();
                this.exporter.export(project.displayName, snapshot.branches);
              },
              plural(snapshot.branches.length, 'branche'),
            ),
          ];

    return [
      action('refresh-cw', 'Lancer une analyse', () => {
        this.dialogs.closePalette();
        this.context.launchAnalysis();
      }),
      ...exportAction,
      action('settings', 'Ouvrir les politiques', () =>
        this.go(['/projects', project.id, 'settings'], {}),
      ),
      action('clock', 'Ouvrir l’historique', () =>
        this.go(['/projects', project.id, 'history'], {}),
      ),
    ];
  }

  private globalActions(): readonly PaletteItem[] {
    return [
      action(this.theme.isDark() ? 'sun' : 'moon', 'Basculer le thème', () => {
        this.theme.toggle();
        this.dialogs.closePalette();
      }),
      action('plus', 'Ajouter un dépôt', () => {
        this.dialogs.closePalette();
        this.dialogs.openAddRepository();
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
