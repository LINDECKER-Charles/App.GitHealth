import { AnalysisPhase, BranchSnapshotResponse } from '../../core/api/api.models';

export const analysisPhases: readonly AnalysisPhase[] = [
  'Waiting',
  'Topology',
  'Enrichment',
  'Persistence',
  'Finished',
];

const phaseLabels: Record<AnalysisPhase, string> = {
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

export function displayReference(referenceName: string): string {
  return referenceName.replace(/^refs\/heads\//, '').replace(/^refs\/remotes\//, '');
}

export function referenceSource(referenceName: string): 'locale' | 'distante' {
  return referenceName.startsWith('refs/remotes/') ? 'distante' : 'locale';
}

export function topologyTone(snapshot: BranchSnapshotResponse): string {
  if (snapshot.topology === 'Diverged' || snapshot.topology === 'Unrelated') {
    return 'attention';
  }

  return snapshot.topology === 'Merged' ? 'settled' : 'current';
}

export function relativeAge(value: string | null): string {
  if (value === null) {
    return 'activité inconnue';
  }

  const days = Math.max(0, Math.floor((Date.now() - Date.parse(value)) / 86_400_000));
  if (days === 0) {
    return "aujourd'hui";
  }

  return `il y a ${days} jour${days > 1 ? 's' : ''}`;
}
