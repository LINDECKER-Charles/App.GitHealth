import { AnalysisPhase } from '../api/api.models';

/** The five visible stages of a successful analysis, in the order the API goes through. */
export const analysisPhases: readonly AnalysisPhase[] = [
  'Waiting',
  'Topology',
  'Enrichment',
  'Persistence',
  'Finished',
];

const phaseLabels: Readonly<Record<AnalysisPhase, string>> = {
  Waiting: $localize`:@@phase.analysis.waiting:Waiting`,
  Topology: $localize`:@@phase.analysis.topology:Topology`,
  Enrichment: $localize`:@@phase.analysis.enrichment:Contributors`,
  Persistence: $localize`:@@phase.analysis.persistence:Saving`,
  Finished: $localize`:@@phase.analysis.finished:Finished`,
  Failed: $localize`:@@phase.analysis.failed:Failed`,
  Cancelled: $localize`:@@phase.analysis.cancelled:Cancelled`,
};

export function phaseLabel(phase: AnalysisPhase): string {
  return phaseLabels[phase];
}

/** Rank of the phase in the sequence, or `-1` for a failure and a cancellation. */
export function phaseIndex(phase: AnalysisPhase): number {
  return analysisPhases.indexOf(phase);
}
