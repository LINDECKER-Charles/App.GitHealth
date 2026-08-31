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
    reason: 'No action recommended',
    isProtected: false,
    isExcluded: false,
    ...overrides,
  };
}

describe('toSnapshotCsv', () => {
  it('writes the expected header, quoted and CRLF-terminated', () => {
    const csv = toSnapshotCsv([]);
    expect(csv.startsWith('﻿')).toBe(true);
    expect(csv.slice(1)).toBe(
      '"referenceName","commitId","aheadCount","behindCount","relationship",' +
        '"lastActivityAtUtc","tipAuthor","topology","activity","recommendation",' +
        '"reason","isProtected","isExcluded"\r\n',
    );
  });

  it('quotes every cell and doubles the quotation marks', () => {
    const csv = toSnapshotCsv([branch({ reason: 'Pattern a"b' })]);
    expect(csv).toContain('"Pattern a""b"');
  });

  it('neutralises a cell starting with a formula character', () => {
    const csv = toSnapshotCsv([branch({ tipAuthor: '=CMD()' })]);
    expect(csv).toContain('"\'=CMD()"');
  });

  it('renders an empty cell for a missing value', () => {
    const csv = toSnapshotCsv([branch({ tipAuthor: null, lastActivityAtUtc: null })]);
    expect(csv).toContain('"CommonAncestor","","",');
  });

  it('measures the size in bytes, accents included', () => {
    expect(csvByteLength('é')).toBe(2);
  });
});
