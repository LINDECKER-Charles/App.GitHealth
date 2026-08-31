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
  Synchronized: $localize`:@@branchLabel.topology.synchronized:In sync`,
  Ahead: $localize`:@@branchLabel.topology.ahead:Ahead`,
  Merged: $localize`:@@branchLabel.topology.merged:Merged`,
  Diverged: $localize`:@@branchLabel.topology.diverged:Diverged`,
  Unrelated: $localize`:@@branchLabel.topology.unrelated:No merge base`,
};

export const topologyTones: Readonly<Record<BranchTopology, Tone>> = {
  Synchronized: 'success',
  Ahead: 'info',
  Merged: 'neutral',
  Diverged: 'warning',
  Unrelated: 'danger',
};

export const activityLabels: Readonly<Record<ActivityStatus, string>> = {
  Active: $localize`:@@activity.status.active:Active`,
  Aging: $localize`:@@activity.status.aging:Ageing`,
  Inactive: $localize`:@@activity.status.inactive:Inactive`,
  Unknown: $localize`:@@activity.status.unknown:Unknown`,
};

export const activityTones: Readonly<Record<ActivityStatus, Tone>> = {
  Active: 'success',
  Aging: 'warning',
  Inactive: 'danger',
  Unknown: 'neutral',
};

export const recommendationLabels: Readonly<Record<RecommendationKind, string>> = {
  Keep: $localize`:@@recommendation.kind.keep:Keep`,
  Review: $localize`:@@recommendation.kind.review:Review`,
  CleanupCandidate: $localize`:@@recommendation.kind.cleanupCandidate:Cleanup possible`,
  Excluded: $localize`:@@recommendation.kind.excluded:Excluded`,
  Merged: $localize`:@@recommendation.kind.merged:Done`,
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
  SameCommit: $localize`:@@relationship.kind.sameCommit:Same commit`,
  CommonAncestor: $localize`:@@relationship.kind.commonAncestor:Common ancestor`,
  BranchIsAncestorOfReference: $localize`:@@relationship.kind.ancestor:Merged into the baseline`,
  NoCommonAncestor: $localize`:@@relationship.kind.noCommonAncestor:No common ancestor`,
};

export function displayReference(referenceName: string): string {
  return referenceName.replace(localHeadsPrefix, '').replace(remoteHeadsPrefix, '');
}

export type ReferenceSource = 'local' | 'remote';

/** Human label for a reference source; the union itself is a discriminant, never display text. */
export const referenceSourceLabels: Readonly<Record<ReferenceSource, string>> = {
  local: $localize`:@@branchLabel.referenceSource.local:local`,
  remote: $localize`:@@branchLabel.referenceSource.remote:remote`,
};

export function referenceSource(referenceName: string): ReferenceSource {
  return referenceName.startsWith(remoteHeadsPrefix) ? 'remote' : 'local';
}

/** Age of the last commit in whole days, clamped to zero to absorb skewed clocks. */
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
    return $localize`:@@branchLabel.age.unknown:unknown activity`;
  }

  return days === 0
    ? $localize`:@@branchLabel.age.today:today`
    : $localize`:@@branchLabel.age.days:${days}:dayCount: d ago`;
}

/** The command the user copies if they decide to clean up. GitHealth never runs it. */
export function deleteCommand(snapshot: BranchSnapshotResponse): string {
  const shortName = displayReference(snapshot.referenceName);
  return referenceSource(snapshot.referenceName) === 'remote'
    ? `git push origin --delete ${shortName.replace(remoteOriginPrefix, '')}`
    : `git branch -d ${shortName}`;
}
