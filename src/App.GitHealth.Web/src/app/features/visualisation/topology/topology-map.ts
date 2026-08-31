import { BranchSnapshotResponse } from '../../../core/api/api.models';
import {
  displayReference,
  topologyLabels,
  topologyTones,
} from '../../../core/branches/branch-labels';
import { Tone } from '../../../ui/icon-name';
import {
  NodeGeometry,
  TopologyFilter,
  TopologyPoint,
  headLabelOffsetX,
  headNameOffsetY,
  headNoteOffsetY,
  headX,
  isOpen,
  layoutBranches,
  trunkStartX,
  viewBoxWidth,
} from './topology-layout';

const focusStrokeWidth = 3;
const restStrokeWidth = 2;
const dimmedOpacity = 0.22;
const dimmedLabelOpacity = 0.25;
const excludedDash = '4 4';
const percentDecimals = 2;
const trailingAnchorRatio = 0.82;
const minusSign = '−';
const separator = ' · ';
const originPrefix = 'origin/';

/** Position d'une étiquette HTML, en pourcentage du cadre : l'overlay ne recalcule rien. */
export interface TopologyLabelPosition {
  readonly left: string;
  readonly top: string;
  /** Passé le seuil d'ancrage, l'étiquette bascule à gauche du repère pour rester dans le cadre. */
  readonly isTrailing: boolean;
}

export interface TopologyNode {
  /** Identité stable d'une analyse à l'autre : le nom de référence, jamais l'id de ligne. */
  readonly id: string;
  readonly branch: BranchSnapshotResponse;
  readonly name: string;
  readonly gap: string;
  readonly ariaLabel: string;
  readonly path: string;
  readonly tone: Tone;
  readonly junction: TopologyPoint;
  readonly tip: TopologyPoint;
  readonly strokeWidth: number;
  readonly dash: string | null;
  readonly opacity: number;
  readonly labelOpacity: number;
  readonly isFocused: boolean;
  readonly namePosition: TopologyLabelPosition;
  readonly gapPosition: TopologyLabelPosition;
}

export interface TopologyCounts {
  readonly total: number;
  readonly open: number;
  readonly merged: number;
  readonly synchronized: number;
  readonly unrelated: number;
}

export interface TopologyMapRequest {
  readonly branches: readonly BranchSnapshotResponse[];
  readonly filter: TopologyFilter;
  readonly focusedId: string | null;
}

export interface TopologyMap {
  readonly viewBox: string;
  readonly height: number;
  readonly trunkStartX: number;
  readonly trunkY: number;
  readonly headX: number;
  readonly headLabelX: number;
  readonly headNameY: number;
  readonly headNoteY: number;
  readonly nodes: readonly TopologyNode[];
  readonly counts: TopologyCounts;
}

/** Assemble la géométrie et l'état de focus en un plan que le gabarit se contente de poser. */
export function buildTopologyMap(request: TopologyMapRequest): TopologyMap {
  const { placed, trunkY, height } = layoutBranches(request.branches, request.filter);
  const focusedId = resolveFocus(placed, request.focusedId);
  return {
    viewBox: `0 0 ${viewBoxWidth} ${height}`,
    height,
    trunkStartX,
    trunkY,
    headX,
    headLabelX: headX + headLabelOffsetX,
    headNameY: trunkY + headNameOffsetY,
    headNoteY: trunkY + headNoteOffsetY,
    nodes: placed.map((geometry) => toNode(geometry, focusedId, height)),
    counts: countBranches(request.branches),
  };
}

/** Un focus sans nœud correspondant — filtre changé, analyse rejouée — n'estompe personne. */
function resolveFocus(placed: readonly NodeGeometry[], focusedId: string | null): string | null {
  const isPlaced = placed.some((geometry) => geometry.branch.referenceName === focusedId);
  return isPlaced ? focusedId : null;
}

function toNode(geometry: NodeGeometry, focusedId: string | null, height: number): TopologyNode {
  const branch = geometry.branch;
  const isFocused = focusedId === branch.referenceName;
  const isDimmed = focusedId !== null && !isFocused;
  const name = shortName(branch.referenceName);
  const gap = gapLabel(branch);
  return {
    id: branch.referenceName,
    branch,
    name,
    gap,
    ariaLabel: `${name}${separator}${topologyLabels[branch.topology]}${separator}${gap}`,
    path: geometry.path,
    tone: topologyTones[branch.topology],
    junction: geometry.junction,
    tip: geometry.tip,
    strokeWidth: isFocused ? focusStrokeWidth : restStrokeWidth,
    dash: branch.isExcluded ? excludedDash : geometry.dash,
    opacity: isDimmed ? dimmedOpacity : 1,
    labelOpacity: isDimmed ? dimmedLabelOpacity : 1,
    isFocused,
    namePosition: percentOf(geometry.nameAt, height),
    gapPosition: percentOf(geometry.gapAt, height),
  };
}

/** Comptes du dépôt entier : la phrase de la fiche vide parle du dépôt, pas du filtre. */
function countBranches(branches: readonly BranchSnapshotResponse[]): TopologyCounts {
  const count = (matches: (branch: BranchSnapshotResponse) => boolean) =>
    branches.filter(matches).length;
  return {
    total: branches.length,
    open: count((branch) => isOpen(branch.topology)),
    merged: count((branch) => branch.topology === 'Merged'),
    synchronized: count((branch) => branch.topology === 'Synchronized'),
    unrelated: count((branch) => branch.topology === 'Unrelated'),
  };
}

function percentOf(point: TopologyPoint, height: number): TopologyLabelPosition {
  const ratio = point.x / viewBoxWidth;
  return {
    left: `${(ratio * 100).toFixed(percentDecimals)}%`,
    top: `${((point.y / height) * 100).toFixed(percentDecimals)}%`,
    isTrailing: ratio > trailingAnchorRatio,
  };
}

function shortName(referenceName: string): string {
  const displayed = displayReference(referenceName);
  return displayed.startsWith(originPrefix) ? displayed.slice(originPrefix.length) : displayed;
}

function gapLabel(branch: BranchSnapshotResponse): string {
  const ahead = branch.aheadCount === 0 ? '0' : `+${branch.aheadCount}`;
  const behind = branch.behindCount === 0 ? '0' : `${minusSign}${branch.behindCount}`;
  return `${ahead} / ${behind}`;
}
