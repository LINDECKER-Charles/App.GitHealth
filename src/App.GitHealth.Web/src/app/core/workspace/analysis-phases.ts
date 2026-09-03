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

/**
 * What the phase is actually doing, in Git terms. Naming the commands behind each stage is
 * what turns a progress bar into a promise the reader can check.
 */
const phaseDetails: Readonly<Record<AnalysisPhase, string>> = {
  Waiting: $localize`:@@phase.detail.waiting:queue slot granted, opening the repository`,
  Topology: $localize`:@@phase.detail.topology:merge base and ahead / behind, per reference`,
  Enrichment: $localize`:@@phase.detail.enrichment:tip date, tip author and shortlog, per reference`,
  Persistence: $localize`:@@phase.detail.persistence:writing the snapshot to githealth.db`,
  Finished: $localize`:@@phase.detail.finished:no Git write`,
  Failed: $localize`:@@phase.detail.failed:the repository was left untouched`,
  Cancelled: $localize`:@@phase.detail.cancelled:stopped before the end, nothing written`,
};

export function phaseLabel(phase: AnalysisPhase): string {
  return phaseLabels[phase];
}

export function phaseDetail(phase: AnalysisPhase): string {
  return phaseDetails[phase];
}

/** Rank of the phase in the sequence, or `-1` for a failure and a cancellation. */
export function phaseIndex(phase: AnalysisPhase): number {
  return analysisPhases.indexOf(phase);
}
