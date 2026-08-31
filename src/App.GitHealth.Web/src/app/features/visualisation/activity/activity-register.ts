import {
  ActivityStatus,
  BranchSnapshotResponse,
  PolicySnapshot,
} from '../../../core/api/api.models';
import {
  activityLabels,
  activityTones,
  ageInDays,
  displayReference,
  recommendationLabels,
  recommendationTones,
  relativeAge,
} from '../../../core/branches/branch-labels';
import {
  AppliedThresholds,
  appliedThresholds,
  matchPattern,
  projectActivity,
  projectRecommendation,
} from '../../../core/branches/branch-policy';
import { IconName, Tone } from '../../../ui/icon-name';

/** Domaine plancher : il reproduit exactement l'axe de la politique par défaut (30 / 90 j). */
const minimumTimelineDays = 120;
const timelineStepDays = 30;
const timelineHeadroomNumerator = 4;
const timelineHeadroomDenominator = 3;

/** Écart minimal entre les deux seuils : en deçà, la bande « vieillissante » n'est plus lisible. */
const minimumThresholdGapDays = 8;
const minimumActiveUntilDays = 1;
const minimumInactiveAfterDays = minimumActiveUntilDays + minimumThresholdGapDays;

const fullTrackPercent = 100;
const axisTickCount = 5;
const clampMarker = '▸';
const unknownActivityLabel = 'activité inconnue';
const protectedFlagLabel = 'motif protégé';
const excludedFlagLabel = 'motif exclu';

/** En deçà, l'étiquette d'une règle sortirait de la piste : elle bascule à gauche du trait. */
const labelFlipEdgePercent = 10;

export type MovedThreshold = 'active' | 'inactive';

export type TickAnchor = 'start' | 'center' | 'end';

export interface ThresholdDraft {
  readonly activeUntilDays: number;
  readonly inactiveAfterDays: number;
}

export interface ThresholdBounds {
  readonly activeMin: number;
  readonly activeMax: number;
  readonly inactiveMin: number;
  readonly inactiveMax: number;
}

export interface ThresholdBoundsRequest {
  readonly saved: ThresholdDraft;
  readonly timelineDays: number;
}

export interface PatternFlag {
  readonly icon: IconName;
  readonly kind: 'protected' | 'excluded';
  readonly label: string;
}

/** Ligne prête à afficher : le gabarit ne fait plus que poser les pourcentages. */
export interface ActivityRow {
  readonly id: string;
  readonly name: string;
  readonly activity: ActivityStatus;
  readonly tone: Tone;
  readonly hasMark: boolean;
  readonly offsetPercent: number;
  readonly barWidthPercent: number;
  readonly ageLabel: string;
  readonly clampLabel: string | null;
  readonly verdictLabel: string;
  readonly verdictTone: Tone;
  readonly flag: PatternFlag | null;
  /** Vrai quand la branche se mesure sur une échelle plus courte que celle des zones. */
  readonly isReduced: boolean;
  readonly scaleLabel: string | null;
  readonly trackLabel: string;
}

export interface PolicyBands {
  readonly activeEdgePercent: number;
  readonly inactiveEdgePercent: number;
  readonly agingWidthPercent: number;
  readonly activeLabel: string;
  readonly inactiveLabel: string;
  readonly isActiveLabelTrailing: boolean;
  readonly isInactiveLabelTrailing: boolean;
}

export interface AxisTick {
  readonly label: string;
  readonly leftPercent: number;
  readonly anchor: TickAnchor;
}

export interface ActivityCounts {
  readonly active: number;
  readonly aging: number;
  readonly inactive: number;
  readonly unknown: number;
}

export interface ActivityRegisterRequest {
  readonly branches: readonly BranchSnapshotResponse[];
  readonly policy: PolicySnapshot;
  readonly timelineDays: number;
}

/**
 * Le domaine se déduit de la politique enregistrée et jamais du brouillon : l'axe
 * doit rester immobile pendant qu'on tire les curseurs.
 */
export function timelineDaysFor(savedInactiveAfterDays: number): number {
  const wanted = (savedInactiveAfterDays * timelineHeadroomNumerator) / timelineHeadroomDenominator;
  const stepped = Math.ceil(wanted / timelineStepDays) * timelineStepDays;
  return Math.max(minimumTimelineDays, stepped);
}

/** Les planchers cèdent devant la politique enregistrée : un seuil plus bas reste atteignable. */
export function thresholdBounds(request: ThresholdBoundsRequest): ThresholdBounds {
  const { saved, timelineDays } = request;
  return {
    activeMin: Math.min(minimumActiveUntilDays, saved.activeUntilDays),
    activeMax: timelineDays - minimumThresholdGapDays,
    inactiveMin: Math.min(minimumInactiveAfterDays, saved.inactiveAfterDays),
    inactiveMax: timelineDays,
  };
}

/** Le brouillon naît dans les bornes du curseur : le pouce démarre où son étiquette l'annonce. */
export function seedDraft(saved: ThresholdDraft, bounds: ThresholdBounds): ThresholdDraft {
  return {
    activeUntilDays: clamp(saved.activeUntilDays, bounds.activeMin, bounds.activeMax),
    inactiveAfterDays: clamp(saved.inactiveAfterDays, bounds.inactiveMin, bounds.inactiveMax),
  };
}

