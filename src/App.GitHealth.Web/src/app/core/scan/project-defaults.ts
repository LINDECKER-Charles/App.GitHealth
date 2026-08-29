import { ProjectSettingsRequest } from '../api/api.models';

/** Seuils initiaux proposés à tout dépôt ajouté, quel que soit le chemin d'ajout. */
export const defaultActiveUntilDays = 30;
export const defaultInactiveAfterDays = 90;

export const localBranchNamespace = 'refs/heads/*';
export const remoteBranchNamespace = 'refs/remotes/*';

const remoteHeadsPrefix = 'refs/remotes/';

/** Une référence distante observe les branches distantes : les deux vont de pair. */
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
