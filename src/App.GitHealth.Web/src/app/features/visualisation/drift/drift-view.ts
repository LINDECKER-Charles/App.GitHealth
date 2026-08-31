import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { DsBadge } from '../../../ui/core/ds-badge';
import { DsButton } from '../../../ui/core/ds-button';
import { DsIcon } from '../../../ui/core/ds-icon';
import { DsSpinner } from '../../../ui/core/ds-spinner';
import { DsStatusDot } from '../../../ui/core/ds-status-dot';
import { DsSelect, SelectOption } from '../../../ui/forms/ds-select';
import { DsCallout } from '../../../ui/surfaces/ds-callout';
import { DsEmptyState } from '../../../ui/surfaces/ds-empty-state';
import { ProjectContext } from '../../project/project-context';
import { DriftStore } from './drift-store';
import {
  CaptureRange,
  DriftCapture,
  DriftGroup,
  buildDrift,
  clampCaptureSelection,
  driftGridColumns,
  driftLegend,
} from './snapshot-drift';

const minimumCaptures = 2;
const compareArrowSize = 14;
const rowArrowSize = 13;
const dotSize = 7;
const flagSize = 12;
const spinnerSize = 20;
const loadingSubtitle = $localize`:@@drift.subtitle.loading:Reading the history…`;
const errorSubtitle = $localize`:@@drift.subtitle.error:The capture history could not be read.`;
const noCaptureSubtitle = $localize`:@@drift.subtitle.noCapture:No completed capture: there is nothing to compare yet.`;
const shortHistorySubtitle = $localize`:@@drift.subtitle.shortHistory:Only one capture saved: nothing to compare for now.`;
const noCaptureDescription = $localize`:@@drift.empty.noCapture:This repository has no completed capture yet. An analysis reads the references present on this machine, writes nothing, and will set the first point of comparison.`;
const shortHistoryDescription = $localize`:@@drift.empty.shortHistory:This repository has only one completed capture. A new analysis reads the references present on this machine, writes nothing, and will give the second point of comparison.`;
const collapseLabel = $localize`:@@drift.toggle.collapse:collapse`;
const expandLabel = $localize`:@@drift.toggle.expand:show`;

/** Journal of what moved between two captures, every branch read "before → after". */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsBadge, DsButton, DsCallout, DsEmptyState, DsIcon, DsSelect, DsSpinner, DsStatusDot],
  providers: [DriftStore],
  selector: 'app-drift-view',
  styleUrls: ['../visualisation-card.scss', './drift-view.scss'],
  templateUrl: './drift-view.html',
})
export class DriftView {
  protected readonly context = inject(ProjectContext);
  protected readonly store = inject(DriftStore);

  protected readonly compareArrowSize = compareArrowSize;
  protected readonly rowArrowSize = rowArrowSize;
  protected readonly dotSize = dotSize;
  protected readonly flagSize = flagSize;
  protected readonly spinnerSize = spinnerSize;

  private readonly moved = signal<'from' | 'to' | null>(null);
  private readonly picked = signal<CaptureRange | null>(null);
  /** `null` until the reader decides: the collapse then follows the automatic rule. */
  private readonly unchangedOverride = signal<boolean | null>(null);

  protected readonly range = computed<CaptureRange>(() => this.clampedRange());
  protected readonly hasComparison = computed(
    () => this.store.captures().length >= minimumCaptures,
  );
  protected readonly gridColumns = computed(() => driftGridColumns(this.store.captures().length));
  protected readonly legend = computed(() =>
    driftLegend(this.store.captures(), this.store.isTruncated()),
  );

  protected readonly drift = computed(() =>
    this.hasComparison() ? buildDrift({ captures: this.store.captures(), ...this.range() }) : null,
  );

  /** Zero captures is not "a single capture": the two cases are not said the same way. */
  private readonly hasNoCapture = computed(() => this.store.captures().length === 0);

  protected readonly emptyDescription = computed(() =>
    this.hasNoCapture() ? noCaptureDescription : shortHistoryDescription,
  );

  protected readonly subtitle = computed(() => {
    if (this.store.isLoading()) {
      return loadingSubtitle;
    }
    if (this.store.error() !== null) {
      return errorSubtitle;
    }
    return (
      this.drift()?.summary ?? (this.hasNoCapture() ? noCaptureSubtitle : shortHistorySubtitle)
    );
  });

  protected readonly fromOptions = computed(() => toOptions(this.store.captures().slice(0, -1), 0));
  protected readonly toOptions = computed(() => toOptions(this.store.captures().slice(1), 1));
  protected readonly fromValue = computed(() => String(this.range().fromIndex));
  protected readonly toValue = computed(() => String(this.range().toIndex));
  protected readonly fromShort = computed(() => this.shortAt(this.range().fromIndex));
  protected readonly toShort = computed(() => this.shortAt(this.range().toIndex));

  private readonly isOnlyUnchanged = computed(() => {
    const groups = this.drift()?.groups ?? [];
    return groups.length === 1 && groups[0].kind === 'same';
  });

  /** Collapsing "Unchanged" while it is the only populated group would leave a blank screen. */
  protected readonly isUnchangedOpen = computed(
    () => this.unchangedOverride() ?? this.isOnlyUnchanged(),
  );

  protected readonly toggleLabel = computed(() =>
    this.isUnchangedOpen() ? collapseLabel : expandLabel,
  );

  protected readonly groups = computed<readonly DriftGroup[]>(() => {
    const isOpen = this.isUnchangedOpen();
    return (this.drift()?.groups ?? []).map((group) =>
      group.isCollapsible && !isOpen ? { ...group, rows: [] } : group,
    );
  });

  constructor() {
    effect(() => {
      const projectId = this.context.project()?.id;
      if (projectId !== undefined) {
        this.store.load(projectId);
      }
    });
  }

  protected selectFrom(value: string): void {
    this.pick('from', Number(value));
  }

  protected selectTo(value: string): void {
    this.pick('to', Number(value));
  }

  protected toggleUnchanged(): void {
    this.unchangedOverride.set(!this.isUnchangedOpen());
  }

  protected launch(): void {
    this.context.launchAnalysis();
  }

  private pick(moved: 'from' | 'to', index: number): void {
    const range = this.range();
    this.moved.set(moved);
    this.picked.set({
      fromIndex: moved === 'from' ? index : range.fromIndex,
      toIndex: moved === 'to' ? index : range.toIndex,
    });
  }

  /** By default the last but one capture is compared to the last, not the first to the last. */
  private clampedRange(): CaptureRange {
    const count = this.store.captures().length;
    const picked = this.picked();
    const range = picked ?? { fromIndex: count - 2, toIndex: count - 1 };
    return clampCaptureSelection({ ...range, count, moved: this.moved() ?? 'to' });
  }

  private shortAt(index: number): string {
    return this.store.captures()[index]?.short ?? '';
  }
}

function toOptions(captures: readonly DriftCapture[], offset: number): readonly SelectOption[] {
  return captures.map((capture, index) => ({
    value: String(index + offset),
    label: capture.label,
  }));
}
