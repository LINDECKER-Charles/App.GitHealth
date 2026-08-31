import { AnalysisPhase, Uuid } from '../api/api.models';

/**
 * Stages of a repository in a bulk scan. `pending` covers the wait before registering as
 * well as before queueing: it is the only state from which the scan can still relaunch it.
 */
export type FolderScanJobState =
  'pending' | 'registering' | 'queued' | 'running' | 'done' | 'failed';

/** A repository kept in the selection, already saved (`projectId`) or still to be added. */
export interface FolderScanTarget {
  readonly canonicalPath: string;
  readonly name: string;
  readonly referenceName: string | null;
  readonly projectId: Uuid | null;
}

export interface FolderScanJob {
  /** The canonical path identifies the repository even before it has a project. */
  readonly key: string;
  readonly name: string;
  readonly referenceName: string | null;
  readonly projectId: Uuid | null;
  readonly analysisId: Uuid | null;
  readonly state: FolderScanJobState;
  readonly phase: AnalysisPhase | null;
  readonly message: string | null;
}

export interface FolderScanSummary {
  readonly total: number;
  readonly done: number;
  readonly failed: number;
  readonly active: number;
}

export const terminalScanStates: readonly FolderScanJobState[] = ['done', 'failed'];

export function isTerminalScanState(state: FolderScanJobState): boolean {
  return terminalScanStates.includes(state);
}
