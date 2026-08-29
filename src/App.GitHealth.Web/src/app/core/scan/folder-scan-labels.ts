import { Tone } from '../../ui/icon-name';
import { phaseLabel } from '../workspace/analysis-phases';
import { FolderScanJob, FolderScanJobState } from './folder-scan.models';

export const scanStateLabels: Readonly<Record<FolderScanJobState, string>> = {
  pending: 'En attente',
  registering: 'Enregistrement',
  queued: 'En file',
  running: 'Analyse',
  done: 'Terminée',
  failed: 'Échec',
};

export const scanStateTones: Readonly<Record<FolderScanJobState, Tone>> = {
  pending: 'neutral',
  registering: 'info',
  queued: 'info',
  running: 'brand',
  done: 'success',
  failed: 'danger',
};

/** Une analyse en cours affiche son étape ; les autres états se suffisent à eux-mêmes. */
export function scanJobDetail(job: FolderScanJob): string {
  if (job.message !== null) {
    return job.message;
  }

  return job.state === 'running' && job.phase !== null
    ? phaseLabel(job.phase)
    : scanStateLabels[job.state];
}
