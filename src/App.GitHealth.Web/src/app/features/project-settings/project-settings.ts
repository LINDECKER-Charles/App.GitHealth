import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { PolicySnapshot } from '../../core/api/api.models';
import { mergedActiveUntilDays, mergedInactiveAfterDays } from '../../core/branches/branch-policy';
import { WorkspaceDialogs } from '../../core/workspace/workspace-dialogs';
import { DsBadge } from '../../ui/core/ds-badge';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsIconButton } from '../../ui/core/ds-icon-button';
import { DsStatusDot } from '../../ui/core/ds-status-dot';
import { DsTag } from '../../ui/core/ds-tag';
import { DsInput } from '../../ui/forms/ds-input';
import { DsCallout } from '../../ui/surfaces/ds-callout';
import { DsPanel } from '../../ui/surfaces/ds-panel';
import { ProjectContext } from '../project/project-context';
import {
  addBaselines,
  baselineMoveUpLabel,
  baselineRemoveLabel,
  canAddBaseline,
  isBaselineListDirty,
  maximumBaselineCount,
  moveBaseline,
  removeBaseline,
} from './baseline-draft';
import { AssistantPolicy } from './assistant-policy/assistant-policy';
import { BranchPicker } from './branch-picker/branch-picker';
import { BranchPatternKind } from './branch-picker/branch-picker-options';
import { PolicyMatch, PolicyStat, projectMatches, projectStats } from './policy-projection';

const minimumBandDays = 120;
const bandHeadroom = 1.6;

