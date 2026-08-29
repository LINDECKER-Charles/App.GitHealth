import {
  ActivityStatus,
  BranchRelationship,
  BranchSnapshotResponse,
  BranchTopology,
  RecommendationKind,
} from '../api/api.models';
import { IconName, Tone } from '../../ui/icon-name';

const millisecondsPerDay = 86_400_000;
const localHeadsPrefix = 'refs/heads/';
const remoteHeadsPrefix = 'refs/remotes/';
const remoteOriginPrefix = 'origin/';

export const topologyLabels: Readonly<Record<BranchTopology, string>> = {
  Synchronized: 'Synchronisée',
  Ahead: 'En avance',
  Merged: 'Fusionnée',
  Diverged: 'Divergente',
  Unrelated: 'Sans base',
};

export const topologyTones: Readonly<Record<BranchTopology, Tone>> = {
  Synchronized: 'success',
  Ahead: 'info',
  Merged: 'neutral',
  Diverged: 'warning',
  Unrelated: 'danger',
};

export const activityLabels: Readonly<Record<ActivityStatus, string>> = {
  Active: 'Active',
  Aging: 'Vieillissante',
  Inactive: 'Inactive',
  Unknown: 'Inconnue',
};

export const activityTones: Readonly<Record<ActivityStatus, Tone>> = {
  Active: 'success',
  Aging: 'warning',
  Inactive: 'danger',
  Unknown: 'neutral',
};

export const recommendationLabels: Readonly<Record<RecommendationKind, string>> = {
  Keep: 'Conserver',
  Review: 'À examiner',
  CleanupCandidate: 'Nettoyage possible',
  Excluded: 'Exclue',
  Merged: 'Terminée',
};

export const recommendationTones: Readonly<Record<RecommendationKind, Tone>> = {
  Keep: 'success',
  Review: 'warning',
  CleanupCandidate: 'danger',
  Excluded: 'neutral',
  Merged: 'merged',
};

export const recommendationIcons: Readonly<Record<RecommendationKind, IconName>> = {
  Keep: 'circle-check',
  Review: 'triangle-alert',
  CleanupCandidate: 'trash-2',
  Excluded: 'eye-off',
  Merged: 'check',
};

export const relationshipLabels: Readonly<Record<BranchRelationship, string>> = {
  SameCommit: 'Même sommet',
  CommonAncestor: 'Ancêtre commun',
  BranchIsAncestorOfReference: 'Fusionnées dans la référence',
  NoCommonAncestor: 'Sans base commune',
};

export function displayReference(referenceName: string): string {
  return referenceName.replace(localHeadsPrefix, '').replace(remoteHeadsPrefix, '');
}

export function referenceSource(referenceName: string): 'locale' | 'distante' {
  return referenceName.startsWith(remoteHeadsPrefix) ? 'distante' : 'locale';
}

/** Âge en jours pleins du dernier commit, borné à zéro pour absorber les horloges décalées. */
export function ageInDays(lastActivityAtUtc: string | null): number | null {
  if (lastActivityAtUtc === null) {
    return null;
  }

  const elapsed = Date.now() - Date.parse(lastActivityAtUtc);
  return Number.isNaN(elapsed) ? null : Math.max(0, Math.floor(elapsed / millisecondsPerDay));
}

export function relativeAge(lastActivityAtUtc: string | null): string {
  const days = ageInDays(lastActivityAtUtc);
  if (days === null) {
    return 'activité inconnue';
  }

  return days === 0 ? "aujourd'hui" : `il y a ${days} j`;
}

/** La commande que l'utilisateur copiera s'il décide de nettoyer. GitHealth ne l'exécute jamais. */
export function deleteCommand(snapshot: BranchSnapshotResponse): string {
  const shortName = displayReference(snapshot.referenceName);
  return referenceSource(snapshot.referenceName) === 'distante'
    ? `git push origin --delete ${shortName.replace(remoteOriginPrefix, '')}`
    : `git branch -d ${shortName}`;
}
