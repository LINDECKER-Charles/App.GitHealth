import { BranchSnapshotResponse, PolicySnapshot } from '../api/api.models';
import {
  appliedThresholds,
  matchPattern,
  parsePatterns,
  projectActivity,
  projectRecommendation,
} from './branch-policy';

const day = 86_400_000;

const policy: PolicySnapshot = {
  activeUntilDays: 30,
  inactiveAfterDays: 90,
  protectedPatterns: [],
  excludedPatterns: [],
};

function branch(overrides: Partial<BranchSnapshotResponse> = {}): BranchSnapshotResponse {
  return {
    id: 'b1',
    referenceName: 'refs/heads/feature/export-csv',
    commitId: 'abc',
    aheadCount: 3,
    behindCount: 4,
    relationship: 'CommonAncestor',
    lastActivityAtUtc: new Date(Date.now() - 5 * day).toISOString(),
    tipAuthor: 'Ada',
    topology: 'Diverged',
    activity: 'Active',
    recommendation: 'Review',
    reason: '',
    isProtected: false,
    isExcluded: false,
    topContributor: null,
    ...overrides,
  };
}

describe('matchPattern', () => {
  it('accepts the * wildcard across several segments', () => {
    expect(matchPattern(['refs/*'], 'refs/remotes/origin/main')).toBe('refs/*');
  });

  it('accepts the ? wildcard on a single character', () => {
    expect(matchPattern(['refs/heads/v?'], 'refs/heads/v2')).toBe('refs/heads/v?');
    expect(matchPattern(['refs/heads/v?'], 'refs/heads/v20')).toBeNull();
  });

  it('anchors the pattern on the whole reference', () => {
    expect(matchPattern(['refs/heads/main'], 'refs/heads/maintenance')).toBeNull();
  });

  it('returns the first pattern that matches', () => {
    expect(matchPattern(['refs/tags/*', 'refs/heads/*'], 'refs/heads/main')).toBe('refs/heads/*');
  });
});

describe('projectActivity', () => {
  it('classifies below the active threshold', () => {
    expect(projectActivity(branch(), policy)).toBe('Active');
  });

  it('classifies between the two thresholds', () => {
    const aging = branch({ lastActivityAtUtc: new Date(Date.now() - 45 * day).toISOString() });
    expect(projectActivity(aging, policy)).toBe('Aging');
  });

  it('classifies beyond the inactive threshold', () => {
    const inactive = branch({ lastActivityAtUtc: new Date(Date.now() - 200 * day).toISOString() });
    expect(projectActivity(inactive, policy)).toBe('Inactive');
  });

  it('returns Unknown with no tip date', () => {
    expect(projectActivity(branch({ lastActivityAtUtc: null }), policy)).toBe('Unknown');
  });
});

describe('projectRecommendation', () => {
  it('removes a protected branch from the recommendations', () => {
    const guarded = { ...policy, protectedPatterns: ['refs/heads/feature/*'] };
    expect(projectRecommendation(branch(), guarded)).toBe('Excluded');
  });

  it('removes an excluded branch from the recommendations', () => {
    const hidden = { ...policy, excludedPatterns: ['refs/heads/feature/*'] };
    expect(projectRecommendation(branch(), hidden)).toBe('Excluded');
  });

  it('offers cleanup for a merged and inactive branch', () => {
    const merged = branch({
      topology: 'Merged',
      lastActivityAtUtc: new Date(Date.now() - 200 * day).toISOString(),
    });
    expect(projectRecommendation(merged, policy)).toBe('CleanupCandidate');
  });

  it('asks for a review on a recent divergence', () => {
    expect(projectRecommendation(branch(), policy)).toBe('Review');
  });

  it('asks for a review with no common ancestor', () => {
    expect(projectRecommendation(branch({ topology: 'Unrelated' }), policy)).toBe('Review');
  });

  it('keeps a branch that is ahead and active', () => {
    expect(projectRecommendation(branch({ topology: 'Ahead' }), policy)).toBe('Keep');
  });
});

/**
 * This table must stay identical to `MergedBranchScaleTests` on the server side: it is the
 * only guard against a drift between the rule and its mirror in the interface.
 */
describe('shortened scale for branches with no own commits', () => {
  const aged = (days: number, topology: 'Merged' | 'Synchronized' | 'Diverged') =>
    branch({ topology, lastActivityAtUtc: new Date(Date.now() - days * day).toISOString() });

  it.each([
    [3, 'Active', 'Merged'],
    [7, 'Active', 'Merged'],
    [8, 'Aging', 'Review'],
    [30, 'Aging', 'Review'],
    [31, 'Inactive', 'CleanupCandidate'],
  ] as const)('merged %i d ago: %s → %s', (days, activity, recommendation) => {
    const merged = aged(days, 'Merged');
    expect(projectActivity(merged, policy)).toBe(activity);
    expect(projectRecommendation(merged, policy)).toBe(recommendation);
  });

  it('treats a branch on the same commit as the baseline the same way', () => {
    expect(projectRecommendation(aged(60, 'Synchronized'), policy)).toBe('CleanupCandidate');
    expect(projectRecommendation(aged(2, 'Synchronized'), policy)).toBe('Merged');
  });

  it('never recommends "Keep" without an own commit', () => {
    for (const days of [1, 14, 120]) {
      expect(projectRecommendation(aged(days, 'Merged'), policy)).not.toBe('Keep');
    }
  });

  it('leaves the project scale to branches that carry their own commits', () => {
    const diverged = aged(45, 'Diverged');
    expect(projectActivity(diverged, policy)).toBe('Aging');
    expect(projectRecommendation(diverged, policy)).toBe('Review');
  });

  it('never lengthens a project scale that is already shorter', () => {
    const tight = { ...policy, activeUntilDays: 3, inactiveAfterDays: 10 };
    expect(appliedThresholds('Merged', tight)).toEqual({
      activeUntilDays: 3,
      inactiveAfterDays: 10,
      isReduced: false,
    });
  });

  it('flags the shortened scale so the card can explain it', () => {
    expect(appliedThresholds('Merged', policy)).toEqual({
      activeUntilDays: 7,
      inactiveAfterDays: 30,
      isReduced: true,
    });
    expect(appliedThresholds('Diverged', policy).isReduced).toBe(false);
  });
});

describe('parsePatterns', () => {
  it('splits, trims and deduplicates', () => {
    expect(parsePatterns('  refs/heads/main \n\n refs/heads/main \r\n refs/tags/*')).toEqual([
      'refs/heads/main',
      'refs/tags/*',
    ]);
  });
});
