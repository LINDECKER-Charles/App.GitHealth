import { BranchSnapshotResponse } from '../../core/api/api.models';
import {
  activityTones,
  displayReference,
  recommendationIcons,
  recommendationLabels,
  recommendationTones,
  referenceSource,
  referenceSourceLabels,
  relativeAge,
  topologyLabels,
  topologyTones,
} from '../../core/branches/branch-labels';
import { IconName, Tone } from '../../ui/icon-name';

/** Row ready to display: the template performs no computation at all. */
export interface BranchRow {
  readonly id: string;
  readonly name: string;
  readonly selectLabel: string;
  readonly explainLabel: string;
  readonly subtitle: string;
  readonly isProtected: boolean;
  readonly isExcluded: boolean;
  readonly ahead: string;
  readonly behind: string;
  readonly topologyLabel: string;
  readonly topologyTone: Tone;
  readonly activityTone: Tone;
  readonly age: string;
  readonly recommendationLabel: string;
  readonly recommendationTone: Tone;
  readonly recommendationIcon: IconName;
  readonly isSelected: boolean;
}

export function toRow(branch: BranchSnapshotResponse, isSelected: boolean): BranchRow {
  const source = referenceSourceLabels[referenceSource(branch.referenceName)];
  const name = displayReference(branch.referenceName);
  return {
    id: branch.id,
    name,
    selectLabel: $localize`:@@dashboard.row.selectAria:Select ${name}:name:`,
    explainLabel: $localize`:@@dashboard.row.explainAria:Explain ${name}:name:`,
    subtitle: branch.tipAuthor === null ? source : `${branch.tipAuthor} · ${source}`,
    isProtected: branch.isProtected,
    isExcluded: branch.isExcluded,
    ahead: branch.aheadCount === 0 ? '0' : `+${branch.aheadCount}`,
    behind: branch.behindCount === 0 ? '0' : `−${branch.behindCount}`,
    topologyLabel: topologyLabels[branch.topology],
    topologyTone: topologyTones[branch.topology],
    activityTone: activityTones[branch.activity],
    age: relativeAge(branch.lastActivityAtUtc),
    recommendationLabel: recommendationLabels[branch.recommendation],
    recommendationTone: recommendationTones[branch.recommendation],
    recommendationIcon: recommendationIcons[branch.recommendation],
    isSelected,
  };
}
