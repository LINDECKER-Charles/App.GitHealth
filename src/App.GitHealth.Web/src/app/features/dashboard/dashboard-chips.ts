import { activityLabels, topologyLabels } from '../../core/branches/branch-labels';
import { relationshipOptions, BranchFilters } from './dashboard-filters';

/** Filtre actif, avec la retouche à appliquer pour le retirer. */
export interface DashboardChip {
  readonly label: string;
  readonly patch: Partial<BranchFilters>;
}

export function buildChips(
  filters: BranchFilters,
  inactiveAfterDays: number,
): readonly DashboardChip[] {
  const chips: DashboardChip[] = [];
  if (filters.topology !== '') {
    chips.push({
      label: `Topologie : ${topologyLabels[filters.topology]}`,
      patch: { topology: '' },
    });
  }

  if (filters.activity !== '') {
    chips.push({
      label: `Activité : ${activityLabels[filters.activity]}`,
      patch: { activity: '' },
    });
  }

  if (filters.search.trim().length > 0) {
    chips.push({ label: `« ${filters.search.trim()} »`, patch: { search: '' } });
  }

  if (filters.relationship !== '') {
    chips.push({ label: `Relation : ${relationshipLabel(filters)}`, patch: { relationship: '' } });
  }

  if (filters.onlyStale) {
    chips.push({
      label: `Sans activité > ${inactiveAfterDays} j`,
      patch: { onlyStale: false },
    });
  }

  return chips;
}

function relationshipLabel(filters: BranchFilters): string {
  return (
    relationshipOptions.find((option) => option.value === filters.relationship)?.label ??
    filters.relationship
  );
}
