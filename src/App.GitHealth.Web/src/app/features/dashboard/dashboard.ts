import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { BranchSnapshotResponse, RecommendationKind } from '../../core/api/api.models';
import { deleteCommand, recommendationLabels } from '../../core/branches/branch-labels';
import { SnapshotExporter } from '../../core/branches/snapshot-export';
import { pluralMessage } from '../../core/i18n/plural-message';
import { ToastService } from '../../core/workspace/toast';
import { DsBadge } from '../../ui/core/ds-badge';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsIconButton } from '../../ui/core/ds-icon-button';
import { DsStatusDot } from '../../ui/core/ds-status-dot';
import { DsTag } from '../../ui/core/ds-tag';
import { Tone } from '../../ui/icon-name';
import { DsCheckbox } from '../../ui/forms/ds-checkbox';
import { DsInput } from '../../ui/forms/ds-input';
import { DsSelect } from '../../ui/forms/ds-select';
import { DsSwitch } from '../../ui/forms/ds-switch';
import { DsCallout } from '../../ui/surfaces/ds-callout';
import { DsEmptyState } from '../../ui/surfaces/ds-empty-state';
import { CaptureStore } from '../project/capture-store';
import { ProjectContext } from '../project/project-context';
import { DashboardChip, buildChips } from './dashboard-chips';
import { BranchRow, toRow } from './dashboard-row';
import {
  BranchFilters,
  RecommendationView,
  activityOptions,
  countByRecommendation,
  defaultFilters,
  filterBranches,
  relationshipOptions,
  sortBranches,
  sortOptions,
  topologyOptions,
} from './dashboard-filters';

interface Tile {
  readonly id: RecommendationView;
  readonly label: string;
  readonly tone: Tone;
  readonly count: number;
  readonly share: string;
}

const tileDefinitions: readonly { id: RecommendationView; label: string; tone: Tone }[] = [
  { id: 'all', label: $localize`:@@dashboard.tile.all:All`, tone: 'info' },
  { id: 'Keep', label: recommendationLabels.Keep, tone: 'success' },
  { id: 'Merged', label: recommendationLabels.Merged, tone: 'merged' },
  { id: 'Review', label: recommendationLabels.Review, tone: 'warning' },
  { id: 'CleanupCandidate', label: recommendationLabels.CleanupCandidate, tone: 'danger' },
  { id: 'Excluded', label: recommendationLabels.Excluded, tone: 'neutral' },
];

