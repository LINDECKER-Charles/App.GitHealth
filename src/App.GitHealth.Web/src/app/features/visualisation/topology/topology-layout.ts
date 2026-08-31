import { BranchSnapshotResponse, BranchTopology } from '../../../core/api/api.models';

/** Drawing frame: the width is fixed, only the height follows the number of branches. */
export const viewBoxWidth = 990;
export const trunkStartX = 40;
export const headX = 830;
export const headLabelOffsetX = 16;
export const headNameOffsetY = -4;
export const headNoteOffsetY = 12;

const minimumSpan = 6;
const scaleMargin = 140;
const minimumScale = 4.5;
const maximumScale = 12;
const firstBranchY = 56;
const branchStep = 36;
const trunkGap = 44;
const bottomPadding = 60;
const forkLeftClamp = 120;
const forkSpacing = 18;
const elbowRadius = 12;
const tipMinimumLength = 60;
const tipLead = 26;
const tipRightClamp = 940;
const mergedTopOffset = 52;
const mergedBridgeWidth = 92;
const mergedLeftOffset = 46;
const mergedLeftClamp = 80;
const mergedBudget = 440;
const mergedStepMin = 44;
const mergedStepMax = 110;
const syncRadius = 11;
const syncRadiusStep = 6;
const syncLabelOffsetY = 30;
const syncLabelStep = 15;
const syncGapOffsetX = 110;
const unrelatedStartX = 60;
const unrelatedBandOffset = 60;
const unrelatedDash = '5 5';
const labelGap = 12;
const nameLiftY = -7;
const firstLineY = 4;
const secondLineY = 18;
const roundingFactor = 100;

export type TopologyFilter = 'all' | 'open' | 'merged';

export interface TopologyPoint {
  readonly x: number;
  readonly y: number;
}

/** A branch laid out in the frame, before any decoration tied to the focus. */
export interface NodeGeometry {
  readonly branch: BranchSnapshotResponse;
  readonly path: string;
  readonly junction: TopologyPoint;
  readonly tip: TopologyPoint;
  readonly nameAt: TopologyPoint;
  readonly gapAt: TopologyPoint;
  readonly dash: string | null;
}

export interface BranchLayout {
  readonly placed: readonly NodeGeometry[];
  readonly trunkY: number;
  readonly height: number;
}

type Branches = readonly BranchSnapshotResponse[];

interface MapFrame {
  readonly scale: number;
  readonly trunkY: number;
}

interface Fork {
  branch: BranchSnapshotResponse;
  forkX: number;
}

interface Bridge {
  readonly start: number;
  readonly end: number;
  readonly y: number;
}

/** "Open" means "not merged": the in-sync and the isolated branches stay. */
export function isVisibleUnder(filter: TopologyFilter, topology: BranchTopology): boolean {
  if (filter === 'merged') {
    return topology === 'Merged';
  }

  return filter !== 'open' || topology !== 'Merged';
}

/** The trunk sits below the open branches, and the frame grows with the content. */
export function layoutBranches(branches: Branches, filter: TopologyFilter): BranchLayout {
  const visible = branches.filter((branch) => isVisibleUnder(filter, branch.topology));
  const select = (topology: BranchTopology) =>
    visible.filter((branch) => branch.topology === topology);
  const scale = pixelsPerCommit(visible);
  const open = visible.filter((branch) => isOpen(branch.topology));
  const trunkY = firstBranchY + Math.max(0, open.length - 1) * branchStep + trunkGap;
  const frame: MapFrame = { scale, trunkY };
  const anchored = [
    ...placeOpen(open, frame),
    ...placeMerged(select('Merged'), frame),
    ...placeSynchronized(select('Synchronized'), frame),
  ];
  const bandTop = lowestPoint(anchored, trunkY) + unrelatedBandOffset;
  const placed = [...anchored, ...placeUnrelated(select('Unrelated'), scale, bandTop)];
  return { placed, trunkY, height: lowestPoint(placed, trunkY) + bottomPadding };
}

export function isOpen(topology: BranchTopology): boolean {
  return topology === 'Diverged' || topology === 'Ahead';
}

function placeOpen(branches: Branches, frame: MapFrame): readonly NodeGeometry[] {
  return spreadForks(branches, frame.scale).map(({ branch, forkX }, index) => {
    const y = firstBranchY + index * branchStep;
    const reach = Math.max(tipMinimumLength, branch.aheadCount * frame.scale + tipLead);
    const tipX = round(Math.min(tipRightClamp, forkX + reach));
    const corner = forkX + elbowRadius;
    return {
      branch,
      path:
        `M ${forkX} ${frame.trunkY} L ${forkX} ${y + elbowRadius} ` +
        `Q ${forkX} ${y} ${corner} ${y} L ${tipX} ${y}`,
      junction: { x: forkX, y: frame.trunkY },
      tip: { x: tipX, y },
      nameAt: { x: forkX + labelGap, y: y + nameLiftY },
      gapAt: { x: tipX + labelGap, y: y + firstLineY },
      dash: null,
    };
  });
}