/** L'écart minimal se tient en écrêtant le seul curseur déplacé : l'autre ne bouge jamais. */
export function clampThresholds(
  draft: ThresholdDraft,
  moved: MovedThreshold,
  bounds: ThresholdBounds,
): ThresholdDraft {
  if (moved === 'active') {
    const ceiling = draft.inactiveAfterDays - minimumThresholdGapDays;
    return {
      activeUntilDays: Math.max(bounds.activeMin, Math.min(draft.activeUntilDays, ceiling)),
      inactiveAfterDays: draft.inactiveAfterDays,
    };
  }

  const floor = Math.max(bounds.inactiveMin, draft.activeUntilDays + minimumThresholdGapDays);
  return {
    activeUntilDays: draft.activeUntilDays,
    inactiveAfterDays: Math.max(floor, draft.inactiveAfterDays),
  };
}

export function buildPolicyBands(policy: PolicySnapshot, timelineDays: number): PolicyBands {
  const activeEdgePercent = offsetPercent(policy.activeUntilDays, timelineDays);
  const inactiveEdgePercent = offsetPercent(policy.inactiveAfterDays, timelineDays);
  return {
    activeEdgePercent,
    inactiveEdgePercent,
    agingWidthPercent: Math.max(0, inactiveEdgePercent - activeEdgePercent),
    activeLabel: `${policy.activeUntilDays} j`,
    inactiveLabel: `${policy.inactiveAfterDays} j`,
    isActiveLabelTrailing: activeEdgePercent < labelFlipEdgePercent,
    isInactiveLabelTrailing: inactiveEdgePercent < labelFlipEdgePercent,
  };
}

export function buildAxisTicks(timelineDays: number): readonly AxisTick[] {
  const lastIndex = axisTickCount - 1;
  return Array.from({ length: axisTickCount }, (_unused, index) => {
    const ratio = index / lastIndex;
    return {
      label: axisTickLabel(timelineDays, ratio),
      leftPercent: ratio * fullTrackPercent,
      anchor: tickAnchor(index, lastIndex),
    };
  });
}

export function buildActivityRows(request: ActivityRegisterRequest): readonly ActivityRow[] {
  return request.branches.map((branch) => toActivityRow(branch, request));
}

export function activityCounts(rows: readonly ActivityRow[]): ActivityCounts {
  return {
    active: countActivity(rows, 'Active'),
    aging: countActivity(rows, 'Aging'),
    inactive: countActivity(rows, 'Inactive'),
    unknown: countActivity(rows, 'Unknown'),
  };
}

function toActivityRow(
  branch: BranchSnapshotResponse,
  request: ActivityRegisterRequest,
): ActivityRow {
  const { policy, timelineDays } = request;
  const days = ageInDays(branch.lastActivityAtUtc);
  const activity = projectActivity(branch, policy);
  const recommendation = projectRecommendation(branch, policy);
  const applied = appliedThresholds(branch.topology, policy);
  const scaleLabel = applied.isReduced ? reducedScaleLabel(applied) : null;
  const name = displayReference(branch.referenceName);
  const offset = days === null ? 0 : offsetPercent(days, timelineDays);
  const track = trackLabel(name, branch.lastActivityAtUtc, activity);
  return {
    id: branch.id,
    name,
    activity,
    tone: activityTones[activity],
    hasMark: days !== null,
    offsetPercent: offset,
    barWidthPercent: offset,
    ageLabel: relativeAge(branch.lastActivityAtUtc),
    clampLabel: days !== null && days > timelineDays ? `${days} j ${clampMarker}` : null,
    verdictLabel: recommendationLabels[recommendation],
    verdictTone: recommendationTones[recommendation],
    flag: patternFlag(branch, policy),
    isReduced: applied.isReduced,
    scaleLabel,
    trackLabel: scaleLabel === null ? track : `${track} · ${scaleLabel}`,
  };
}

/** La ligne dit elle-même son échelle : les zones, elles, tracent la politique entière. */
function reducedScaleLabel({ activeUntilDays, inactiveAfterDays }: AppliedThresholds): string {
  return `échelle réduite · active ≤ ${activeUntilDays} j · inactive > ${inactiveAfterDays} j`;
}

/** Au-delà du domaine la marque se colle au bord gauche : c'est l'étiquette qui dit le vrai âge. */
function offsetPercent(days: number, timelineDays: number): number {
  if (timelineDays <= 0) {
    return 0;
  }

  return (clamp(days, 0, timelineDays) / timelineDays) * fullTrackPercent;
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(Math.max(value, minimum), maximum);
}

function patternFlag(branch: BranchSnapshotResponse, policy: PolicySnapshot): PatternFlag | null {
  if (matchPattern(policy.protectedPatterns, branch.referenceName) !== null) {
    return { icon: 'lock', kind: 'protected', label: protectedFlagLabel };
  }

  if (matchPattern(policy.excludedPatterns, branch.referenceName) !== null) {
    return { icon: 'eye-off', kind: 'excluded', label: excludedFlagLabel };
  }

  return null;
}

function trackLabel(name: string, lastActivity: string | null, activity: ActivityStatus): string {
  if (activity === 'Unknown') {
    return `${name} · ${unknownActivityLabel}`;
  }

  const state = activityLabels[activity].toLowerCase();
  return `${name} · dernière activité ${relativeAge(lastActivity)} · ${state}`;
}

function axisTickLabel(timelineDays: number, ratio: number): string {
  const days = Math.round(timelineDays * (1 - ratio));
  if (days === 0) {
    return "aujourd'hui";
  }

  return ratio === 0 ? `il y a ${days} j` : `${days} j`;
}

function tickAnchor(index: number, lastIndex: number): TickAnchor {
  if (index === 0) {
    return 'start';
  }

  return index === lastIndex ? 'end' : 'center';
}

function countActivity(rows: readonly ActivityRow[], activity: ActivityStatus): number {
  return rows.filter((row) => row.activity === activity).length;
}
