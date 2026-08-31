import { ProjectSettingsRequest } from '../api/api.models';

/** Initial thresholds offered to every added repository, whichever way it is added. */
export const defaultActiveUntilDays = 30;
export const defaultInactiveAfterDays = 90;

export const localBranchNamespace = 'refs/heads/*';
export const remoteBranchNamespace = 'refs/remotes/*';

const remoteHeadsPrefix = 'refs/remotes/';

/** A remote baseline observes the remote branches: the two go together. */
export function branchNamespaceFor(referenceName: string): string {
  return referenceName.startsWith(remoteHeadsPrefix) ? remoteBranchNamespace : localBranchNamespace;
}

export function defaultProjectSettings(referenceName: string): ProjectSettingsRequest {
  return {
    referenceName,
    branchNamespace: branchNamespaceFor(referenceName),
    activeUntilDays: defaultActiveUntilDays,
    inactiveAfterDays: defaultInactiveAfterDays,
    excludedPatterns: [],
    protectedPatterns: [],
  };
}
