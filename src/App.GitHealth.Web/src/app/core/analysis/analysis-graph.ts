import { AnalysisReferenceProgress } from '../api/api.models';
import { displayReference, topologyTones } from '../branches/branch-labels';
import { Tone } from '../../ui/icon-name';

/** Geometry of the drawing, in the units of its 312-wide viewport. */
const graphWidth = 312;
const rowHeight = 16;
const topPadding = 14;
const trunkX = 24;
const trunkPadding = 12;
const bottomPadding = 16;
const forkReach = 3;
const branchStep = 10;
const maximumStep = 8;
const branchOrigin = 64;
const labelOffset = 9;
const labelBaseline = 6;
const trunkLabelGap = 11;
const monospaceCharacterWidth = 6.7;
const minimumLabelLength = 10;

/**
 * References drawn at once. A repository can hold hundreds; the drawing is a window on the
 * ones being read, not a map of everything — the ledger next to it is the exhaustive list.
 */
const visibleRows = 14;

export interface AnalysisGraphNode {
  readonly id: string;
  readonly path: string;
  readonly x: number;
  readonly y: number;
  readonly isHollow: boolean;
  readonly tone: Tone;
  readonly labelX: number;
  readonly labelY: number;
  readonly label: string;
}

export interface AnalysisGraph {
  readonly nodes: readonly AnalysisGraphNode[];
  readonly height: number;
  readonly trunkEnd: number;
  readonly trunkLabelY: number;
  readonly cursorX: number | null;
  readonly cursorY: number | null;
  readonly placed: number;
  readonly total: number;
}

export function buildGraph(references: readonly AnalysisReferenceProgress[]): AnalysisGraph {
  const drawable = references.filter(isDrawable);
  const drawn = slidingWindow(drawable);
  const trunkEnd = topPadding + Math.max(0, drawn.length - 1) * rowHeight + trunkPadding;
  const cursor = drawn.findIndex(isReading);
  const nodes = drawn
    .map((reference, rank) => ({ reference, rank }))
    .filter((entry) => hasTopology(entry.reference))
    .map((entry) => toNode(entry.reference, entry.rank));
  return {
    nodes,
    height: trunkEnd + bottomPadding,
    trunkEnd,
    trunkLabelY: trunkEnd + trunkLabelGap,
    cursorX: cursor === -1 ? null : cursorColumn(drawn[cursor]),
    cursorY: cursor === -1 ? null : topPadding + cursor * rowHeight,
    placed: references.filter(hasTopology).length,
    total: references.length,
  };
}

/**
 * The drawing follows the reference being read: it ends on it, so the cursor stays in
 * sight instead of scrolling past the bottom on a repository with hundreds of branches.
 */
function slidingWindow(
  drawable: readonly AnalysisReferenceProgress[],
): readonly AnalysisReferenceProgress[] {
  const reading = lastIndexOfReading(drawable);
  const end = reading === -1 ? drawable.length : reading + 1;
  return drawable.slice(Math.max(0, end - visibleRows), end);
}

function cursorColumn(reference: AnalysisReferenceProgress): number {
  return hasTopology(reference) ? branchColumn(reference) : trunkX;
}

function toNode(reference: AnalysisReferenceProgress, rank: number): AnalysisGraphNode {
  const y = topPadding + rank * rowHeight;
  const x = branchColumn(reference);
  const labelX = x + labelOffset;
  return {
    id: reference.referenceName,
    path: `M${trunkX},${y - rowHeight + forkReach} Q${trunkX},${y} ${x - forkReach},${y}`,
    x,
    y,
    isHollow: (reference.aheadCount ?? 0) === 0,
    tone: reference.topology === null ? 'neutral' : topologyTones[reference.topology],
    labelX,
    labelY: y + labelBaseline / 2,
    label: truncate(displayReference(reference.referenceName), labelRoom(labelX)),
  };
}

/** The further a reference has run ahead, the further its node sits from the trunk. */
function branchColumn(reference: AnalysisReferenceProgress): number {
  const ahead = Math.min(reference.aheadCount ?? 0, maximumStep);
  return branchOrigin + ahead * branchStep;
}

function isReading(reference: AnalysisReferenceProgress): boolean {
  return reference.state === 'Measuring' || reference.state === 'Enriching';
}

function lastIndexOfReading(references: readonly AnalysisReferenceProgress[]): number {
  for (let index = references.length - 1; index >= 0; index -= 1) {
    if (isReading(references[index])) {
      return index;
    }
  }

  return -1;
}

function labelRoom(labelX: number): number {
  const room = Math.floor((graphWidth - labelX) / monospaceCharacterWidth);
  return Math.max(minimumLabelLength, room);
}

function truncate(value: string, length: number): string {
  return value.length <= length ? value : `${value.slice(0, length - 1)}…`;
}

/** Drawn once a reference is measured, or while it is being measured. */
function isDrawable(reference: AnalysisReferenceProgress): boolean {
  return reference.topology !== null || reference.state === 'Measuring';
}

function hasTopology(reference: AnalysisReferenceProgress): boolean {
  return reference.topology !== null;
}