/**
 * Forks are de-collided starting from the rightmost one: nothing is ever pushed right,
 * and the left clamp keeps priority so the drawing never leaves the frame.
 */
function spreadForks(branches: Branches, scale: number): readonly Fork[] {
  const forks: Fork[] = branches.map((branch) => ({
    branch,
    forkX: Math.max(forkLeftClamp, headX - branch.behindCount * scale),
  }));
  forks.sort((left, right) => right.forkX - left.forkX);
  let limit = headX;
  for (const fork of forks) {
    fork.forkX = round(Math.max(forkLeftClamp, Math.min(fork.forkX, limit)));
    limit = fork.forkX - forkSpacing;
  }

  forks.sort((left, right) => left.forkX - right.forkX);
  return forks;
}

/** The bridge keeps a constant width: only its start carries any information. */
function placeMerged(branches: Branches, frame: MapFrame): readonly NodeGeometry[] {
  const top = frame.trunkY + mergedTopOffset;
  const step = mergedStep(branches.length);
  return branches.map((branch, index) => {
    const y = round(top + index * step);
    const reach = headX - branch.behindCount * frame.scale - mergedLeftOffset;
    const start = round(Math.min(headX - mergedBridgeWidth, Math.max(mergedLeftClamp, reach)));
    const end = start + mergedBridgeWidth;
    return {
      branch,
      path: bridgePath({ start, end, y }, frame.trunkY),
      junction: { x: start, y: frame.trunkY },
      tip: { x: end, y: frame.trunkY },
      nameAt: { x: end + labelGap, y: y + firstLineY },
      gapAt: { x: end + labelGap, y: y + secondLineY },
      dash: null,
    };
  });
}

/** Concentric loops around HEAD: every branch on the same tip stays readable. */
function placeSynchronized(branches: Branches, frame: MapFrame): readonly NodeGeometry[] {
  return branches.map((branch, index) => {
    const radius = syncRadius + index * syncRadiusStep;
    const labelY = frame.trunkY + syncLabelOffsetY + index * syncLabelStep;
    const labelX = headX + headLabelOffsetX;
    const left = headX - radius;
    const right = headX + radius;
    return {
      branch,
      path: `M ${left} ${frame.trunkY} A ${radius} ${radius} 0 1 0 ${right} ${frame.trunkY}`,
      junction: { x: left, y: frame.trunkY },
      tip: { x: right, y: frame.trunkY },
      nameAt: { x: labelX, y: labelY },
      gapAt: { x: labelX + syncGapOffsetX, y: labelY },
      dash: null,
    };
  });
}

/** Detached band at the bottom: with no common ancestor, the length says ahead, not behind. */
function placeUnrelated(branches: Branches, scale: number, top: number): readonly NodeGeometry[] {
  return branches.map((branch, index) => {
    const y = round(top + index * branchStep);
    const reach = Math.max(tipMinimumLength, branch.aheadCount * scale);
    const tipX = round(Math.min(tipRightClamp, unrelatedStartX + reach));
    return {
      branch,
      path: `M ${unrelatedStartX} ${y} L ${tipX} ${y}`,
      junction: { x: unrelatedStartX, y },
      tip: { x: tipX, y },
      nameAt: { x: tipX + labelGap, y: y + firstLineY },
      gapAt: { x: tipX + labelGap, y: y + secondLineY },
      dash: unrelatedDash,
    };
  });
}

function bridgePath(bridge: Bridge, trunkY: number): string {
  const { start, end, y } = bridge;
  const shoulder = y - elbowRadius;
  return (
    `M ${start} ${trunkY} L ${start} ${shoulder} Q ${start} ${y} ${start + elbowRadius} ${y} ` +
    `L ${end - elbowRadius} ${y} Q ${end} ${y} ${end} ${shoulder} L ${end} ${trunkY}`
  );
}

/** Bridges spread out while there are few of them, never packing tighter than 44 px. */
function mergedStep(count: number): number {
  return count <= 1 ? 0 : clamp(mergedBudget / (count - 1), mergedStepMin, mergedStepMax);
}

function pixelsPerCommit(branches: Branches): number {
  const widest = branches.reduce((span, branch) => Math.max(span, spanOf(branch)), minimumSpan);
  return clamp((headX - scaleMargin) / widest, minimumScale, maximumScale);
}

function lowestPoint(placed: readonly NodeGeometry[], fallback: number): number {
  return placed.reduce((lowest, geometry) => Math.max(lowest, geometry.gapAt.y), fallback);
}

function spanOf(branch: BranchSnapshotResponse): number {
  return branch.topology === 'Unrelated' ? branch.aheadCount : branch.behindCount;
}

function clamp(value: number, lowest: number, highest: number): number {
  return Math.min(highest, Math.max(lowest, value));
}

function round(value: number): number {
  return Math.round(value * roundingFactor) / roundingFactor;
}
