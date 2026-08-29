import { SnapshotDetailResponse } from '../../core/api/api.models';
import {
  ageInDays,
  displayReference,
  recommendationLabels,
} from '../../core/branches/branch-labels';
import { appliedThresholds, matchPattern } from '../../core/branches/branch-policy';
import { IconName } from '../../ui/icon-name';

export interface TraceLine {
  readonly icon: IconName;
  readonly text: string;
  readonly rule: string;
}

const topologyRule = 'git merge-base --is-ancestor + git rev-list --count';
const activityRule = 'date du commit pointé par la branche';
const reducedScaleRule =
  'échelle réduite : la branche n’a aucun commit propre, tout est déjà dans la référence';

/**
 * Reconstitue, à partir des seuls faits capturés, le chemin qui mène à la
 * recommandation renvoyée par l'API. Rien n'est recalculé : tout est expliqué.
 */
export function buildTrace(detail: SnapshotDetailResponse): readonly TraceLine[] {
  const { snapshot, policy } = detail;
  const reference = displayReference(detail.referenceName);
  return [
    patternLine(policy.excludedPatterns, snapshot.referenceName, snapshot.isExcluded, 'exclusion'),
    patternLine(policy.protectedPatterns, snapshot.referenceName, snapshot.isProtected, 'protégé'),
    { icon: 'circle-check', text: topologyText(detail, reference), rule: topologyRule },
    { icon: 'circle-check', text: activityText(detail), rule: activityRuleOf(detail) },
    {
      icon: 'arrow-right',
      text: `Conclusion : ${recommendationLabels[snapshot.recommendation].toLowerCase()}`,
      rule: 'règle appliquée au moment de la lecture',
    },
  ];
}

function patternLine(
  patterns: readonly string[],
  referenceName: string,
  isMatched: boolean,
  kind: 'exclusion' | 'protégé',
): TraceLine {
  const noun = kind === 'exclusion' ? 'motif d’exclusion' : 'motif protégé';
  if (!isMatched) {
    return {
      icon: 'minus',
      text: `Aucun ${noun} ne correspond`,
      rule: `${patterns.length} motif${patterns.length > 1 ? 's' : ''} évalué${patterns.length > 1 ? 's' : ''}`,
    };
  }

  const hit = matchPattern(patterns, referenceName) ?? referenceName;
  return {
    icon: 'triangle-alert',
    text: kind === 'exclusion' ? `Exclue par « ${hit} »` : `Protégée par « ${hit} »`,
    rule: `${noun} → retirée des recommandations d’action`,
  };
}

function topologyText(detail: SnapshotDetailResponse, reference: string): string {
  const { aheadCount, behindCount, topology } = detail.snapshot;
  switch (topology) {
    case 'Merged':
      return `Fusionnée : 0 commit en avance sur ${reference}`;
    case 'Ahead':
      return `${aheadCount} commit${aheadCount > 1 ? 's' : ''} en avance, 0 en retard`;
    case 'Synchronized':
      return `Même sommet que ${reference}`;
    case 'Unrelated':
      return 'Aucun ancêtre commun trouvé';
    default:
      return `Divergente : +${aheadCount} / −${behindCount}`;
  }
}

function activityRuleOf(detail: SnapshotDetailResponse): string {
  return appliedThresholds(detail.snapshot.topology, detail.policy).isReduced
    ? reducedScaleRule
    : activityRule;
}

function activityText(detail: SnapshotDetailResponse): string {
  const days = ageInDays(detail.snapshot.lastActivityAtUtc);
  const { activeUntilDays, inactiveAfterDays } = appliedThresholds(
    detail.snapshot.topology,
    detail.policy,
  );
  if (days === null) {
    return 'Activité inconnue : Git n’expose pas de date pour ce sommet';
  }

  switch (detail.snapshot.activity) {
    case 'Inactive':
      return `Inactive : ${days} j > seuil ${inactiveAfterDays} j`;
    case 'Aging':
      return `Vieillissante : ${activeUntilDays} j < ${days} j ≤ ${inactiveAfterDays} j`;
    case 'Active':
      return `Active : ${days} j ≤ seuil ${activeUntilDays} j`;
    default:
      return `Dernier commit il y a ${days} j`;
  }
}
