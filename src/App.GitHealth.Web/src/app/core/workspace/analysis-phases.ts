import { AnalysisPhase } from '../api/api.models';

/** Les cinq étapes visibles d'une analyse réussie, dans l'ordre où l'API les traverse. */
export const analysisPhases: readonly AnalysisPhase[] = [
  'Waiting',
  'Topology',
  'Enrichment',
  'Persistence',
  'Finished',
];

const phaseLabels: Readonly<Record<AnalysisPhase, string>> = {
  Waiting: 'En attente',
  Topology: 'Topologie',
  Enrichment: 'Contributeurs',
  Persistence: 'Enregistrement',
  Finished: 'Terminée',
  Failed: 'Échec',
  Cancelled: 'Annulée',
};

export function phaseLabel(phase: AnalysisPhase): string {
  return phaseLabels[phase];
}

/** Rang de la phase dans la séquence, ou `-1` pour un échec et une annulation. */
export function phaseIndex(phase: AnalysisPhase): number {
  return analysisPhases.indexOf(phase);
}
