import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { PolicySnapshot } from '../../../core/api/api.models';
import {
  displayReference,
  recommendationIcons,
  recommendationLabels,
  recommendationTones,
  relationshipLabels,
  relativeAge,
  topologyLabels,
  topologyTones,
} from '../../../core/branches/branch-labels';
import { matchPattern } from '../../../core/branches/branch-policy';
import { pluralMessage } from '../../../core/i18n/plural-message';
import { DsBadge } from '../../../ui/core/ds-badge';
import { DsSpinner } from '../../../ui/core/ds-spinner';
import { DsStatusDot } from '../../../ui/core/ds-status-dot';
import { SelectOption } from '../../../ui/forms/ds-select';
import { DsEmptyState } from '../../../ui/surfaces/ds-empty-state';
import { DsKeyValueList, KeyValueItem } from '../../../ui/surfaces/ds-key-value-list';
import { DsSegmentedControl } from '../../../ui/surfaces/ds-segmented-control';
import { IconName, Tone } from '../../../ui/icon-name';
import { CaptureStore } from '../../project/capture-store';
import { TopologyFilter, isVisibleUnder } from './topology-layout';
import { TopologyCounts, TopologyMap, TopologyNode, buildTopologyMap } from './topology-map';

interface LegendEntry {
  readonly label: string;
  readonly tone: Tone;
}

interface TopologyCard {
  readonly name: string;
  readonly isPinned: boolean;
  readonly topologyTone: Tone;
  readonly topologyLabel: string;
  readonly recommendationTone: Tone;
  readonly recommendationIcon: IconName;
  readonly recommendationLabel: string;
  readonly details: readonly KeyValueItem[];
  readonly reason: string;
  readonly hint: string;
}

const filterOptions: readonly SelectOption[] = [
  { value: 'all', label: $localize`:@@topology.filter.all:All` },
  { value: 'open', label: $localize`:@@topology.filter.open:Open` },
  { value: 'merged', label: $localize`:@@topology.filter.merged:Merged` },
];

/** The tones come from `topologyTones`: the map and the Diagnostic cannot diverge. */
const legend: readonly LegendEntry[] = [
  { label: $localize`:@@topology.legend.ahead:ahead`, tone: topologyTones.Ahead },
  { label: $localize`:@@topology.legend.diverged:diverged`, tone: topologyTones.Diverged },
  { label: $localize`:@@topology.legend.merged:merged`, tone: topologyTones.Merged },
  { label: $localize`:@@topology.legend.synchronized:in sync`, tone: topologyTones.Synchronized },
  { label: $localize`:@@topology.legend.unrelated:no merge base`, tone: topologyTones.Unrelated },
];

const legendDotSize = 7;
const haloRadius = 12;
const headRadius = 7;
const tipRadius = 5;
const junctionRadius = 3;
const spinnerSize = 20;
const shortShaLength = 8;

const pinnedHint = $localize`:@@topology.hint.pinned:Click the branch again to release the card.`;
const unpinnedHint = $localize`:@@topology.hint.unpinned:Click the branch to pin the card.`;
const unknownAuthor = $localize`:@@topology.detail.unknownAuthor:unknown author`;

