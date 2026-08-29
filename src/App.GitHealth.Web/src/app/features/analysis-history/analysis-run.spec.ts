import { AnalysisHistoryItem } from '../../core/api/api.models';
import { toRuns } from './analysis-run';

function item(overrides: Partial<AnalysisHistoryItem> = {}): AnalysisHistoryItem {
  return {
    analysisId: 'c80c2489-0000-0000-0000-000000000000',
    status: 'Completed',
    startedAtUtc: '2026-08-29T11:21:15Z',
    completedAtUtc: '2026-08-29T11:21:15.400Z',
    capturedAtUtc: '2026-08-29T11:21:15.400Z',
    referenceName: 'refs/heads/main',
    referenceCommit: '6f9f137c08ee',
    branchNamespace: 'refs/heads/*',
    activeUntilDays: 30,
    inactiveAfterDays: 90,
    excludedPatterns: ['refs/heads/archive/*'],
    protectedPatterns: ['refs/heads/main'],
    gitVersion: '2.51.0',
    branchCount: 8,
    failureCode: null,
    failureMessage: null,
    ...overrides,
  };
}

describe('toRuns', () => {
  it('rend l’identifiant court, la durée et les seuils', () => {
    const [run] = toRuns([item()]);
    expect(run.shortId).toBe('c80c2489');
    expect(run.statusLabel).toBe('Terminée');
    expect(run.tone).toBe('success');
    expect(run.duration).toBe('0,4 s');
    expect(run.thresholds).toBe('30 / 90 j');
    expect(run.reference).toBe('main');
    expect(run.branchCount).toBe('8 branches');
    expect(run.isOpenable).toBe(true);
  });

  it('compare le passage au précédent, qui suit dans la liste', () => {
    const runs = toRuns([item({ branchCount: 8 }), item({ branchCount: 7 })]);
    expect(runs[0].delta).toBe('+1 branche');
    expect(runs[1].delta).toBe('—');
    expect(toRuns([item({ branchCount: 9 }), item({ branchCount: 7 })])[0].delta).toBe(
      '+2 branches',
    );
  });

  it('rend « inchangé » à nombre de branches égal', () => {
    expect(toRuns([item(), item()])[0].delta).toBe('inchangé');
  });

  it('n’ouvre pas un passage échoué et conserve son motif', () => {
    const [run] = toRuns([
      item({
        status: 'Failed',
        completedAtUtc: null,
        failureCode: 'git.exit_code_128',
        failureMessage: 'Le dépôt était verrouillé.',
      }),
    ]);
    expect(run.isOpenable).toBe(false);
    expect(run.tone).toBe('danger');
    expect(run.statusLabel).toBe('Échouée');
    expect(run.branchCount).toBe('—');
    expect(run.failureCode).toBe('git.exit_code_128');
  });
});
