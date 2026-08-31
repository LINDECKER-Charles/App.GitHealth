import { BranchSnapshotResponse, RecommendationKind } from '../../../core/api/api.models';
import {
  displayReference,
  recommendationLabels,
  recommendationTones,
} from '../../../core/branches/branch-labels';
import { pluralMessage } from '../../../core/i18n/plural-message';
import { Tone } from '../../../ui/icon-name';

/** Six captures already tell a drift; beyond that, every capture costs all of its branches. */
export const driftCaptureLimit = 6;

export type DriftKind = 'worse' | 'better' | 'fresh' | 'gone' | 'same';

/** A capture reduced to what the journal reads of it: its branches indexed by reference name. */
export interface DriftCapture {
  readonly analysisId: string;
  readonly short: string;
  readonly label: string;
  readonly branches: ReadonlyMap<string, BranchSnapshotResponse>;
  /** Branch list cut at the read cap: the drift of this capture is partial. */
  readonly isBranchListTruncated: boolean;
}

export interface DriftCell {
  readonly title: string;
  /** `null`: the branch did not exist at that capture, the cell is drawn dotted. */
  readonly tone: Tone | null;
  readonly isCompared: boolean;
}

export interface DriftRow {
  readonly name: string;
  readonly cells: readonly DriftCell[];
  readonly fromLabel: string;
  readonly fromTone: Tone | null;
  readonly toLabel: string;
  readonly toTone: Tone | null;
  readonly note: string;
  readonly isProtected: boolean;
  readonly isExcluded: boolean;
}

export interface DriftGroup {
  readonly kind: DriftKind;
  readonly rowsId: string;
  readonly label: string;
  readonly tone: Tone;
  readonly arrowTone: Tone;
  readonly count: number;
  readonly rows: readonly DriftRow[];
  readonly isCollapsible: boolean;
}

export interface DriftStat {
  readonly label: string;
  readonly tone: Tone;
  readonly count: number;
}

export interface Drift {
  readonly groups: readonly DriftGroup[];
  readonly stats: readonly DriftStat[];
  readonly summary: string;
}

export interface CaptureRange {
  readonly fromIndex: number;
  readonly toIndex: number;
}

export interface DriftRequest extends CaptureRange {
  readonly captures: readonly DriftCapture[];
}

export interface CaptureSelection extends CaptureRange {
  readonly count: number;
  readonly moved: 'from' | 'to';
}

interface GroupDefinition {
  readonly kind: DriftKind;
  readonly label: string;
  readonly dot: Tone;
  readonly arrow: Tone;
}

type Buckets = ReadonlyMap<DriftKind, readonly DriftRow[]>;

/** The journal opens on what demands an action; the summary opens on the good news. */
const groupDefinitions: readonly GroupDefinition[] = [
  {
    kind: 'worse',
    label: $localize`:@@drift.group.worse:Degraded`,
    dot: 'danger',
    arrow: 'danger',
  },
  {
    kind: 'better',
    label: $localize`:@@drift.group.better:Resolved`,
    dot: 'success',
    arrow: 'success',
  },
  { kind: 'fresh', label: $localize`:@@drift.group.fresh:New`, dot: 'info', arrow: 'info' },
  {
    kind: 'gone',
    label: $localize`:@@drift.group.gone:Removed from the repository`,
    dot: 'neutral',
    arrow: 'danger',
  },
  {
    kind: 'same',
    label: $localize`:@@drift.group.same:Unchanged`,
    dot: 'neutral',
    arrow: 'neutral',
  },
];

const statLabels: Readonly<Record<DriftKind, string>> = {
  worse: $localize`:@@drift.stat.worse:degraded`,
  better: $localize`:@@drift.stat.better:resolved`,
  fresh: $localize`:@@drift.stat.fresh:new branches`,
  gone: $localize`:@@drift.stat.gone:removed from the repository`,
  same: $localize`:@@drift.stat.same:unchanged`,
};

const recommendationRank: Partial<Record<RecommendationKind, number>> = {
  Keep: 0,
  Review: 1,
  CleanupCandidate: 2,
};

