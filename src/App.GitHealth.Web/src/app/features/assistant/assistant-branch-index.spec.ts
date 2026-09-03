import { BranchSnapshotResponse } from '../../core/api/api.models';
import { buildBranchIndex, emptyBranchIndex } from './assistant-branch-index';

describe('buildBranchIndex', () => {
  /**
   * The tools spell branch names in full; an answer written for a reader usually shortens
   * them. Either spelling has to open the row, or half the links in an answer go nowhere.
   */
  it('indexes a branch under both the full reference and the short name', () => {
    const index = buildBranchIndex([branch('refs/heads/feature/panel', 'row-1')]);

    expect(index.rows.get('refs/heads/feature/panel')).toBe('row-1');
    expect(index.rows.get('feature/panel')).toBe('row-1');
    expect(index.names).toContain('feature/panel');
  });

  it('shortens a remote branch to the name it is shown under', () => {
    const index = buildBranchIndex([branch('refs/remotes/origin/spike/webkit', 'row-2')]);

    expect(index.rows.get('origin/spike/webkit')).toBe('row-2');
  });

  /** A local branch and its remote shorten alike; opening the first beats opening neither. */
  it('keeps the first row to claim a spelling', () => {
    const index = buildBranchIndex([
      branch('refs/heads/main', 'local'),
      branch('refs/remotes/origin/main', 'remote'),
    ]);

    expect(index.rows.get('main')).toBe('local');
    expect(index.rows.get('refs/remotes/origin/main')).toBe('remote');
  });

  it('says nothing about a branch that is not in the capture', () => {
    const index = buildBranchIndex([branch('refs/heads/main', 'row-1')]);

    expect(index.rows.get('refs/heads/dev')).toBeUndefined();
  });

  it('reads as empty before any capture is loaded', () => {
    expect(emptyBranchIndex.names).toEqual([]);
    expect(buildBranchIndex([]).names).toEqual([]);
  });

  function branch(referenceName: string, id: string): BranchSnapshotResponse {
    return {
      id,
      referenceName,
      commitId: 'abcdef0',
      aheadCount: 0,
      behindCount: 0,
      relationship: 'CommonAncestor',
      lastActivityAtUtc: null,
      tipAuthor: null,
      topology: 'Diverged',
      activity: 'Active',
      recommendation: 'Keep',
      reason: 'Own commits',
      isProtected: false,
      isExcluded: false,
      topContributor: null,
    };
  }
});
