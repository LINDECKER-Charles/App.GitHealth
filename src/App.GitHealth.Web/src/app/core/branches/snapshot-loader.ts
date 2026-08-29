import { Observable, map, of, switchMap } from 'rxjs';
import { BranchSnapshotResponse, PolicySnapshot, SnapshotPageResponse } from '../api/api.models';

/** Le maximum accepté par l'API ; au-delà elle rejette la requête. */
export const snapshotPageSize = 200;

/** Garde-fou : 10 000 branches suffisent très largement, et bornent les allers-retours. */
export const maximumSnapshotPages = 50;

export type FetchSnapshotPage = (cursor: string | null) => Observable<SnapshotPageResponse>;

/** Un snapshot complet, tel que les vues le manipulent : filtres, tris et compteurs sont locaux. */
export interface LoadedSnapshot {
  readonly analysisId: string;
  readonly capturedAtUtc: string;
  readonly referenceName: string;
  readonly policy: PolicySnapshot;
  readonly branches: readonly BranchSnapshotResponse[];
  readonly isTruncated: boolean;
}

/**
 * Suit les curseurs jusqu'à la dernière page. Charger l'analyse entière une fois
 * permet ensuite de filtrer, trier et compter sans nouvel appel réseau.
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
