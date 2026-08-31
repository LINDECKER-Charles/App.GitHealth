import { SnapshotDetailResponse } from '../../core/api/api.models';
import { buildTrace } from './branch-trace';

const day = 86_400_000;

function detail(overrides: Partial<SnapshotDetailResponse> = {}): SnapshotDetailResponse {
  return {
    analysisId: 'a1',
    referenceName: 'refs/heads/main',
    referenceCommit: '6f9f137c08ee',
    capturedAtUtc: '2026-08-29T11:21:16Z',
    snapshot: {
      id: 'b1',
      referenceName: 'refs/heads/feature/export-csv',
      commitId: '1f484960946c',
      aheadCount: 4,
      behindCount: 3,
      relationship: 'CommonAncestor',
      lastActivityAtUtc: new Date(Date.now() - 3 * day).toISOString(),
      tipAuthor: 'Camille Rousseau',
      topology: 'Diverged',
      activity: 'Active',
      recommendation: 'Review',
      reason: 'Diverged history to review',
      isProtected: false,
      isExcluded: false,
    },
    contributors: [],
    attributionStatus: 'Available',
    mailmapApplied: true,
    policy: {
      activeUntilDays: 30,
      inactiveAfterDays: 90,
      excludedPatterns: ['refs/heads/archive/*'],
      protectedPatterns: ['refs/heads/main', 'refs/heads/release/*'],
    },
    ...overrides,
  };
}

describe('buildTrace', () => {
  it('states the evaluated patterns, the topology, the activity and the conclusion', () => {
    const lines = buildTrace(detail());
    expect(lines).toHaveLength(5);
    expect(lines[0].text).toBe('No exclusion pattern matches');
    expect(lines[0].rule).toBe('1 pattern evaluated');
    expect(lines[1].rule).toBe('2 patterns evaluated');
    expect(lines[2].text).toBe('Diverged: +4 / −3');
    expect(lines[2].rule).toBe('git merge-base --is-ancestor + git rev-list --count');
    expect(lines[3].text).toBe('Active: 3 d ≤ 30 d threshold');
    expect(lines[4].text).toBe('Conclusion: review');
  });

  it('names the protected pattern that captures the branch', () => {
    const lines = buildTrace(
      detail({
        snapshot: {
          ...detail().snapshot,
          referenceName: 'refs/heads/release/2026.08',
          isProtected: true,
        },
      }),
    );
    expect(lines[1].text).toBe('Protected by "refs/heads/release/*"');
    expect(lines[1].rule).toContain('removed from action recommendations');
  });

  it('names the exclusion pattern that captures the branch', () => {
    const lines = buildTrace(
      detail({
        snapshot: {
          ...detail().snapshot,
          referenceName: 'refs/heads/archive/2023-legacy',
          isExcluded: true,
        },
      }),
    );
    expect(lines[0].text).toBe('Excluded by "refs/heads/archive/*"');
  });

  it('describes a branch merged into the baseline', () => {
    const lines = buildTrace(
      detail({ snapshot: { ...detail().snapshot, topology: 'Merged', aheadCount: 0 } }),
    );
    expect(lines[2].text).toBe('Merged: 0 commits ahead of main');
  });

  it('accepts the absence of a tip date', () => {
    const lines = buildTrace(
      detail({
        snapshot: { ...detail().snapshot, lastActivityAtUtc: null, activity: 'Unknown' },
      }),
    );
    expect(lines[3].text).toContain('Unknown activity');
  });
});
