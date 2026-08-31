import { AnalysisHistoryItem, AnalysisRunStatus } from '../../core/api/api.models';
import { displayReference } from '../../core/branches/branch-labels';
import { pluralMessage } from '../../core/i18n/plural-message';
import { elapsedDuration } from '../../core/workspace/relative-time';
import { Tone } from '../../ui/icon-name';

/** Analysis run as the card shows it: every value comes from a recorded fact. */
export interface AnalysisRun {
  readonly id: string;
  readonly shortId: string;
  readonly statusLabel: string;
  readonly tone: Tone;
  readonly isOpenable: boolean;
  readonly startedAtUtc: string;
  readonly duration: string;
  readonly reference: string;
  readonly branchCount: string;
  readonly thresholds: string;
  readonly delta: string;
  readonly failureCode: string | null;
  readonly failureMessage: string | null;
  readonly protectedPatterns: readonly string[];
  readonly excludedPatterns: readonly string[];
}

const statusLabels: Readonly<Record<AnalysisRunStatus, string>> = {
  Running: $localize`:@@history.status.running:Running`,
  Completed: $localize`:@@history.status.completed:Completed`,
  Failed: $localize`:@@history.status.failed:Failed`,
  Cancelled: $localize`:@@history.status.cancelled:Cancelled`,
};

const statusTones: Readonly<Record<AnalysisRunStatus, Tone>> = {
  Running: 'info',
  Completed: 'success',
  Failed: 'danger',
  Cancelled: 'neutral',
};

const shortIdLength = 8;

/**
 * The history arrives newest first, so a card's difference is read against the
 * next item in the list, which is the previous run.
 */
export function toRuns(items: readonly AnalysisHistoryItem[]): readonly AnalysisRun[] {
  return items.map((item, index) => toRun(item, items[index + 1]));
}

function toRun(item: AnalysisHistoryItem, previous: AnalysisHistoryItem | undefined): AnalysisRun {
  const isCompleted = item.status === 'Completed';
  return {
    id: item.analysisId,
    shortId: item.analysisId.slice(0, shortIdLength),
    statusLabel: statusLabels[item.status],
    tone: statusTones[item.status],
    isOpenable: isCompleted,
    startedAtUtc: item.startedAtUtc,
    duration: elapsedDuration(item.startedAtUtc, item.completedAtUtc),
    reference: displayReference(item.referenceName),
    branchCount: isCompleted ? branchCountLabel(item.branchCount) : '—',
    thresholds: thresholdLabel(item),
    delta: branchDelta(item, previous),
    failureCode: item.failureCode,
    failureMessage: item.failureMessage,
    protectedPatterns: item.protectedPatterns,
    excludedPatterns: item.excludedPatterns,
  };
}

function thresholdLabel(item: AnalysisHistoryItem): string {
  const active = item.activeUntilDays;
  const inactive = item.inactiveAfterDays;
  return $localize`:@@history.run.thresholds:${active} / ${inactive} d`;
}

function branchDelta(item: AnalysisHistoryItem, previous: AnalysisHistoryItem | undefined): string {
  if (item.status !== 'Completed' || previous?.status !== 'Completed') {
    return '—';
  }

  const delta = item.branchCount - previous.branchCount;
  if (delta === 0) {
    return $localize`:@@history.delta.unchanged:unchanged`;
  }

  return delta > 0 ? addedBranchesLabel(delta) : removedBranchesLabel(Math.abs(delta));
}

function branchCountLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@history.branchCount.one:${count}:count: branch`,
    other: $localize`:@@history.branchCount.many:${count}:count: branches`,
  });
}

/** The sign belongs to the sentence: a locale is free to place it elsewhere. */
function addedBranchesLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@history.delta.addedOne:+${count}:count: branch`,
    other: $localize`:@@history.delta.addedMany:+${count}:count: branches`,
  });
}

function removedBranchesLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@history.delta.removedOne:−${count}:count: branch`,
    other: $localize`:@@history.delta.removedMany:−${count}:count: branches`,
  });
}
