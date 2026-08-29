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
 * Échelle réduite des branches sans commit propre. Reprend, en miroir, les constantes
 * de `ActivityThresholds` côté serveur : les deux doivent bouger ensemble.
 */
export const mergedActiveUntilDays = 7;
export const mergedInactiveAfterDays = 30;

export interface AppliedThresholds {
  readonly activeUntilDays: number;
  readonly inactiveAfterDays: number;
  readonly isReduced: boolean;
}

/**
 * Faux quand tous les commits de la branche sont déjà accessibles depuis la référence :
 * sommet identique, ou branche strictement ancêtre.
 */
export function hasOwnCommits(topology: BranchTopology): boolean {
  return topology !== 'Merged' && topology !== 'Synchronized';
}

/**
 * Une branche sans commit propre ne détient plus rien que la référence n'ait déjà :
 * son compte à rebours court sur la plus courte des deux échelles.
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
 * Projection cliente des règles appliquées par `BranchClassifier` côté serveur.
 * Elle ne sert qu'à montrer l'effet d'une politique avant enregistrement : la
 * recommandation qui fait foi reste celle du snapshot renvoyé par l'API.
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

/** Premier motif qui capture la référence, ou `null`. Reproduit les jokers `*` et `?`. */
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