const unrankedRecommendation = 9;
const collapsibleKind: DriftKind = 'same';
const absentLabel = $localize`:@@drift.verdict.absent:absent`;
const removedLabel = $localize`:@@drift.verdict.removed:removed`;
const legendTail = $localize`:@@drift.legend.pale:Pale cells are outside the chosen comparison.`;
const historyWindowNotice = $localize`:@@drift.legend.window:Only the last ${driftCaptureLimit}:limit: captures are shown.`;
const cellSize = 11;
const cellGap = 4;
const stripPadding = 10;
const journalColumns = '212px 146px 20px 146px minmax(0, 1fr)';

/** The drift of two captures, grouped by movement and summed up in one sentence. */
export function buildDrift(request: DriftRequest): Drift {
  const from = request.captures[request.fromIndex];
  const to = request.captures[request.toIndex];
  const buckets = new Map<DriftKind, DriftRow[]>(groupDefinitions.map(({ kind }) => [kind, []]));
  for (const referenceName of joinedNames(from, to)) {
    const before = from.branches.get(referenceName) ?? null;
    const after = to.branches.get(referenceName) ?? null;
    const kind = classify(before?.recommendation ?? null, after?.recommendation ?? null);
    if (kind !== null) {
      buckets.get(kind)?.push(buildRow(request, referenceName, kind));
    }
  }

  return {
    groups: toGroups(buckets),
    stats: toStats(buckets),
    summary: toSummary(from.short, to.short, buckets),
  };
}

/**
 * Two oddities of the rule are deliberately kept: `Excluded` and `Merged` have no rank, so
 * `Merged → Review` counts as a resolution and `Keep → Excluded` as a degradation. That is
 * the rule of the mock-up, reproduced as it stands.
 */
function classify(
  before: RecommendationKind | null,
  after: RecommendationKind | null,
): DriftKind | null {
  if (before === null && after === null) {
    return null;
  }
  if (before === null) {
    return 'fresh';
  }
  if (after === null) {
    return 'gone';
  }
  if (before === after) {
    return 'same';
  }
  return after === 'Merged' || rankOf(after) < rankOf(before) ? 'better' : 'worse';
}

/** A stays strictly older than B: the select left alone is pushed one notch away. */
export function clampCaptureSelection(selection: CaptureSelection): CaptureRange {
  const last = selection.count - 1;
  if (last < 1) {
    return { fromIndex: 0, toIndex: 0 };
  }
  if (selection.moved === 'from') {
    const fromIndex = clamp(selection.fromIndex, 0, last - 1);
    return { fromIndex, toIndex: Math.max(clamp(selection.toIndex, 0, last), fromIndex + 1) };
  }
  const toIndex = clamp(selection.toIndex, 1, last);
  return { fromIndex: Math.min(clamp(selection.fromIndex, 0, last), toIndex - 1), toIndex };
}

/** The history strip measures exactly its cells: never a fixed width. */
export function driftGridColumns(captureCount: number): string {
  const cells = Math.max(0, captureCount * cellSize + (captureCount - 1) * cellGap);
  return `${cells + stripPadding}px ${journalColumns}`;
}

/** The dates come from the loaded captures, and say when the history was cut. */
export function driftLegend(captures: readonly DriftCapture[], isTruncated: boolean): string {
  if (captures.length === 0) {
    return '';
  }
  const first = captures[0].short;
  const last = captures[captures.length - 1].short;
  const scope = isTruncated ? truncatedScopeLabel(first, last) : fullScopeLabel(first, last);
  const parts = isTruncated ? [scope, historyWindowNotice, legendTail] : [scope, legendTail];
  return parts.join(' ');
}

/** A single cut capture is enough to make the drift partial: the five counters then lie. */
export function hasTruncatedBranchList(captures: readonly DriftCapture[]): boolean {
  return captures.some((capture) => capture.isBranchListTruncated);
}

function fullScopeLabel(first: string, last: string): string {
  return $localize`:@@drift.legend.full:Each cell follows the verdict of one capture, from the oldest (${first}:first:) to ${last}:last:.`;
}

function truncatedScopeLabel(first: string, last: string): string {
  return $localize`:@@drift.legend.truncated:Each cell follows the verdict of one capture, from the first loaded (${first}:first:) to ${last}:last:.`;
}

