import { BranchSnapshotResponse, RecommendationKind } from '../../../core/api/api.models';
import {
  displayReference,
  recommendationLabels,
  recommendationTones,
} from '../../../core/branches/branch-labels';
import { plural } from '../../../core/workspace/plural';
import { Tone } from '../../../ui/icon-name';

/** Six captures racontent déjà une dérive ; au-delà, chaque capture coûte toutes ses branches. */
export const driftCaptureLimit = 6;

export type DriftKind = 'worse' | 'better' | 'fresh' | 'gone' | 'same';

/** Une capture réduite à ce que le journal en lit : ses branches indexées par nom de référence. */
export interface DriftCapture {
  readonly analysisId: string;
  readonly short: string;
  readonly label: string;
  readonly branches: ReadonlyMap<string, BranchSnapshotResponse>;
  /** Liste de branches coupée au plafond de lecture : le diff de cette capture est partiel. */
  readonly isBranchListTruncated: boolean;
}

export interface DriftCell {
  readonly title: string;
  /** `null` : la branche n'existait pas à cette capture, la case se dessine en pointillé. */
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

/** Le journal ouvre sur ce qui demande une action ; le résumé, lui, ouvre sur la bonne nouvelle. */
const groupDefinitions: readonly GroupDefinition[] = [
  { kind: 'worse', label: 'Dégradées', dot: 'danger', arrow: 'danger' },
  { kind: 'better', label: 'Résolues', dot: 'success', arrow: 'success' },
  { kind: 'fresh', label: 'Nouvelles', dot: 'info', arrow: 'info' },
  { kind: 'gone', label: 'Supprimées du dépôt', dot: 'neutral', arrow: 'danger' },
  { kind: 'same', label: 'Inchangées', dot: 'neutral', arrow: 'neutral' },
];

const statLabels: Readonly<Record<DriftKind, string>> = {
  worse: 'dégradées',
  better: 'résolues',
  fresh: 'nouvelles branches',
  gone: 'supprimées du dépôt',
  same: 'inchangées',
};

const recommendationRank: Partial<Record<RecommendationKind, number>> = {
  Keep: 0,
  Review: 1,
  CleanupCandidate: 2,
};

const unrankedRecommendation = 9;
const collapsibleKind: DriftKind = 'same';
const absentLabel = 'absente';
const removedLabel = 'supprimée';
const freshNotePrefix = 'créée après la capture du';
const legendTail = ' Les cases pâlies sont hors de la comparaison choisie.';
const historyWindowNotice = ` Seules les ${driftCaptureLimit} dernières captures sont affichées.`;
const cellSize = 11;
const cellGap = 4;
const stripPadding = 10;
const journalColumns = '212px 146px 20px 146px minmax(0, 1fr)';

/** Le diff de deux captures, groupé par mouvement et résumé en une phrase. */
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
 * Deux bizarreries de la règle sont volontairement conservées : `Excluded` et `Merged` n'ont
 * pas de rang, donc `Merged → À examiner` compte comme une résolution et `Conserver → Exclue`
 * comme une dégradation. C'est la règle de la maquette, reproduite telle quelle.
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

/** A reste strictement plus ancienne que B : le select laissé de côté est repoussé d'un cran. */
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

/** La bande d'historique mesure exactement ses cases : jamais une largeur figée. */
export function driftGridColumns(captureCount: number): string {
  const cells = Math.max(0, captureCount * cellSize + (captureCount - 1) * cellGap);
  return `${cells + stripPadding}px ${journalColumns}`;
}

/** Les dates viennent des captures chargées, et disent quand l'historique a été coupé. */
export function driftLegend(captures: readonly DriftCapture[], isTruncated: boolean): string {
  if (captures.length === 0) {
    return '';
  }
  const scope = isTruncated
    ? `de la première chargée (${captures[0].short})`
    : `de la plus ancienne (${captures[0].short})`;
  const last = captures[captures.length - 1].short;
  const notice = isTruncated ? historyWindowNotice : '';
  return `Chaque case suit le verdict d’une capture, ${scope} à ${last}.${notice}${legendTail}`;
}

/** Une seule capture coupée suffit à rendre le diff partiel : les cinq compteurs mentent alors. */
export function hasTruncatedBranchList(captures: readonly DriftCapture[]): boolean {
  return captures.some((capture) => capture.isBranchListTruncated);
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

/** Une branche disparue n'a plus de raison en B : la sienne vient de la capture de départ. */
function buildNote(
  request: DriftRequest,
  kind: DriftKind,
  source: BranchSnapshotResponse | null,
): string {
  const reason = source?.reason ?? '';
  if (kind !== 'fresh') {
    return reason;
  }
  return `${freshNotePrefix} ${request.captures[request.fromIndex].short} — ${reason}`;
}

function buildCells(request: DriftRequest, referenceName: string): readonly DriftCell[] {
  return request.captures.map((capture, index) => {
    const found = capture.branches.get(referenceName)?.recommendation ?? null;
    const verdict = found === null ? absentLabel : recommendationLabels[found];
    return {
      title: `${capture.short} : ${verdict}`,
      tone: found === null ? null : recommendationTones[found],
      isCompared: index === request.fromIndex || index === request.toIndex,
    };
  });
}

/** L'ordre de la capture de départ, puis les nouvelles venues : la lecture reste stable. */
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

/** Le panneau garde ses cinq lignes même à zéro : le journal montre, le panneau fait le bilan. */
function toStats(buckets: Buckets): readonly DriftStat[] {
  return groupDefinitions.map(({ kind, dot }) => ({
    label: statLabels[kind],
    tone: dot,
    count: countOf(buckets, kind),
  }));
}

function toSummary(from: string, to: string, buckets: Buckets): string {
  const parts = [
    plural(countOf(buckets, 'better'), 'résolution'),
    plural(countOf(buckets, 'worse'), 'dégradation'),
    plural(countOf(buckets, 'fresh'), 'nouvelle'),
    plural(countOf(buckets, 'gone'), 'supprimée'),
  ];
  return `Entre ${from} et ${to} : ${parts.join(', ')}.`;
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
