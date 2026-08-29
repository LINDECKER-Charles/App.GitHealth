import {
  BranchSnapshotResponse,
  PolicySnapshot,
  RecommendationKind,
} from '../../core/api/api.models';
import { recommendationLabels, recommendationTones } from '../../core/branches/branch-labels';
import { matchPattern, projectRecommendation } from '../../core/branches/branch-policy';
import { Tone } from '../../ui/icon-name';

export interface PolicyStat {
  readonly label: string;
  readonly tone: Tone;
  readonly count: number;
  readonly delta: string;
}

export interface PolicyMatch {
  readonly referenceName: string;
  readonly flag: string;
  readonly tone: Tone;
}

const projectedKinds: readonly RecommendationKind[] = [
  'Keep',
  'Merged',
  'Review',
  'CleanupCandidate',
  'Excluded',
];

/**
 * Compare la politique en cours d'édition aux recommandations déjà renvoyées par
 * l'API pour le même snapshot. La référence est donc la politique enregistrée.
 */
export function projectStats(
  branches: readonly BranchSnapshotResponse[],
  policy: PolicySnapshot,
): readonly PolicyStat[] {
  const projected = branches.map((branch) => projectRecommendation(branch, policy));
  return projectedKinds.map((kind) => {
    const count = projected.filter((value) => value === kind).length;
    const saved = branches.filter((branch) => branch.recommendation === kind).length;
    return {
      label: recommendationLabels[kind],
      tone: recommendationTones[kind],
      count,
      delta: formatDelta(count - saved),
    };
  });
}

export function projectMatches(
  branches: readonly BranchSnapshotResponse[],
  policy: PolicySnapshot,
): readonly PolicyMatch[] {
  return branches
    .map((branch) => toMatch(branch, policy))
    .filter((match): match is PolicyMatch => match !== null);
}

function toMatch(branch: BranchSnapshotResponse, policy: PolicySnapshot): PolicyMatch | null {
  if (matchPattern(policy.excludedPatterns, branch.referenceName) !== null) {
    return { referenceName: branch.referenceName, flag: 'Exclue', tone: 'neutral' };
  }

  return matchPattern(policy.protectedPatterns, branch.referenceName) === null
    ? null
    : { referenceName: branch.referenceName, flag: 'Protégée', tone: 'brand' };
}

function formatDelta(delta: number): string {
  if (delta === 0) {
    return 'inchangé';
  }

  return `${delta > 0 ? '+' : '−'}${Math.abs(delta)} vs politique enregistrée`;
}
