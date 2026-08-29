import { AnalysisPhase, Uuid } from '../api/api.models';

/**
 * Étapes d'un dépôt dans un scan groupé. `pending` couvre l'attente avant enregistrement
 * comme avant mise en file : c'est le seul état d'où le scan peut encore relancer le dépôt.
 */
export type FolderScanJobState =
  'pending' | 'registering' | 'queued' | 'running' | 'done' | 'failed';

/** Un dépôt retenu dans la sélection, déjà enregistré (`projectId`) ou encore à ajouter. */
export interface FolderScanTarget {
  readonly canonicalPath: string;
  readonly name: string;
  readonly referenceName: string | null;
  readonly projectId: Uuid | null;
}

export interface FolderScanJob {
  /** Le chemin canonique identifie le dépôt avant même qu'il ait un projet. */
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
