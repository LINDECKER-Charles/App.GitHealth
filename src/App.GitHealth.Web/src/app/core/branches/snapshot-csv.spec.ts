import { BranchSnapshotResponse } from '../api/api.models';
import { csvByteLength, toSnapshotCsv } from './snapshot-csv';

function branch(overrides: Partial<BranchSnapshotResponse> = {}): BranchSnapshotResponse {
  return {
    id: 'b1',
    referenceName: 'refs/heads/main',
    commitId: 'abcdef123456',
    aheadCount: 2,
    behindCount: 0,
    relationship: 'CommonAncestor',
    lastActivityAtUtc: '2026-08-29T08:00:00Z',
    tipAuthor: 'Ada Lovelace',
    topology: 'Ahead',
    activity: 'Active',
    recommendation: 'Keep',
    reason: 'Aucune action recommandée',
    isProtected: false,
    isExcluded: false,
    ...overrides,
  };
}

describe('toSnapshotCsv', () => {
  it('écrit l’en-tête attendu, cité et terminé en CRLF', () => {
    const csv = toSnapshotCsv([]);
    expect(csv.startsWith('﻿')).toBe(true);
    expect(csv.slice(1)).toBe(
      '"referenceName","commitId","aheadCount","behindCount","relationship",' +
        '"lastActivityAtUtc","tipAuthor","topology","activity","recommendation",' +
        '"reason","isProtected","isExcluded"\r\n',
    );
  });

  it('cite chaque cellule et double les guillemets', () => {
    const csv = toSnapshotCsv([branch({ reason: 'Motif « a"b »' })]);
    expect(csv).toContain('"Motif « a""b »"');
  });

  it('neutralise une cellule qui commence par un caractère de formule', () => {
    const csv = toSnapshotCsv([branch({ tipAuthor: '=CMD()' })]);
    expect(csv).toContain('"\'=CMD()"');
  });

  it('rend une cellule vide pour une valeur absente', () => {
    const csv = toSnapshotCsv([branch({ tipAuthor: null, lastActivityAtUtc: null })]);
    expect(csv).toContain('"CommonAncestor","","",');
  });

  it('mesure la taille en octets, accents compris', () => {
    expect(csvByteLength('é')).toBe(2);
  });
});
