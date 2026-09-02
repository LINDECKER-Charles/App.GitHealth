import { BranchSnapshotResponse } from '../../core/api/api.models';
import {
  authorOptions,
  countByRecommendation,
  defaultFilters,
  filterBranches,
  sortBranches,
} from './dashboard-filters';

const day = 86_400_000;

function branch(
  name: string,
  overrides: Partial<BranchSnapshotResponse> = {},
): BranchSnapshotResponse {
  return {
    id: name,
    referenceName: `refs/heads/${name}`,
    commitId: 'abc',
    aheadCount: 1,
    behindCount: 1,
    relationship: 'CommonAncestor',
    lastActivityAtUtc: new Date(Date.now() - 5 * day).toISOString(),
    tipAuthor: 'Ada Lovelace',
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

const branches: readonly BranchSnapshotResponse[] = [
  branch('feature/export-csv', {
    aheadCount: 4,
    recommendation: 'Review',
    tipAuthor: 'Zoé Martin',
    lastActivityAtUtc: new Date(Date.now() - 3 * day).toISOString(),
  }),
  branch('docs/guide', {
    aheadCount: 1,
    behindCount: 0,
    topology: 'Ahead',
    recommendation: 'Keep',
    lastActivityAtUtc: new Date(Date.now() - 12 * day).toISOString(),
  }),
  branch('archive/2023', {
    recommendation: 'Excluded',
    isExcluded: true,
    activity: 'Inactive',
    tipAuthor: null,
    lastActivityAtUtc: new Date(Date.now() - 400 * day).toISOString(),
  }),
  branch('feature/fusionnee', {
    aheadCount: 0,
    behindCount: 2,
    topology: 'Merged',
    relationship: 'BranchIsAncestorOfReference',
    activity: 'Inactive',
    recommendation: 'CleanupCandidate',
    lastActivityAtUtc: new Date(Date.now() - 200 * day).toISOString(),
  }),
];

describe('filterBranches', () => {
  it('filters nothing by default', () => {
    expect(filterBranches(branches, defaultFilters, 90)).toHaveLength(4);
  });

  it('filters on the recommendation chosen by the tile', () => {
    const filtered = filterBranches(branches, { ...defaultFilters, view: 'Keep' }, 90);
    expect(filtered.map((item) => item.id)).toEqual(['docs/guide']);
  });

  it('searches the short name, without the refs prefix', () => {
    const filtered = filterBranches(branches, { ...defaultFilters, search: 'FEATURE/' }, 90);
    expect(filtered).toHaveLength(2);
  });

  it('crosses topology and activity', () => {
    const filtered = filterBranches(
      branches,
      { ...defaultFilters, topology: 'Merged', activity: 'Inactive' },
      90,
    );
    expect(filtered.map((item) => item.id)).toEqual(['feature/fusionnee']);
  });

  it('filters on the Git relationship', () => {
    const filtered = filterBranches(
      branches,
      { ...defaultFilters, relationship: 'BranchIsAncestorOfReference' },
      90,
    );
    expect(filtered).toHaveLength(1);
  });

  it('filters on the tip author, merged branches included', () => {
    const filtered = filterBranches(branches, { ...defaultFilters, author: 'Ada Lovelace' }, 90);
    expect(filtered.map((item) => item.id)).toEqual(['docs/guide', 'feature/fusionnee']);
  });

  it('crosses the author with another facet', () => {
    const filtered = filterBranches(
      branches,
      { ...defaultFilters, author: 'Ada Lovelace', topology: 'Merged' },
      90,
    );
    expect(filtered.map((item) => item.id)).toEqual(['feature/fusionnee']);
  });

  it('keeps only the branches beyond the inactivity threshold', () => {
    const filtered = filterBranches(branches, { ...defaultFilters, onlyStale: true }, 90);
    expect(filtered.map((item) => item.id).sort()).toEqual(['archive/2023', 'feature/fusionnee']);
  });
});

describe('sortBranches', () => {
  it('sorts by short name without mutating the source', () => {
    const sorted = sortBranches(branches, 'name', 'asc');
    expect(sorted.map((item) => item.id)).toEqual([
      'archive/2023',
      'docs/guide',
      'feature/export-csv',
      'feature/fusionnee',
    ]);
    expect(branches[0].id).toBe('feature/export-csv');
  });

  it('sorts by descending ahead count', () => {
    expect(sortBranches(branches, 'ahead', 'desc')[0].id).toBe('feature/export-csv');
  });

  it('sorts by activity, the most recent first', () => {
    expect(sortBranches(branches, 'activity', 'desc')[0].aheadCount).toBe(4);
  });
});

describe('authorOptions', () => {
  it('lists the distinct authors, sorted, behind the any-author entry', () => {
    expect(authorOptions(branches)).toEqual([
      { value: '', label: 'Any author' },
      { value: 'Ada Lovelace', label: 'Ada Lovelace' },
      { value: 'Zoé Martin', label: 'Zoé Martin' },
    ]);
  });

  it('ignores the branches without a tip author', () => {
    const options = authorOptions([branch('orphan', { tipAuthor: null })]);
    expect(options).toEqual([{ value: '', label: 'Any author' }]);
  });
});

describe('countByRecommendation', () => {
  it('counts the total and each recommendation', () => {
    expect(countByRecommendation(branches)).toEqual({
      all: 4,
      Keep: 1,
      Merged: 0,
      Review: 1,
      CleanupCandidate: 1,
      Excluded: 1,
    });
  });
});
