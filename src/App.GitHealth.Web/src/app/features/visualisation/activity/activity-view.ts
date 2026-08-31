import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PolicySnapshot } from '../../../core/api/api.models';
import {
  mergedActiveUntilDays,
  mergedInactiveAfterDays,
} from '../../../core/branches/branch-policy';
import { pluralMessage } from '../../../core/i18n/plural-message';
import { DsBadge } from '../../../ui/core/ds-badge';
import { DsButton } from '../../../ui/core/ds-button';
import { DsIcon } from '../../../ui/core/ds-icon';
import { DsEmptyState } from '../../../ui/surfaces/ds-empty-state';
import { Tone } from '../../../ui/icon-name';
import { ProjectContext } from '../../project/project-context';
import { CaptureStore } from '../../project/capture-store';
import {
  ActivityCounts,
  ActivityRow,
  MovedThreshold,
  ThresholdDraft,
  activityCounts,
  buildActivityRows,
  buildAxisTicks,
  buildPolicyBands,
  clampThresholds,
  seedDraft,
  thresholdBounds,
  timelineDaysFor,
} from './activity-register';

const flagIconSize = 12;

/** Fallback policy: it is only read for as long as the project takes to come back from the API. */
const defaultPolicy: PolicySnapshot = {
  activeUntilDays: 30,
  inactiveAfterDays: 90,
  protectedPatterns: [],
  excludedPatterns: [],
};

interface CountBadge {
  readonly tone: Tone;
  readonly label: string;
}

/**
 * Activity register: the two sliders are a local sandbox — they replay the server rule on the
 * current snapshot without ever writing the policy.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsBadge, DsButton, DsEmptyState, DsIcon, RouterLink],
  selector: 'app-activity-view',
  styleUrls: ['../visualisation-card.scss', './activity-view.scss'],
  templateUrl: './activity-view.html',
})
export class ActivityView {
  private readonly context = inject(ProjectContext);
  protected readonly captures = inject(CaptureStore);

  protected readonly flagIconSize = flagIconSize;
  protected readonly mergedActiveUntilDays = mergedActiveUntilDays;
  protected readonly mergedInactiveAfterDays = mergedInactiveAfterDays;

  /**
   * Compared by value: every reread of the project rebuilds the object, and without this the
   * slider draft would be reset after the slightest analysis.
   */
  private readonly savedPolicy = computed<PolicySnapshot>(
    () => this.captures.snapshot()?.policy ?? defaultPolicy,
    { equal: isSamePolicy },
  );

  private readonly timelineDays = computed(() =>
    timelineDaysFor(this.savedPolicy().inactiveAfterDays),
  );

  protected readonly bounds = computed(() =>
    thresholdBounds({ saved: this.savedPolicy(), timelineDays: this.timelineDays() }),
  );

  private readonly draft = linkedSignal<ThresholdDraft>(() =>
    seedDraft(this.savedPolicy(), this.bounds()),
  );

  private readonly draftPolicy = computed<PolicySnapshot>(() => ({
    ...this.savedPolicy(),
    ...this.draft(),
  }));

  protected readonly projectId = computed(() => this.context.project()?.id ?? '');
  protected readonly axisTicks = computed(() => buildAxisTicks(this.timelineDays()));
  protected readonly bands = computed(() =>
    buildPolicyBands(this.draftPolicy(), this.timelineDays()),
  );

  protected readonly rows = computed<readonly ActivityRow[]>(() =>
    buildActivityRows({
      branches: this.captures.snapshot()?.branches ?? [],
      policy: this.draftPolicy(),
      timelineDays: this.timelineDays(),
    }),
  );

  protected readonly hasSnapshot = computed(() => this.captures.snapshot() !== null);
  protected readonly hasRows = computed(() => this.rows().length > 0);
  protected readonly countBadges = computed(() => toCountBadges(activityCounts(this.rows())));
  protected readonly activeValue = computed(() => String(this.draft().activeUntilDays));
  protected readonly inactiveValue = computed(() => String(this.draft().inactiveAfterDays));
  protected readonly activeValueText = computed(() => dayValueText(this.draft().activeUntilDays));
  protected readonly inactiveValueText = computed(() =>
    dayValueText(this.draft().inactiveAfterDays),
  );

  protected readonly isDirty = computed(() => {
    const saved = this.savedPolicy();
    const draft = this.draft();
    return (
      draft.activeUntilDays !== saved.activeUntilDays ||
      draft.inactiveAfterDays !== saved.inactiveAfterDays
    );
  });

  protected readonly savedLabel = computed(() => {
    const { activeUntilDays, inactiveAfterDays } = this.savedPolicy();
    return $localize`:@@activity.view.savedPolicy:active ≤ ${activeUntilDays}:active: d · inactive > ${inactiveAfterDays}:inactive: d`;
  });

  protected changeActive(event: Event): void {
    this.moveThreshold(event, 'active');
  }

  protected changeInactive(event: Event): void {
    this.moveThreshold(event, 'inactive');
  }

  protected reset(): void {
    this.draft.set(seedDraft(this.savedPolicy(), this.bounds()));
  }

  /**
   * Clamping being invisible to the native slider, the value kept is written back to it:
   * without that the thumb would carry on past the minimum gap.
   */
  private moveThreshold(event: Event, moved: MovedThreshold): void {
    const input = event.target as HTMLInputElement;
    const wanted = withThreshold(this.draft(), Number(input.value), moved);
    const clamped = clampThresholds(wanted, moved, this.bounds());
    input.value = String(moved === 'active' ? clamped.activeUntilDays : clamped.inactiveAfterDays);
    this.draft.set(clamped);
  }
}

/** Two policies identical field by field: object identity says nothing about the content. */
function isSamePolicy(left: PolicySnapshot, right: PolicySnapshot): boolean {
  return (
    left.activeUntilDays === right.activeUntilDays &&
    left.inactiveAfterDays === right.inactiveAfterDays &&
    isSamePatterns(left.protectedPatterns, right.protectedPatterns) &&
    isSamePatterns(left.excludedPatterns, right.excludedPatterns)
  );
}

function isSamePatterns(left: readonly string[], right: readonly string[]): boolean {
  return left.length === right.length && left.every((pattern, index) => pattern === right[index]);
}

function withThreshold(draft: ThresholdDraft, days: number, moved: MovedThreshold): ThresholdDraft {
  return moved === 'active'
    ? { activeUntilDays: days, inactiveAfterDays: draft.inactiveAfterDays }
    : { activeUntilDays: draft.activeUntilDays, inactiveAfterDays: days };
}

/** Spoken by the slider, so the unit is a whole word rather than the axis abbreviation. */
function dayValueText(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@activity.view.dayValueOne:${count}:count: day`,
    other: $localize`:@@activity.view.dayValueMany:${count}:count: days`,
  });
}

function toCountBadges(counts: ActivityCounts): readonly CountBadge[] {
  const { active, aging, inactive, unknown } = counts;
  const badges: CountBadge[] = [
    { tone: 'success', label: $localize`:@@activity.view.count.active:${active}:count: active` },
    { tone: 'warning', label: $localize`:@@activity.view.count.ageing:${aging}:count: ageing` },
    {
      tone: 'danger',
      label: $localize`:@@activity.view.count.inactive:${inactive}:count: inactive`,
    },
  ];
  if (unknown > 0) {
    badges.push({
      tone: 'neutral',
      label: $localize`:@@activity.view.count.unknown:${unknown}:count: unknown`,
    });
  }

  return badges;
}
