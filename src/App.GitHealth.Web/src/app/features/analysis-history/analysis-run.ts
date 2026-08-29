import { AnalysisHistoryItem, AnalysisRunStatus } from '../../core/api/api.models';
import { displayReference } from '../../core/branches/branch-labels';
import { elapsedDuration } from '../../core/workspace/relative-time';
import { plural } from '../../core/workspace/plural';
import { Tone } from '../../ui/icon-name';

/** Passage d'analyse tel que la carte l'affiche : chaque valeur vient d'un fait enregistré. */
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
  Running: 'En cours',
  Completed: 'Terminée',
  Failed: 'Échouée',
  Cancelled: 'Annulée',
};

const statusTones: Readonly<Record<AnalysisRunStatus, Tone>> = {
  Running: 'info',
  Completed: 'success',
  Failed: 'danger',
  Cancelled: 'neutral',
};

const shortIdLength = 8;

/**
 * L'historique arrive de la plus récente à la plus ancienne : l'écart d'une carte
 * se lit donc contre l'élément suivant, qui est le passage précédent.
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
    branchCount: isCompleted ? plural(item.branchCount, 'branche') : '—',
    thresholds: `${item.activeUntilDays} / ${item.inactiveAfterDays} j`,
    delta: branchDelta(item, previous),
    failureCode: item.failureCode,
    failureMessage: item.failureMessage,
    protectedPatterns: item.protectedPatterns,
    excludedPatterns: item.excludedPatterns,
  };
}

function branchDelta(item: AnalysisHistoryItem, previous: AnalysisHistoryItem | undefined): string {
  if (item.status !== 'Completed' || previous?.status !== 'Completed') {
    return '—';
  }

  const delta = item.branchCount - previous.branchCount;
  if (delta === 0) {
    return 'inchangé';
  }

  return delta > 0 ? `+${plural(delta, 'branche')}` : `−${plural(Math.abs(delta), 'branche')}`;
}
