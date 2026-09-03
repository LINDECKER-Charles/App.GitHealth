import { AnalysisReferenceProgress } from '../api/api.models';
import {
  displayReference,
  relativeAge,
  topologyLabels,
  topologyTones,
} from '../branches/branch-labels';
import { Tone } from '../../ui/icon-name';

const shortCommitLength = 8;
const emDash = '—';
const minus = '−';

/** One line of the ledger, ready to render: no branching left for the template. */
export interface AnalysisLedgerRow {
  readonly id: string;
  readonly name: string;
  readonly isReading: boolean;
  readonly isRead: boolean;
  readonly mergeBase: string;
  readonly ahead: string;
  readonly behind: string;
  readonly topologyLabel: string | null;
  readonly topologyTone: Tone;
  readonly contributors: string;
  readonly age: string;
}

export function toLedgerRow(reference: AnalysisReferenceProgress): AnalysisLedgerRow {
  const topology = reference.topology;
  return {
    id: reference.referenceName,
    name: displayReference(reference.referenceName),
    isReading: reference.state === 'Measuring' || reference.state === 'Enriching',
    isRead: reference.state === 'Measured' || reference.state === 'Read',
    mergeBase: shortCommit(reference.mergeBaseCommit),
    ahead: signedCount(reference.aheadCount, '+'),
    behind: signedCount(reference.behindCount, minus),
    topologyLabel: topology === null ? null : topologyLabels[topology],
    topologyTone: topology === null ? 'neutral' : topologyTones[topology],
    contributors: contributorsLabel(reference),
    age: reference.state === 'Read' ? relativeAge(reference.lastActivityAtUtc) : '',
  };
}

export function shortCommit(commitId: string | null): string {
  return commitId === null ? '' : commitId.slice(0, shortCommitLength);
}

/** Zero stays bare: a `+0` reads as movement where there is none. */
function signedCount(count: number | null, sign: string): string {
  if (count === null) {
    return '';
  }

  return count === 0 ? '0' : `${sign}${count}`;
}

function contributorsLabel(reference: AnalysisReferenceProgress): string {
  const count = reference.contributorCount;
  if (count === null) {
    return '';
  }

  if (count === 0 || reference.topContributor === null) {
    return emDash;
  }

  return count === 1 ? reference.topContributor : `${reference.topContributor} +${count - 1}`;
}
