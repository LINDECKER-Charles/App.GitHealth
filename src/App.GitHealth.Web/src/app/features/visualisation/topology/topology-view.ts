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
import { plural } from '../../../core/workspace/plural';
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
import { TopologyMap, TopologyNode, buildTopologyMap } from './topology-map';

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
  { value: 'all', label: 'Toutes' },
  { value: 'open', label: 'Ouvertes' },
  { value: 'merged', label: 'Fusionnées' },
];

/** Les tons viennent de `topologyTones` : la carte et le Diagnostic ne peuvent pas diverger. */
const legend: readonly LegendEntry[] = [
  { label: 'en avance', tone: topologyTones.Ahead },
  { label: 'divergente', tone: topologyTones.Diverged },
  { label: 'fusionnée', tone: topologyTones.Merged },
  { label: 'synchronisée', tone: topologyTones.Synchronized },
  { label: 'sans base', tone: topologyTones.Unrelated },
];

const legendDotSize = 7;
const haloRadius = 12;
const headRadius = 7;
const tipRadius = 5;
const junctionRadius = 3;
const spinnerSize = 20;
const shortShaLength = 8;

const pinnedHint = 'Re-clique la branche pour libérer la fiche.';
const unpinnedHint = 'Clique la branche pour épingler la fiche.';
const unknownAuthor = 'auteur inconnu';

/** Carte du dépôt : le tronc est la référence, chaque forme encode l'avance et le retard. */
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

  /** Focus tenu par nom de référence : une analyse rejouée renumérote les lignes du snapshot. */
  private readonly hoveredId = signal<string | null>(null);
  protected readonly pinnedId = signal<string | null>(null);
  protected readonly filter = signal<TopologyFilter>('all');

  /** L'épingle gagne toujours sur le survol : une fiche lue ne se dérobe pas. */
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
    return `référence · analyse ${analysisId.slice(0, shortShaLength)}`;
  });

  /** Décrit tout le snapshot, pas le filtre actif : la phrase parle du dépôt, pas de la vue. */
  protected readonly overview = computed(() => {
    const counts = this.map()?.counts;
    if (counts === undefined) {
      return '';
    }

    return (
      `${plural(counts.total, 'branche')} face à la référence : ` +
      `${plural(counts.open, 'ouverte')} au-dessus du tronc, ` +
      `${plural(counts.merged, 'fusionnée')} en pont, ` +
      `${plural(counts.synchronized, 'synchronisée')} au même sommet, ` +
      `${plural(counts.unrelated, 'isolée')} sans base commune.`
    );
  });

  protected readonly summary = computed(() => `Plan de topologie · ${this.overview()}`);

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

  /** Changer de filtre reconstruit la carte : un focus devenu invisible doit tomber. */
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

function detailsOf(node: TopologyNode, policy: PolicySnapshot): readonly KeyValueItem[] {
  const branch = node.branch;
  const author = branch.tipAuthor ?? unknownAuthor;
  return [
    { label: 'écart', value: `${node.gap} commits` },
    { label: 'dernier commit', value: `${relativeAge(branch.lastActivityAtUtc)} — ${author}` },
    { label: 'relation', value: relationshipLabels[branch.relationship] },
    { label: 'politique', value: policyLine(policy, branch.referenceName) },
  ];
}

/** Un motif l'emporte sur les seuils : c'est lui qui décide du sort de la branche. */
function policyLine(policy: PolicySnapshot, referenceName: string): string {
  const protectedBy = matchPattern(policy.protectedPatterns, referenceName);
  if (protectedBy !== null) {
    return `motif protégé ${protectedBy}`;
  }

  const excludedBy = matchPattern(policy.excludedPatterns, referenceName);
  if (excludedBy !== null) {
    return `motif exclu ${excludedBy}`;
  }

  return `active ≤ ${policy.activeUntilDays} j · inactive > ${policy.inactiveAfterDays} j`;
}
