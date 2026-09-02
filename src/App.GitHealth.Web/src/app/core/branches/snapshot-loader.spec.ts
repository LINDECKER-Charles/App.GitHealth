import { firstValueFrom, of } from 'rxjs';
import { SnapshotPageResponse } from '../api/api.models';
import { loadEntireSnapshot, maximumSnapshotPages } from './snapshot-loader';

function page(index: number, nextCursor: string | null): SnapshotPageResponse {
  return {
    analysisId: 'a1',
    capturedAtUtc: '2026-08-29T08:00:00Z',
    referenceName: 'refs/heads/main',
    policy: {
      activeUntilDays: 30,
      inactiveAfterDays: 90,
      excludedPatterns: [],
      protectedPatterns: [],
    },
    items: [
      {
        id: `b${index}`,
        referenceName: `refs/heads/page-${index}`,
        commitId: 'abc',
        aheadCount: 0,
        behindCount: 0,
        relationship: 'SameCommit',
        lastActivityAtUtc: null,
        tipAuthor: null,
        topology: 'Synchronized',
        activity: 'Unknown',
        recommendation: 'Keep',
        reason: '',
        isProtected: false,
        isExcluded: false,
        topContributor: null,
      },
    ],
    nextCursor,
  };
}

describe('loadEntireSnapshot', () => {
  it('follows the cursors and concatenates the branches', async () => {
    const cursors: (string | null)[] = [];
    const snapshot = await firstValueFrom(
      loadEntireSnapshot((cursor) => {
        cursors.push(cursor);
        if (cursor === null) {
          return of(page(0, 'c1'));
        }

        return of(cursor === 'c1' ? page(1, 'c2') : page(2, null));
      }),
    );

    expect(cursors).toEqual([null, 'c1', 'c2']);
    expect(snapshot.branches.map((branch) => branch.referenceName)).toEqual([
      'refs/heads/page-0',
      'refs/heads/page-1',
      'refs/heads/page-2',
    ]);
    expect(snapshot.isTruncated).toBe(false);
    expect(snapshot.referenceName).toBe('refs/heads/main');
  });

  it('flags a truncated read when the guard rail is reached', async () => {
    let calls = 0;
    const snapshot = await firstValueFrom(
      loadEntireSnapshot(() => {
        calls += 1;
        return of(page(calls, 'always-more'));
      }),
    );

    expect(calls).toBe(maximumSnapshotPages);
    expect(snapshot.isTruncated).toBe(true);
  });
});
