import { SnapshotDetailResponse } from '../../core/api/api.models';
import {
  ageInDays,
  displayReference,
  recommendationLabels,
} from '../../core/branches/branch-labels';
import { appliedThresholds, matchPattern } from '../../core/branches/branch-policy';
import { IconName } from '../../ui/icon-name';

export type PatternKind = 'exclusion' | 'protected';

export interface TraceLine {
  readonly icon: IconName;
  readonly text: string;
  readonly rule: string;
}

const topologyRule = 'git merge-base --is-ancestor + git rev-list --count';
const activityRule = $localize`:@@branch.rule.activity:date of the commit the branch points at`;
const reducedScaleRule = $localize`:@@branch.rule.reducedScale:shortened scale: the branch has no own commits, everything is already in the baseline`;
const conclusionRule = $localize`:@@branch.rule.conclusion:rule applied at read time`;

/** Sentences per pattern kind, whole: a translated message is never assembled from fragments. */
const patternTexts: Readonly<Record<PatternKind, PatternTexts>> = {
  exclusion: {
    none: $localize`:@@branch.trace.exclusionNone:No exclusion pattern matches`,
    matched: (hit) => $localize`:@@branch.trace.excludedBy:Excluded by "${hit}:pattern:"`,
    rule: $localize`:@@branch.rule.exclusion:exclusion pattern → removed from action recommendations`,
  },
  protected: {
    none: $localize`:@@branch.trace.protectedNone:No protected pattern matches`,
    matched: (hit) => $localize`:@@branch.trace.protectedBy:Protected by "${hit}:pattern:"`,
    rule: $localize`:@@branch.rule.protected:protected pattern → removed from action recommendations`,
  },
};

interface PatternTexts {
  readonly none: string;
  readonly matched: (hit: string) => string;
  readonly rule: string;
}

/**
 * Rebuilds, from the captured facts alone, the path that leads to the
 * recommendation the API returned. Nothing is recomputed: everything is explained.
 */
export function buildTrace(detail: SnapshotDetailResponse): readonly TraceLine[] {
  const { snapshot, policy } = detail;
  const reference = displayReference(detail.referenceName);
  const conclusion = recommendationLabels[snapshot.recommendation].toLowerCase();
  return [
    patternLine(policy.excludedPatterns, snapshot.referenceName, snapshot.isExcluded, 'exclusion'),
    patternLine(
      policy.protectedPatterns,
      snapshot.referenceName,
      snapshot.isProtected,
      'protected',
    ),
    { icon: 'circle-check', text: topologyText(detail, reference), rule: topologyRule },
    { icon: 'circle-check', text: activityText(detail), rule: activityRuleOf(detail) },
    {
      icon: 'arrow-right',
      text: $localize`:@@branch.trace.conclusion:Conclusion: ${conclusion}:label:`,
      rule: conclusionRule,
    },
  ];
}

function patternLine(
  patterns: readonly string[],
  referenceName: string,
  isMatched: boolean,
  kind: PatternKind,
): TraceLine {
  const texts = patternTexts[kind];
  if (!isMatched) {
    return { icon: 'minus', text: texts.none, rule: evaluatedRule(patterns.length) };
  }

  const hit = matchPattern(patterns, referenceName) ?? referenceName;
  return { icon: 'triangle-alert', text: texts.matched(hit), rule: texts.rule };
}

function evaluatedRule(count: number): string {
  return count === 1
    ? $localize`:@@branch.rule.onePatternEvaluated:1 pattern evaluated`
    : $localize`:@@branch.rule.patternsEvaluated:${count}:count: patterns evaluated`;
}

function topologyText(detail: SnapshotDetailResponse, reference: string): string {
  const { aheadCount, behindCount, topology } = detail.snapshot;
  switch (topology) {
    case 'Merged':
      return $localize`:@@branch.trace.merged:Merged: 0 commits ahead of ${reference}:reference:`;
    case 'Ahead':
      return aheadText(aheadCount);
    case 'Synchronized':
      return $localize`:@@branch.trace.inSync:Same commit as ${reference}:reference:`;
    case 'Unrelated':
      return $localize`:@@branch.trace.unrelated:No common ancestor found`;
    default:
      return $localize`:@@branch.trace.diverged:Diverged: +${aheadCount}:ahead: / −${behindCount}:behind:`;
  }
}

function aheadText(aheadCount: number): string {
  return aheadCount === 1
    ? $localize`:@@branch.trace.aheadOne:1 commit ahead, 0 behind`
    : $localize`:@@branch.trace.aheadMany:${aheadCount}:ahead: commits ahead, 0 behind`;
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
    return $localize`:@@branch.trace.activityUnknown:Unknown activity: Git exposes no date for this tip`;
  }

  switch (detail.snapshot.activity) {
    case 'Inactive':
      return $localize`:@@branch.trace.inactive:Inactive: ${days}:days: d > ${inactiveAfterDays}:threshold: d threshold`;
    case 'Aging':
      return $localize`:@@branch.trace.ageing:Ageing: ${activeUntilDays}:active: d < ${days}:days: d ≤ ${inactiveAfterDays}:inactive: d`;
    case 'Active':
      return $localize`:@@branch.trace.active:Active: ${days}:days: d ≤ ${activeUntilDays}:threshold: d threshold`;
    default:
      return $localize`:@@branch.trace.lastCommit:Last commit ${days}:days: d ago`;
  }
}
