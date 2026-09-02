import { AnalysisPhase } from '../api/api.models';
import { analysisPhases, phaseIndex, phaseLabel } from '../workspace/analysis-phases';

/** A stage that has started shows a sliver even at zero: nothing means "not reached". */
const minimumStartedFill = 4;

/** Stages with no per-reference count still have to move; these say roughly how far in. */
const waitingFill = 40;
const persistenceFill = 60;
const fullFill = 100;

export interface AnalysisPhaseStep {
  readonly phase: AnalysisPhase;
  readonly label: string;
  readonly fill: string;
  readonly isDone: boolean;
  readonly isCurrent: boolean;
}

/**
 * The five stages of a run, each with how far it got. A failed or cancelled run is not one
 * of the five: it leaves every stage where it stood, which is what the reader must see.
 */
export function buildPhaseSteps(
  current: AnalysisPhase,
  processed: number,
  total: number,
): readonly AnalysisPhaseStep[] {
  const rank = phaseIndex(current);
  const isFinished = current === 'Finished';
  return analysisPhases.map((phase, index) => {
    const isDone = isFinished || (rank >= 0 && index < rank);
    const isCurrent = !isFinished && index === rank;
    return {
      phase,
      label: phaseLabel(phase),
      fill: `${fillPercent(phase, isDone, isCurrent, ratio(processed, total))}%`,
      isDone,
      isCurrent,
    };
  });
}

function fillPercent(
  phase: AnalysisPhase,
  isDone: boolean,
  isCurrent: boolean,
  progress: number,
): number {
  if (isDone) {
    return fullFill;
  }

  if (!isCurrent) {
    return 0;
  }

  if (phase === 'Topology' || phase === 'Enrichment') {
    return Math.max(minimumStartedFill, Math.round(progress * fullFill));
  }

  return phase === 'Waiting' ? waitingFill : persistenceFill;
}

function ratio(processed: number, total: number): number {
  return total === 0 ? 0 : processed / total;
}
