import {
  ActivityStatus,
  BranchRelationship,
  BranchSnapshotResponse,
  BranchTopology,
  RecommendationKind,
  SnapshotSort,
  SortDirection,
} from '../../core/api/api.models';
import { ageInDays, displayReference } from '../../core/branches/branch-labels';
import { SelectOption } from '../../ui/forms/ds-select';

export type RecommendationView = RecommendationKind | 'all';

export interface BranchFilters {
  readonly view: RecommendationView;
  readonly search: string;
  readonly topology: BranchTopology | '';
  readonly activity: ActivityStatus | '';
  readonly relationship: BranchRelationship | '';
  readonly onlyStale: boolean;
  readonly sort: SnapshotSort;
  readonly direction: SortDirection;
}

export const defaultFilters: BranchFilters = {
  view: 'all',
  search: '',
  topology: '',
  activity: '',
  relationship: '',
  onlyStale: false,
  sort: 'activity',
  direction: 'desc',
};

export const topologyOptions: readonly SelectOption[] = [
  { value: '', label: 'Toute topologie' },
  { value: 'Synchronized', label: 'Synchronisées' },
  { value: 'Ahead', label: 'En avance' },
  { value: 'Merged', label: 'Fusionnées' },
  { value: 'Diverged', label: 'Divergentes' },
  { value: 'Unrelated', label: 'Sans base' },
];

export const activityOptions: readonly SelectOption[] = [
  { value: '', label: 'Toute activité' },
  { value: 'Active', label: 'Actives' },
  { value: 'Aging', label: 'Vieillissantes' },
  { value: 'Inactive', label: 'Inactives' },
  { value: 'Unknown', label: 'Inconnues' },
];

export const relationshipOptions: readonly SelectOption[] = [
  { value: '', label: 'Toutes relations' },
  { value: 'SameCommit', label: 'Même sommet' },
  { value: 'CommonAncestor', label: 'Ancêtre commun' },
  { value: 'BranchIsAncestorOfReference', label: 'Fusionnées dans la référence' },
  { value: 'NoCommonAncestor', label: 'Sans base commune' },
];

export const sortOptions: readonly SelectOption[] = [
  { value: 'activity', label: 'Dernière activité' },
  { value: 'name', label: 'Nom' },
  { value: 'ahead', label: 'Avance' },
  { value: 'behind', label: 'Retard' },
];

export function filterBranches(
  branches: readonly BranchSnapshotResponse[],
  filters: BranchFilters,
  inactiveAfterDays: number,
): readonly BranchSnapshotResponse[] {
  const needle = filters.search.trim().toLowerCase();
  return branches.filter((branch) => {
    if (filters.view !== 'all' && branch.recommendation !== filters.view) {
      return false;
    }

    if (
      needle.length > 0 &&
      !displayReference(branch.referenceName).toLowerCase().includes(needle)
    ) {
      return false;
    }

    if (filters.onlyStale && (ageInDays(branch.lastActivityAtUtc) ?? 0) <= inactiveAfterDays) {
      return false;
    }

    return matchesFacets(branch, filters);
  });
}

export function sortBranches(
  branches: readonly BranchSnapshotResponse[],
  sort: SnapshotSort,
  direction: SortDirection,
): readonly BranchSnapshotResponse[] {
  const factor = direction === 'desc' ? -1 : 1;
  return [...branches].sort((left, right) => compare(left, right, sort) * factor);
}

export function countByRecommendation(
  branches: readonly BranchSnapshotResponse[],
): Readonly<Record<RecommendationView, number>> {
  const counts: Record<RecommendationView, number> = {
    all: branches.length,
    Keep: 0,
    Merged: 0,
    Review: 0,
    CleanupCandidate: 0,
    Excluded: 0,
  };
  for (const branch of branches) {
    counts[branch.recommendation] += 1;
  }

  return counts;
}

function matchesFacets(branch: BranchSnapshotResponse, filters: BranchFilters): boolean {
  if (filters.topology !== '' && branch.topology !== filters.topology) {
    return false;
  }

  if (filters.activity !== '' && branch.activity !== filters.activity) {
    return false;
  }

  return filters.relationship === '' || branch.relationship === filters.relationship;
}

function compare(
  left: BranchSnapshotResponse,
  right: BranchSnapshotResponse,
  sort: SnapshotSort,
): number {
  switch (sort) {
    case 'name':
      return displayReference(left.referenceName).localeCompare(
        displayReference(right.referenceName),
        'fr',
      );
    case 'ahead':
      return left.aheadCount - right.aheadCount;
    case 'behind':
      return left.behindCount - right.behindCount;
    default:
      return instant(left.lastActivityAtUtc) - instant(right.lastActivityAtUtc);
  }
}

function instant(value: string | null): number {
  return value === null ? 0 : Date.parse(value);
}
