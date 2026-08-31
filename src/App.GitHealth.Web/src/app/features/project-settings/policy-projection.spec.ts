import { BranchSnapshotResponse, PolicySnapshot } from '../../core/api/api.models';
import { projectMatches, projectStats } from './policy-projection';

const day = 86_400_000;

const savedPolicy: PolicySnapshot = {
  activeUntilDays: 30,
  inactiveAfterDays: 90,
  protectedPatterns: [],
  excludedPatterns: [],
};

function branch(
  name: string,
  overrides: Partial<BranchSnapshotResponse> = {},
): BranchSnapshotResponse {
  return {
    id: name,
    referenceName: `refs/heads/${name}`,
    commitId: 'abc',
    aheadCount: 2,
    behindCount: 0,
    relationship: 'CommonAncestor',
    lastActivityAtUtc: new Date(Date.now() - 3 * day).toISOString(),
    tipAuthor: 'Ada',
    topology: 'Ahead',
    activity: 'Active',
    recommendation: 'Keep',
    reason: '',
    isProtected: false,
    isExcluded: false,
    ...overrides,
  };
}

const branches = [branch('docs/guide'), branch('feature/export-csv')];

describe('projectStats', () => {
  it('reports "unchanged" when the edited policy equals the saved policy', () => {
    const stats = projectStats(branches, savedPolicy);
    expect(stats.map((stat) => stat.label)).toEqual([
      'Keep',
      'Done',
      'Review',
      'Cleanup possible',
      'Excluded',
    ]);
    expect(stats.every((stat) => stat.delta === 'unchanged')).toBe(true);
    expect(stats[0].count).toBe(2);
  });

  it('quantifies the difference a new pattern introduces', () => {
    const stats = projectStats(branches, {
      ...savedPolicy,
      excludedPatterns: ['refs/heads/feature/*'],
    });
    const keep = stats.find((stat) => stat.label === 'Keep');
    const excluded = stats.find((stat) => stat.label === 'Excluded');
    expect(keep?.count).toBe(1);
    expect(keep?.delta).toBe('−1 vs saved policy');
    expect(excluded?.count).toBe(1);
    expect(excluded?.delta).toBe('+1 vs saved policy');
  });
});

describe('projectMatches', () => {
  it('lists only the branches captured by a pattern', () => {
    const matches = projectMatches(branches, {
      ...savedPolicy,
      protectedPatterns: ['refs/heads/docs/*'],
    });
    expect(matches).toEqual([
      { referenceName: 'refs/heads/docs/guide', flag: 'Protected', tone: 'brand' },
    ]);
  });

  it('gives priority to exclusion over protection', () => {
    const matches = projectMatches(branches, {
      ...savedPolicy,
      protectedPatterns: ['refs/heads/*'],
      excludedPatterns: ['refs/heads/docs/*'],
    });
    expect(matches[0].flag).toBe('Excluded');
    expect(matches[1].flag).toBe('Protected');
  });
});
