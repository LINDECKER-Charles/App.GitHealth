import {
  ActivityStatus,
  BranchSnapshotResponse,
  BranchTopology,
  PolicySnapshot,
  RecommendationKind,
} from '../api/api.models';
import { ageInDays } from './branch-labels';

const regexSpecialCharacters = /[.+^${}()|[\]\\]/g;

/**
 * Shortened scale for branches with no own commits. Mirrors the `ActivityThresholds`
 * constants on the server side: the two must move together.
 */
export const mergedActiveUntilDays = 7;
export const mergedInactiveAfterDays = 30;

export interface AppliedThresholds {
  readonly activeUntilDays: number;
  readonly inactiveAfterDays: number;
  readonly isReduced: boolean;
}

/**
 * False when every commit of the branch is already reachable from the baseline:
 * same commit, or branch strictly an ancestor.
 */
export function hasOwnCommits(topology: BranchTopology): boolean {
  return topology !== 'Merged' && topology !== 'Synchronized';
}

/**
 * A branch with no own commits holds nothing the baseline does not already have:
 * its countdown runs on the shorter of the two scales.
 */
export function appliedThresholds(
  topology: BranchTopology,
  policy: PolicySnapshot,
): AppliedThresholds {
  if (hasOwnCommits(topology)) {
    return {
      activeUntilDays: policy.activeUntilDays,
      inactiveAfterDays: policy.inactiveAfterDays,
      isReduced: false,
    };
  }

  const activeUntilDays = Math.min(policy.activeUntilDays, mergedActiveUntilDays);
  const inactiveAfterDays = Math.min(policy.inactiveAfterDays, mergedInactiveAfterDays);
  return {
    activeUntilDays,
    inactiveAfterDays,
    isReduced:
      activeUntilDays !== policy.activeUntilDays || inactiveAfterDays !== policy.inactiveAfterDays,
  };
}

/**
 * Client-side projection of the rules applied by `BranchClassifier` on the server.
 * It only shows the effect of a policy before it is saved: the authoritative
 * recommendation stays the one from the snapshot returned by the API.
 */
export function projectRecommendation(
  snapshot: BranchSnapshotResponse,
  policy: PolicySnapshot,
): RecommendationKind {
  const isProtected = matchPattern(policy.protectedPatterns, snapshot.referenceName) !== null;
  const isExcluded = matchPattern(policy.excludedPatterns, snapshot.referenceName) !== null;
  if (isProtected || isExcluded) {
    return 'Excluded';
  }

  const activity = projectActivity(snapshot, policy);
  if (!hasOwnCommits(snapshot.topology)) {
    return recommendWithoutOwnCommits(activity);
  }

  return activity === 'Inactive' || isUnsettled(snapshot.topology) ? 'Review' : 'Keep';
}

export function projectActivity(
  snapshot: BranchSnapshotResponse,
  policy: PolicySnapshot,
): ActivityStatus {
  const days = ageInDays(snapshot.lastActivityAtUtc);
  if (days === null) {
    return 'Unknown';
  }

  const thresholds = appliedThresholds(snapshot.topology, policy);
  if (days <= thresholds.activeUntilDays) {
    return 'Active';
  }

  return days <= thresholds.inactiveAfterDays ? 'Aging' : 'Inactive';
}

/** First pattern that captures the reference, or `null`. Reproduces the `*` and `?` wildcards. */
export function matchPattern(patterns: readonly string[], referenceName: string): string | null {
  return patterns.find((pattern) => toRegExp(pattern).test(referenceName)) ?? null;
}

export function parsePatterns(value: string): readonly string[] {
  return Array.from(
    new Set(
      value
        .split(/\r?\n/)
        .map((pattern) => pattern.trim())
        .filter((pattern) => pattern.length > 0),
    ),
  );
}

function recommendWithoutOwnCommits(activity: ActivityStatus): RecommendationKind {
  if (activity === 'Inactive') {
    return 'CleanupCandidate';
  }

  return activity === 'Aging' ? 'Review' : 'Merged';
}

function isUnsettled(topology: BranchTopology): boolean {
  return topology === 'Diverged' || topology === 'Unrelated';
}

function toRegExp(pattern: string): RegExp {
  const escaped = pattern
    .replace(regexSpecialCharacters, '\\$&')
    .replace(/\*/g, '.*')
    .replace(/\?/g, '.');
  return new RegExp(`^${escaped}$`);
}
