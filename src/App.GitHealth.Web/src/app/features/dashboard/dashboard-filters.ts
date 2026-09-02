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
import { sourceLocale } from '../../core/i18n/locale';
import { SelectOption } from '../../ui/forms/ds-select';

export type RecommendationView = RecommendationKind | 'all';

export interface BranchFilters {
  readonly view: RecommendationView;
  readonly search: string;
  readonly topology: BranchTopology | '';
  readonly activity: ActivityStatus | '';
  readonly relationship: BranchRelationship | '';
  /** Tip author to keep, empty for any. */
  readonly author: string;
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
  author: '',
  onlyStale: false,
  sort: 'activity',
  direction: 'desc',
};

export const topologyOptions: readonly SelectOption[] = [
  { value: '', label: $localize`:@@dashboard.topology.any:Any topology` },
  { value: 'Synchronized', label: $localize`:@@dashboard.topology.inSync:In sync` },
  { value: 'Ahead', label: $localize`:@@dashboard.topology.ahead:Ahead` },
  { value: 'Merged', label: $localize`:@@dashboard.topology.merged:Merged` },
  { value: 'Diverged', label: $localize`:@@dashboard.topology.diverged:Diverged` },
  { value: 'Unrelated', label: $localize`:@@dashboard.topology.noMergeBase:No merge base` },
];

export const activityOptions: readonly SelectOption[] = [
  { value: '', label: $localize`:@@dashboard.activity.any:Any activity` },
  { value: 'Active', label: $localize`:@@dashboard.activity.active:Active` },
  { value: 'Aging', label: $localize`:@@dashboard.activity.ageing:Ageing` },
  { value: 'Inactive', label: $localize`:@@dashboard.activity.inactive:Inactive` },
  { value: 'Unknown', label: $localize`:@@dashboard.activity.unknown:Unknown` },
];

export const relationshipOptions: readonly SelectOption[] = [
  { value: '', label: $localize`:@@dashboard.relationship.any:Any relationship` },
  { value: 'SameCommit', label: $localize`:@@dashboard.relationship.sameCommit:Same commit` },
  { value: 'CommonAncestor', label: $localize`:@@dashboard.relationship.ancestor:Common ancestor` },
  {
    value: 'BranchIsAncestorOfReference',
    label: $localize`:@@dashboard.relationship.merged:Merged into the baseline`,
  },
  {
    value: 'NoCommonAncestor',
    label: $localize`:@@dashboard.relationship.noAncestor:No common ancestor`,
  },
];

export const sortOptions: readonly SelectOption[] = [
  { value: 'activity', label: $localize`:@@dashboard.sort.activity:Last activity` },
  { value: 'name', label: $localize`:@@dashboard.sort.name:Name` },
  { value: 'ahead', label: $localize`:@@dashboard.sort.ahead:Ahead` },
  { value: 'behind', label: $localize`:@@dashboard.sort.behind:Behind` },
];

/** Names are sorted with the app locale, never with the host's default collation. */
const nameCollator = new Intl.Collator(sourceLocale);

/**
 * Facet built from the loaded snapshot: the tip author is the only author present on every
 * branch, including the merged ones a top contributor never covers.
 */
export function authorOptions(
  branches: readonly BranchSnapshotResponse[],
): readonly SelectOption[] {
  const authors = new Set<string>();
  for (const branch of branches) {
    if (branch.tipAuthor !== null && branch.tipAuthor !== '') {
      authors.add(branch.tipAuthor);
    }
  }

  const sorted = [...authors].sort((left, right) => nameCollator.compare(left, right));
  return [
    { value: '', label: $localize`:@@dashboard.author.any:Any author` },
    ...sorted.map((author) => ({ value: author, label: author })),
  ];
}

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

  if (filters.author !== '' && branch.tipAuthor !== filters.author) {
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
      return nameCollator.compare(
        displayReference(left.referenceName),
        displayReference(right.referenceName),
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
