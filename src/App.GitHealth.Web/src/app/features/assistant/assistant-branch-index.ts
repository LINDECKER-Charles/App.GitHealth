import { BranchSnapshotResponse } from '../../core/api/api.models';
import { displayReference } from '../../core/branches/branch-labels';

/**
 * The branch names an answer may mention, and the row each one opens. Both spellings are
 * indexed: the tools hand the agent full reference names, but an answer written for a human
 * usually shortens them, and either should open the same row rather than only the one the
 * agent happened to pick.
 */
export interface AssistantBranchIndex {
  readonly names: readonly string[];
  readonly rows: ReadonlyMap<string, string>;
}

export const emptyBranchIndex: AssistantBranchIndex = { names: [], rows: new Map() };

export function buildBranchIndex(
  branches: readonly BranchSnapshotResponse[],
): AssistantBranchIndex {
  const rows = new Map<string, string>();
  for (const branch of branches) {
    remember(rows, branch.referenceName, branch.id);
    remember(rows, displayReference(branch.referenceName), branch.id);
  }

  return { names: [...rows.keys()], rows };
}

/**
 * The first row to claim a spelling keeps it. Two branches can shorten to the same name —
 * a local and its remote — and guessing between them is worse than opening the first.
 */
function remember(rows: Map<string, string>, name: string, snapshotId: string): void {
  if (name.length > 0 && !rows.has(name)) {
    rows.set(name, snapshotId);
  }
}
