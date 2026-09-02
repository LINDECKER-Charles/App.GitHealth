import { AnalysisReferenceProgress, ReferenceProgressState } from '../api/api.models';
import { toLedgerRow } from './analysis-ledger-row';

function reference(
  state: ReferenceProgressState,
  overrides: Partial<AnalysisReferenceProgress> = {},
): AnalysisReferenceProgress {
  return {
    referenceName: 'refs/remotes/origin/feat/scan',
    commitId: 'aaaaaaaabbbbbbbbcccccccc',
    state,
    lastActivityAtUtc: new Date().toISOString(),
    tipAuthor: 'M. Dupont',
    mergeBaseCommit: null,
    aheadCount: null,
    behindCount: null,
    topology: null,
    topContributor: null,
    contributorCount: null,
    ...overrides,
  };
}

describe('toLedgerRow', () => {
  it('shows nothing but the name while the reference is only listed', () => {
    const row = toLedgerRow(reference('Listed'));

    expect(row.name).toBe('origin/feat/scan');
    expect(row.isReading).toBe(false);
    expect(row.isRead).toBe(false);
    expect(row).toMatchObject({ mergeBase: '', ahead: '', behind: '', contributors: '', age: '' });
    expect(row.topologyLabel).toBeNull();
  });

  it('fills the distance and the topology once the reference is measured', () => {
    const row = toLedgerRow(
      reference('Measured', {
        mergeBaseCommit: 'c480b1a7893da6710328e5092e5d84a97c6',
        aheadCount: 3,
        behindCount: 5,
        topology: 'Diverged',
      }),
    );

    expect(row.isRead).toBe(true);
    expect(row.mergeBase).toBe('c480b1a7');
    expect(row.ahead).toBe('+3');
    expect(row.behind).toBe('−5');
    expect(row.topologyLabel).toBe('Diverged');
    expect(row.topologyTone).toBe('warning');
  });

  it('leaves a zero counter bare, where a sign would read as movement', () => {
    const row = toLedgerRow(
      reference('Measured', { aheadCount: 0, behindCount: 0, topology: 'Synchronized' }),
    );

    expect(row.ahead).toBe('0');
    expect(row.behind).toBe('0');
  });

  it('names the main author and how many others follow', () => {
    const one = toLedgerRow(
      reference('Read', { topContributor: 'M. Dupont', contributorCount: 1 }),
    );
    const several = toLedgerRow(
      reference('Read', { topContributor: 'M. Dupont', contributorCount: 3 }),
    );
    const none = toLedgerRow(reference('Read', { topContributor: null, contributorCount: 0 }));

    expect(one.contributors).toBe('M. Dupont');
    expect(several.contributors).toBe('M. Dupont +2');
    expect(none.contributors).toBe('—');
  });

  it('pulses while the reference is being read, in either stage', () => {
    expect(toLedgerRow(reference('Measuring')).isReading).toBe(true);
    expect(toLedgerRow(reference('Enriching')).isReading).toBe(true);
  });
});