/** Map of the repository: the trunk is the baseline, each shape encodes ahead and behind. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsBadge, DsEmptyState, DsKeyValueList, DsSegmentedControl, DsSpinner, DsStatusDot],
  selector: 'app-topology-view',
  styleUrls: ['../visualisation-card.scss', './topology-view.scss'],
  templateUrl: './topology-view.html',
})
export class TopologyView {
  protected readonly captures = inject(CaptureStore);
  protected readonly filterOptions = filterOptions;
  protected readonly legend = legend;
  protected readonly legendDotSize = legendDotSize;
  protected readonly haloRadius = haloRadius;
  protected readonly headRadius = headRadius;
  protected readonly tipRadius = tipRadius;
  protected readonly junctionRadius = junctionRadius;
  protected readonly spinnerSize = spinnerSize;

  /** Focus held by reference name: a replayed analysis renumbers the rows of the snapshot. */
  private readonly hoveredId = signal<string | null>(null);
  protected readonly pinnedId = signal<string | null>(null);
  protected readonly filter = signal<TopologyFilter>('all');

  /** The pin always beats hover: a card being read never slips away. */
  private readonly focusedId = computed(() => this.pinnedId() ?? this.hoveredId());

  private readonly branches = computed(() => this.captures.snapshot()?.branches ?? []);

  protected readonly map = computed<TopologyMap | null>(() => {
    const snapshot = this.captures.snapshot();
    if (snapshot === null) {
      return null;
    }

    return buildTopologyMap({
      branches: snapshot.branches,
      filter: this.filter(),
      focusedId: this.focusedId(),
    });
  });

  protected readonly referenceLabel = computed(() =>
    displayReference(this.captures.snapshot()?.referenceName ?? ''),
  );

  protected readonly referenceNote = computed(() => {
    const analysisId = this.captures.snapshot()?.analysisId ?? '';
    const shortId = analysisId.slice(0, shortShaLength);
    return $localize`:@@topology.head.note:baseline · analysis ${shortId}:analysisId:`;
  });

  /** Describes the whole snapshot, not the active filter: it speaks of the repository. */
  protected readonly overview = computed(() => {
    const counts = this.map()?.counts;
    return counts === undefined ? '' : overviewMessage(counts);
  });

  protected readonly summary = computed(
    () => $localize`:@@topology.summary:Topology map · ${this.overview()}:overview:`,
  );

  private readonly focusedNode = computed(() => {
    const focused = this.focusedId();
    return this.map()?.nodes.find((node) => node.id === focused) ?? null;
  });

  protected readonly card = computed<TopologyCard | null>(() => {
    const node = this.focusedNode();
    const policy = this.captures.snapshot()?.policy;
    if (node === null || policy === undefined) {
      return null;
    }

    const branch = node.branch;
    const isPinned = this.pinnedId() === branch.referenceName;
    return {
      name: displayReference(branch.referenceName),
      isPinned,
      topologyTone: topologyTones[branch.topology],
      topologyLabel: topologyLabels[branch.topology],
      recommendationTone: recommendationTones[branch.recommendation],
      recommendationIcon: recommendationIcons[branch.recommendation],
      recommendationLabel: recommendationLabels[branch.recommendation],
      details: detailsOf(node, policy),
      reason: branch.reason,
      hint: isPinned ? pinnedHint : unpinnedHint,
    };
  });

  protected hover(id: string | null): void {
    this.hoveredId.set(id);
  }

  protected togglePin(id: string): void {
    this.pinnedId.update((current) => (current === id ? null : id));
  }

  protected pinFromKeyboard(event: Event, id: string): void {
    event.preventDefault();
    this.togglePin(id);
  }

  protected clearPin(): void {
    this.pinnedId.set(null);
  }

  /** Changing the filter rebuilds the map: a focus that became invisible has to drop. */
  protected changeFilter(value: string): void {
    const filter = value as TopologyFilter;
    this.filter.set(filter);
    this.hoveredId.set(null);
    this.pinnedId.update((id) => this.keepIfVisible(id, filter));
  }

  private keepIfVisible(id: string | null, filter: TopologyFilter): string | null {
    const branch = this.branches().find((item) => item.referenceName === id);
    return branch !== undefined && isVisibleUnder(filter, branch.topology) ? id : null;
  }
}

/** One whole sentence per plural category: the counts are placed by the translation, never glued. */
function overviewMessage(counts: TopologyCounts): string {
  const { total, open, merged, synchronized, unrelated } = counts;
  return pluralMessage(total, {
    one: $localize`:@@topology.overview.one:${total}:total: branch against the baseline: ${open}:open: open above the trunk, ${merged}:merged: merged as bridges, ${synchronized}:synchronized: in sync at the same commit, ${unrelated}:unrelated: isolated with no common ancestor.`,
    other: $localize`:@@topology.overview.many:${total}:total: branches against the baseline: ${open}:open: open above the trunk, ${merged}:merged: merged as bridges, ${synchronized}:synchronized: in sync at the same commit, ${unrelated}:unrelated: isolated with no common ancestor.`,
  });
}

function detailsOf(node: TopologyNode, policy: PolicySnapshot): readonly KeyValueItem[] {
  const branch = node.branch;
  const author = branch.tipAuthor ?? unknownAuthor;
  const age = relativeAge(branch.lastActivityAtUtc);
  return [
    {
      label: $localize`:@@topology.detail.difference:difference`,
      value: $localize`:@@topology.detail.differenceValue:${node.gap}:gap: commits`,
    },
    {
      label: $localize`:@@topology.detail.lastCommit:last commit`,
      value: $localize`:@@topology.detail.lastCommitValue:${age}:age: — ${author}:author:`,
    },
    {
      label: $localize`:@@topology.detail.relationship:relationship`,
      value: relationshipLabels[branch.relationship],
    },
    {
      label: $localize`:@@topology.detail.policy:policy`,
      value: policyLine(policy, branch.referenceName),
    },
  ];
}

/** A pattern beats the thresholds: it is the pattern that decides the fate of the branch. */
function policyLine(policy: PolicySnapshot, referenceName: string): string {
  const protectedBy = matchPattern(policy.protectedPatterns, referenceName);
  if (protectedBy !== null) {
    return $localize`:@@topology.detail.policyProtected:protected pattern ${protectedBy}:pattern:`;
  }

  const excludedBy = matchPattern(policy.excludedPatterns, referenceName);
  if (excludedBy !== null) {
    return $localize`:@@topology.detail.policyExcluded:excluded pattern ${excludedBy}:pattern:`;
  }

  const activeUntilDays = policy.activeUntilDays;
  const inactiveAfterDays = policy.inactiveAfterDays;
  return $localize`:@@topology.detail.policyThresholds:active ≤ ${activeUntilDays}:activeUntilDays: d · inactive > ${inactiveAfterDays}:inactiveAfterDays: d`;
}
