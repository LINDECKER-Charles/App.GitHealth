import { Tone } from '../../ui/icon-name';
import { phaseLabel } from '../workspace/analysis-phases';
import { FolderScanJob, FolderScanJobState } from './folder-scan.models';

export const scanStateLabels: Readonly<Record<FolderScanJobState, string>> = {
  pending: $localize`:@@scanState.job.pending:Pending`,
  registering: $localize`:@@scanState.job.registering:Registering`,
  queued: $localize`:@@scanState.job.queued:Queued`,
  running: $localize`:@@scanState.job.running:Analysing`,
  done: $localize`:@@scanState.job.done:Done`,
  failed: $localize`:@@scanState.job.failed:Failed`,
};

export const scanStateTones: Readonly<Record<FolderScanJobState, Tone>> = {
  pending: 'neutral',
  registering: 'info',
  queued: 'info',
  running: 'brand',
  done: 'success',
  failed: 'danger',
};

/** A running analysis shows its stage; the other states speak for themselves. */
export function scanJobDetail(job: FolderScanJob): string {
  if (job.message !== null) {
    return job.message;
  }

  return job.state === 'running' && job.phase !== null
    ? phaseLabel(job.phase)
    : scanStateLabels[job.state];
}