function buildRow(request: DriftRequest, referenceName: string, kind: DriftKind): DriftRow {
  const before = request.captures[request.fromIndex].branches.get(referenceName) ?? null;
  const after = request.captures[request.toIndex].branches.get(referenceName) ?? null;
  const flags = after ?? before;
  return {
    name: displayReference(referenceName),
    cells: buildCells(request, referenceName),
    fromLabel: before === null ? absentLabel : recommendationLabels[before.recommendation],
    fromTone: before === null ? null : recommendationTones[before.recommendation],
    toLabel: after === null ? removedLabel : recommendationLabels[after.recommendation],
    toTone: after === null ? null : recommendationTones[after.recommendation],
    note: buildNote(request, kind, kind === 'gone' ? before : after),
    isProtected: flags?.isProtected ?? false,
    isExcluded: flags?.isExcluded ?? false,
  };
}

/** A branch that is gone has no reason left in B: its own comes from the starting capture. */
function buildNote(
  request: DriftRequest,
  kind: DriftKind,
  source: BranchSnapshotResponse | null,
): string {
  const reason = source?.reason ?? '';
  if (kind !== 'fresh') {
    return reason;
  }
  const capture = request.captures[request.fromIndex].short;
  return $localize`:@@drift.note.fresh:created after the ${capture}:capture: capture — ${reason}:reason:`;
}

function buildCells(request: DriftRequest, referenceName: string): readonly DriftCell[] {
  return request.captures.map((capture, index) => {
    const found = capture.branches.get(referenceName)?.recommendation ?? null;
    const verdict = found === null ? absentLabel : recommendationLabels[found];
    return {
      title: cellTitle(capture.short, verdict),
      tone: found === null ? null : recommendationTones[found],
      isCompared: index === request.fromIndex || index === request.toIndex,
    };
  });
}

function cellTitle(capture: string, verdict: string): string {
  return $localize`:@@drift.cell.title:${capture}:capture:: ${verdict}:verdict:`;
}

/** The order of the starting capture, then the newcomers: the reading stays stable. */
function joinedNames(from: DriftCapture, to: DriftCapture): readonly string[] {
  const names = [...from.branches.keys()];
  for (const name of to.branches.keys()) {
    if (!from.branches.has(name)) {
      names.push(name);
    }
  }
  return names;
}

function toGroups(buckets: Buckets): readonly DriftGroup[] {
  return groupDefinitions
    .map(({ kind, label, dot, arrow }) => ({
      kind,
      rowsId: `drift-group-${kind}`,
      label,
      tone: dot,
      arrowTone: arrow,
      count: countOf(buckets, kind),
      rows: buckets.get(kind) ?? [],
      isCollapsible: kind === collapsibleKind,
    }))
    .filter((group) => group.count > 0);
}

/** The panel keeps its five rows even at zero: the journal shows, the panel takes stock. */
function toStats(buckets: Buckets): readonly DriftStat[] {
  return groupDefinitions.map(({ kind, dot }) => ({
    label: statLabels[kind],
    tone: dot,
    count: countOf(buckets, kind),
  }));
}

function toSummary(from: string, to: string, buckets: Buckets): string {
  const parts = [
    resolutionLabel(countOf(buckets, 'better')),
    degradationLabel(countOf(buckets, 'worse')),
    freshLabel(countOf(buckets, 'fresh')),
    goneLabel(countOf(buckets, 'gone')),
  ].join(', ');
  return $localize`:@@drift.summary.sentence:Between ${from}:from: and ${to}:to:: ${parts}:parts:.`;
}

function resolutionLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@drift.summary.resolutionOne:${count}:count: resolution`,
    other: $localize`:@@drift.summary.resolutionMany:${count}:count: resolutions`,
  });
}

function degradationLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@drift.summary.degradationOne:${count}:count: degradation`,
    other: $localize`:@@drift.summary.degradationMany:${count}:count: degradations`,
  });
}

function freshLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@drift.summary.freshOne:${count}:count: new branch`,
    other: $localize`:@@drift.summary.freshMany:${count}:count: new branches`,
  });
}

function goneLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@drift.summary.goneOne:${count}:count: removed branch`,
    other: $localize`:@@drift.summary.goneMany:${count}:count: removed branches`,
  });
}

function countOf(buckets: Buckets, kind: DriftKind): number {
  return (buckets.get(kind) ?? []).length;
}

function rankOf(recommendation: RecommendationKind): number {
  return recommendationRank[recommendation] ?? unrankedRecommendation;
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(Math.max(value, minimum), maximum);
}