/** Diagnostic view: tiles, filters and table of the loaded snapshot's branches. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DsBadge,
    DsButton,
    DsCallout,
    DsCheckbox,
    DsEmptyState,
    DsIcon,
    DsIconButton,
    DsInput,
    DsSelect,
    DsStatusDot,
    DsSwitch,
    DsTag,
  ],
  selector: 'app-dashboard',
  styleUrls: ['./dashboard.scss', './dashboard-table.scss'],
  templateUrl: './dashboard.html',
})
export class Dashboard {
  private readonly exporter = inject(SnapshotExporter);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly captures = inject(CaptureStore);
  protected readonly context = inject(ProjectContext);
  protected readonly topologyOptions = topologyOptions;
  protected readonly activityOptions = activityOptions;
  protected readonly relationshipOptions = relationshipOptions;
  protected readonly sortOptions = sortOptions;

  private readonly queryParams = toSignal(this.route.queryParamMap, { requireSync: true });

  protected readonly openBranchId = computed(() => this.queryParams().get('branch'));
  /** A past capture is replayed as it was: its patterns can no longer be edited from here. */
  protected readonly isHistorical = computed(() => !this.captures.isLatestSelected());
  protected readonly filters = signal<BranchFilters>(defaultFilters);
  protected readonly showMoreFilters = signal(false);
  protected readonly selection = signal<ReadonlySet<string>>(new Set());

  protected readonly snapshot = this.captures.snapshot;
  protected readonly isLoading = this.captures.isLoading;
  protected readonly branches = computed(() => this.snapshot()?.branches ?? []);
  protected readonly counts = computed(() => countByRecommendation(this.branches()));

  protected readonly tiles = computed<readonly Tile[]>(() => {
    const counts = this.counts();
    const total = Math.max(counts.all, 1);
    return tileDefinitions.map((tile) => ({
      ...tile,
      count: counts[tile.id],
      share: `${Math.round((counts[tile.id] / total) * 100)}%`,
    }));
  });

  protected readonly visible = computed(() => {
    const filters = this.filters();
    const threshold = this.snapshot()?.policy.inactiveAfterDays ?? 0;
    return sortBranches(
      filterBranches(this.branches(), filters, threshold),
      filters.sort,
      filters.direction,
    );
  });

  protected readonly rows = computed<readonly BranchRow[]>(() =>
    this.visible().map((branch) => toRow(branch, this.selection().has(branch.id))),
  );

  protected readonly chips = computed<readonly DashboardChip[]>(() =>
    buildChips(this.filters(), this.snapshot()?.policy.inactiveAfterDays ?? 0),
  );

  protected readonly selectedCount = computed(() => this.selection().size);
  protected readonly areAllSelected = computed(
    () => this.rows().length > 0 && this.selectedCount() >= this.rows().length,
  );

  protected readonly selectionLabel = computed(() =>
    this.selectedCount() === 1
      ? $localize`:@@dashboard.selection.one:branch selected`
      : $localize`:@@dashboard.selection.many:branches selected`,
  );

  protected readonly tableLabel = computed(() => {
    const reference = this.snapshot()?.referenceName ?? '';
    return $localize`:@@dashboard.table.aria:Branches compared to ${reference}:reference:`;
  });

  protected readonly countLabel = computed(() => {
    const total = this.counts().all;
    const shown = this.rows().length;
    return shown === total ? branchCountMessage(total) : shownOfTotalMessage(shown, total);
  });

  constructor() {
    effect(() => this.context.visibleBranchIds.set(this.rows().map((row) => row.id)));
  }

  protected update(patch: Partial<BranchFilters>): void {
    this.filters.update((filters) => ({ ...filters, ...patch }));
    this.selection.set(new Set());
  }

  protected resetFilters(): void {
    this.filters.set(defaultFilters);
    this.selection.set(new Set());
  }

  protected toggleDirection(): void {
    this.update({ direction: this.filters().direction === 'desc' ? 'asc' : 'desc' });
  }

  protected toggleSelection(id: string): void {
    this.selection.update((current) => {
      const next = new Set(current);
      if (!next.delete(id)) {
        next.add(id);
      }

      return next;
    });
  }

  protected toggleAll(): void {
    this.selection.update((current) =>
      current.size > 0 ? new Set() : new Set(this.rows().map((row) => row.id)),
    );
  }

  protected openBranch(id: string): void {
    void this.router.navigate([], {
      queryParams: { branch: id },
      queryParamsHandling: 'merge',
      relativeTo: this.route,
    });
  }

  protected exportSelection(): void {
    const project = this.context.project();
    if (project !== null) {
      this.exporter.export(project.displayName, this.selectedBranches());
    }
  }

  protected copyCommands(): void {
    const commands = this.selectedBranches().map(deleteCommand).join('\n');
    void navigator.clipboard?.writeText(commands);
    const count = this.selectedCount();
    this.toast.show(
      $localize`:@@dashboard.toast.copied:${count}:count: git commands copied to the clipboard`,
    );
  }

  protected addSelectionToPolicy(kind: 'protected' | 'excluded'): void {
    const project = this.context.project();
    if (project === null) {
      return;
    }

    const references = this.selectedBranches().map((branch) => branch.referenceName);
    const count = references.length;
    const isProtected = kind === 'protected';
    const merged = Array.from(
      new Set([
        ...(isProtected ? project.protectedPatterns : project.excludedPatterns),
        ...references,
      ]),
    );
    this.context.savePolicy(
      {
        activeUntilDays: project.activeUntilDays,
        inactiveAfterDays: project.inactiveAfterDays,
        protectedPatterns: isProtected ? merged : project.protectedPatterns,
        excludedPatterns: isProtected ? project.excludedPatterns : merged,
      },
      patternsAddedMessage(count, kind),
    );
    this.selection.set(new Set());
  }

  private selectedBranches(): readonly BranchSnapshotResponse[] {
    const selection = this.selection();
    return this.visible().filter((branch) => selection.has(branch.id));
  }
}

function branchCountMessage(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@dashboard.count.one:${count}:count: branch`,
    other: $localize`:@@dashboard.count.many:${count}:count: branches`,
  });
}

/** The filtered count carries its own sentence: "of" is not a suffix every locale appends. */
function shownOfTotalMessage(shown: number, total: number): string {
  return pluralMessage(shown, {
    one: $localize`:@@dashboard.countOfTotal.one:${shown}:shown: branch of ${total}:total:`,
    other: $localize`:@@dashboard.countOfTotal.many:${shown}:shown: branches of ${total}:total:`,
  });
}

/** Full sentence per case: a translated message is never assembled from fragments. */
function patternsAddedMessage(count: number, kind: 'protected' | 'excluded'): string {
  if (kind === 'protected') {
    return count === 1
      ? $localize`:@@dashboard.toast.protectedOne:${count}:count: protected pattern added`
      : $localize`:@@dashboard.toast.protectedMany:${count}:count: protected patterns added`;
  }

  return count === 1
    ? $localize`:@@dashboard.toast.excludedOne:${count}:count: exclusion pattern added`
    : $localize`:@@dashboard.toast.excludedMany:${count}:count: exclusion patterns added`;
}
