import { Observable, map, of, switchMap } from 'rxjs';
import { BranchSnapshotResponse, PolicySnapshot, SnapshotPageResponse } from '../api/api.models';

/** The maximum the API accepts; beyond it, the request is rejected. */
export const snapshotPageSize = 200;

/** Guard rail: 10,000 branches are far more than enough, and bound the round trips. */
export const maximumSnapshotPages = 50;

export type FetchSnapshotPage = (cursor: string | null) => Observable<SnapshotPageResponse>;

/** A whole snapshot, as the views handle it: filters, sorts and counters are local. */
export interface LoadedSnapshot {
  readonly analysisId: string;
  readonly capturedAtUtc: string;
  readonly referenceName: string;
  readonly policy: PolicySnapshot;
  readonly branches: readonly BranchSnapshotResponse[];
  readonly isTruncated: boolean;
}

/**
 * Follows the cursors to the last page. Loading the whole analysis once then allows
 * filtering, sorting and counting with no further network call.
 */
export function loadEntireSnapshot(fetchPage: FetchSnapshotPage): Observable<LoadedSnapshot> {
  return collectPages(fetchPage, null, maximumSnapshotPages).pipe(map(assemble));
}

function collectPages(
  fetchPage: FetchSnapshotPage,
  cursor: string | null,
  remainingPages: number,
): Observable<readonly SnapshotPageResponse[]> {
  return fetchPage(cursor).pipe(
    switchMap((page) =>
      page.nextCursor === null || remainingPages <= 1
        ? of([page])
        : collectPages(fetchPage, page.nextCursor, remainingPages - 1).pipe(
            map((rest) => [page, ...rest]),
          ),
    ),
  );
}

function assemble(pages: readonly SnapshotPageResponse[]): LoadedSnapshot {
  const first = pages[0];
  const last = pages[pages.length - 1];
  return {
    analysisId: first.analysisId,
    capturedAtUtc: first.capturedAtUtc,
    referenceName: first.referenceName,
    policy: first.policy,
    branches: pages.flatMap((page) => page.items),
    isTruncated: last.nextCursor !== null,
  };
}
