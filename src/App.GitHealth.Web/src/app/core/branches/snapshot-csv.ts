import { BranchSnapshotResponse } from '../api/api.models';

const columns = [
  'referenceName',
  'commitId',
  'aheadCount',
  'behindCount',
  'relationship',
  'lastActivityAtUtc',
  'tipAuthor',
  'topology',
  'activity',
  'recommendation',
  'reason',
  'isProtected',
  'isExcluded',
];

const formulaPrefixes = ['=', '+', '-', '@'];
const byteOrderMark = '﻿';
const rowSeparator = '\r\n';

/** Même format que l'export serveur : toutes les cellules citées, CRLF, BOM UTF-8. */
export function toSnapshotCsv(branches: readonly BranchSnapshotResponse[]): string {
  const rows = [columns, ...branches.map(cells)];
  return byteOrderMark + rows.map(formatRow).join('');
}

/** Nombre d'octets du fichier produit, pour l'annoncer dans le message de confirmation. */
export function csvByteLength(csv: string): number {
  return new TextEncoder().encode(csv).length;
}

function cells(snapshot: BranchSnapshotResponse): readonly (string | null)[] {
  return [
    snapshot.referenceName,
    snapshot.commitId,
    String(snapshot.aheadCount),
    String(snapshot.behindCount),
    snapshot.relationship,
    snapshot.lastActivityAtUtc,
    snapshot.tipAuthor,
    snapshot.topology,
    snapshot.activity,
    snapshot.recommendation,
    snapshot.reason,
    String(snapshot.isProtected),
    String(snapshot.isExcluded),
  ];
}

function formatRow(values: readonly (string | null)[]): string {
  return values.map(formatCell).join(',') + rowSeparator;
}

function formatCell(value: string | null): string {
  return `"${neutralize(value).replace(/"/g, '""')}"`;
}

/** Une cellule qui commence par `=`, `+`, `-` ou `@` serait interprétée comme une formule. */
function neutralize(value: string | null): string {
  if (value === null || value.length === 0) {
    return '';
  }

  const trimmed = value.trimStart();
  return trimmed.length > 0 && formulaPrefixes.includes(trimmed[0]) ? `'${value}` : value;
}