/** Settings view: thresholds, patterns, baselines, relocation and deletion. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AssistantPolicy,
    BranchPicker,
    DsBadge,
    DsButton,
    DsCallout,
    DsIcon,
    DsIconButton,
    DsInput,
    DsPanel,
    DsStatusDot,
    DsTag,
  ],
  selector: 'app-project-settings',
  styleUrl: './project-settings.scss',
  templateUrl: './project-settings.html',
})
export class ProjectSettings {
  private readonly dialogs = inject(WorkspaceDialogs);

  protected readonly context = inject(ProjectContext);

  /** The assistant section is scoped to one repository, so it is handed the one on screen. */
  protected readonly projectId = computed(() => this.context.project()?.id ?? '');

  protected readonly mergedActiveUntilDays = mergedActiveUntilDays;
  protected readonly mergedInactiveAfterDays = mergedInactiveAfterDays;
  protected readonly maximumBaselineCount = maximumBaselineCount;
  protected readonly baselineRemoveLabel = baselineRemoveLabel;
  protected readonly baselineMoveUpLabel = baselineMoveUpLabel;

  protected readonly activeUntilDays = signal('30');
  protected readonly inactiveAfterDays = signal('90');
  protected readonly protectedPatterns = signal<readonly string[]>([]);
  protected readonly excludedPatterns = signal<readonly string[]>([]);
  protected readonly baselines = signal<readonly string[]>([]);
  protected readonly newProtected = signal('');
  protected readonly newExcluded = signal('');
  protected readonly relocationPath = signal('');
  protected readonly isRelocating = signal(false);
  protected readonly pickerKind = signal<BranchPatternKind | null>(null);

  protected readonly draft = computed<PolicySnapshot>(() => ({
    activeUntilDays: toDays(this.activeUntilDays(), 0),
    inactiveAfterDays: toDays(this.inactiveAfterDays(), 1),
    protectedPatterns: this.protectedPatterns(),
    excludedPatterns: this.excludedPatterns(),
  }));

  protected readonly hasThresholdError = computed(
    () => this.draft().inactiveAfterDays <= this.draft().activeUntilDays,
  );

  protected readonly isDirty = computed(() => {
    const project = this.context.project();
    if (project === null) {
      return false;
    }

    const draft = this.draft();
    return (
      draft.activeUntilDays !== project.activeUntilDays ||
      draft.inactiveAfterDays !== project.inactiveAfterDays ||
      !sameSet(draft.protectedPatterns, project.protectedPatterns) ||
      !sameSet(draft.excludedPatterns, project.excludedPatterns)
    );
  });

  protected readonly dirtyLabel = computed(() =>
    this.isDirty()
      ? $localize`:@@settings.dirty.pending:Unsaved changes — the preview is already up to date.`
      : $localize`:@@settings.dirty.clean:Policy up to date.`,
  );

  protected readonly canAddBaseline = computed(() => canAddBaseline(this.baselines()));
  /** A project needs at least one reference to compare against. */
  protected readonly canRemoveBaseline = computed(() => this.baselines().length > 1);

  protected readonly isBaselineDirty = computed(() =>
    isBaselineListDirty(this.baselines(), this.context.project()?.referenceNames ?? []),
  );

  protected readonly baselineDirtyLabel = computed(() =>
    this.isBaselineDirty()
      ? $localize`:@@settings.baselines.dirty:Unsaved baselines — save them to measure them.`
      : $localize`:@@settings.baselines.clean:Baselines up to date.`,
  );

  protected readonly bands = computed(() => {
    const draft = this.draft();
    const span = Math.max(draft.inactiveAfterDays * bandHeadroom, minimumBandDays);
    return {
      active: `${Math.round((draft.activeUntilDays / span) * 100)}%`,
      aging: `${Math.round(((draft.inactiveAfterDays - draft.activeUntilDays) / span) * 100)}%`,
    };
  });

  protected readonly branches = computed(() => this.context.latestSnapshot()?.branches ?? []);
  protected readonly stats = computed<readonly PolicyStat[]>(() =>
    projectStats(this.branches(), this.draft()),
  );
  protected readonly matches = computed<readonly PolicyMatch[]>(() =>
    projectMatches(this.branches(), this.draft()),
  );

  protected readonly repositoryPath = computed(() => this.context.project()?.repositoryPath ?? '');

  /** The repository may be unreachable: the last capture is still a list of references. */
  protected readonly capturedReferences = computed<readonly string[]>(() =>
    this.branches().map((branch) => branch.referenceName),
  );

  /** What the picker must already consider taken, whichever list it is feeding. */
  protected readonly pickerPatterns = computed<readonly string[]>(() => {
    const kind = this.pickerKind();
    if (kind === 'baseline') {
      return this.baselines();
    }

    return kind === 'excluded' ? this.excludedPatterns() : this.protectedPatterns();
  });

  constructor() {
    effect(() => this.reset());
  }

  protected reset(): void {
    const project = this.context.project();
    if (project === null) {
      return;
    }

    this.activeUntilDays.set(String(project.activeUntilDays));
    this.inactiveAfterDays.set(String(project.inactiveAfterDays));
    this.protectedPatterns.set(project.protectedPatterns);
    this.excludedPatterns.set(project.excludedPatterns);
    this.baselines.set(project.referenceNames);
  }

  protected addProtected(): void {
    this.protectedPatterns.update((patterns) => append(patterns, this.newProtected()));
    this.newProtected.set('');
  }

  protected addExcluded(): void {
    this.excludedPatterns.update((patterns) => append(patterns, this.newExcluded()));
    this.newExcluded.set('');
  }

  protected openPicker(kind: BranchPatternKind): void {
    this.pickerKind.set(kind);
  }

  protected closePicker(): void {
    this.pickerKind.set(null);
  }

  /** A picked branch becomes an exact pattern: globs stay reserved for manual entry. */
  protected addPicked(references: readonly string[]): void {
    const kind = this.pickerKind();
    this.closePicker();
    if (kind === null) {
      return;
    }

    if (kind === 'baseline') {
      this.baselines.update((current) => addBaselines(current, references));
      return;
    }

    const target = kind === 'excluded' ? this.excludedPatterns : this.protectedPatterns;
    target.update((patterns) => references.reduce(append, patterns));
  }

  protected dropBaseline(reference: string): void {
    this.baselines.update((current) => removeBaseline(current, reference));
  }

  /** One step towards the primary position: repeated, it reaches any order. */
  protected promoteBaseline(reference: string): void {
    this.baselines.update((current) => moveBaseline(current, reference, -1));
  }

  protected saveBaselines(): void {
    if (this.isBaselineDirty()) {
      this.context.saveBaselines(this.baselines());
    }
  }

  protected openProjectDelete(): void {
    const project = this.context.project();
    if (project !== null) {
      this.dialogs.openProjectDelete(project.id);
    }
  }

  protected removeProtected(pattern: string): void {
    this.protectedPatterns.update((patterns) => patterns.filter((value) => value !== pattern));
  }

  protected removeExcluded(pattern: string): void {
    this.excludedPatterns.update((patterns) => patterns.filter((value) => value !== pattern));
  }

  protected save(): void {
    if (this.hasThresholdError()) {
      return;
    }

    this.context.savePolicy(
      this.draft(),
      $localize`:@@settings.toast.saved:Policy saved · the SHAs and the counters are unchanged`,
    );
  }

  protected relocate(): void {
    const path = this.relocationPath().trim();
    if (path.length === 0 || this.isRelocating()) {
      return;
    }

    this.isRelocating.set(true);
    this.context.relocate(path, (succeeded) => {
      this.isRelocating.set(false);
      if (succeeded) {
        this.relocationPath.set('');
      }
    });
  }
}

function append(patterns: readonly string[], candidate: string): readonly string[] {
  const pattern = candidate.trim();
  return pattern.length === 0 || patterns.includes(pattern) ? patterns : [...patterns, pattern];
}

function sameSet(left: readonly string[], right: readonly string[]): boolean {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

function toDays(value: string, minimum: number): number {
  const parsed = Number.parseInt(value, 10);
  return Number.isNaN(parsed) ? minimum : Math.max(minimum, parsed);
}
