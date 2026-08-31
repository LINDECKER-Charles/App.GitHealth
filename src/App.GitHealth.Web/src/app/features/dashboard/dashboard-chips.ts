import { activityLabels, topologyLabels } from '../../core/branches/branch-labels';
import { relationshipOptions, BranchFilters } from './dashboard-filters';

/** Active filter, with the patch to apply in order to remove it. */
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
    const topology = topologyLabels[filters.topology];
    chips.push({
      label: $localize`:@@dashboard.chip.topology:Topology: ${topology}:value:`,
      patch: { topology: '' },
    });
  }

  if (filters.activity !== '') {
    const activity = activityLabels[filters.activity];
    chips.push({
      label: $localize`:@@dashboard.chip.activity:Activity: ${activity}:value:`,
      patch: { activity: '' },
    });
  }

  if (filters.search.trim().length > 0) {
    chips.push({ label: `"${filters.search.trim()}"`, patch: { search: '' } });
  }

  if (filters.relationship !== '') {
    const relationship = relationshipLabel(filters);
    chips.push({
      label: $localize`:@@dashboard.chip.relationship:Relationship: ${relationship}:value:`,
      patch: { relationship: '' },
    });
  }

  if (filters.onlyStale) {
    chips.push({
      label: $localize`:@@dashboard.chip.stale:No activity > ${inactiveAfterDays}:days: d`,
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
