import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PolicySnapshot } from '../../../core/api/api.models';
import {
  mergedActiveUntilDays,
  mergedInactiveAfterDays,
} from '../../../core/branches/branch-policy';
import { plural } from '../../../core/workspace/plural';
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
const dayUnit = 'jour';

/** Politique de repli : elle n'est lue que le temps que le projet remonte de l'API. */
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
 * Registre d'activité : les deux curseurs sont un bac à sable local — ils rejouent
 * la règle du serveur sur le snapshot courant sans jamais écrire la politique.
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
   * Comparée par valeurs : chaque relecture du projet reconstruit l'objet, et sans cela
   * le brouillon des curseurs serait remis à zéro après la moindre analyse.
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
  protected readonly activeValueText = computed(() =>
    plural(this.draft().activeUntilDays, dayUnit),
  );
  protected readonly inactiveValueText = computed(() =>
    plural(this.draft().inactiveAfterDays, dayUnit),
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
    const saved = this.savedPolicy();
    return `active ≤ ${saved.activeUntilDays} j · inactive > ${saved.inactiveAfterDays} j`;
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
   * L'écrêtage étant invisible pour le curseur natif, la valeur retenue lui est
   * réécrite : sans cela le pouce continuerait au-delà de l'écart minimal.
   */
  private moveThreshold(event: Event, moved: MovedThreshold): void {
    const input = event.target as HTMLInputElement;
    const wanted = withThreshold(this.draft(), Number(input.value), moved);
    const clamped = clampThresholds(wanted, moved, this.bounds());
    input.value = String(moved === 'active' ? clamped.activeUntilDays : clamped.inactiveAfterDays);
    this.draft.set(clamped);
  }
}

/** Deux politiques identiques champ à champ : l'identité de l'objet ne dit rien du contenu. */
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

function toCountBadges(counts: ActivityCounts): readonly CountBadge[] {
  const badges: CountBadge[] = [
    { tone: 'success', label: plural(counts.active, 'active') },
    { tone: 'warning', label: plural(counts.aging, 'vieillissante') },
    { tone: 'danger', label: plural(counts.inactive, 'inactive') },
  ];
  if (counts.unknown > 0) {
    badges.push({ tone: 'neutral', label: plural(counts.unknown, 'inconnue') });
  }

  return badges;
}
