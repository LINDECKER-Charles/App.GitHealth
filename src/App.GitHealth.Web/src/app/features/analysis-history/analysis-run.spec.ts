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
  it('renders the short identifier, the duration and the thresholds', () => {
    const [run] = toRuns([item()]);
    expect(run.shortId).toBe('c80c2489');
    expect(run.statusLabel).toBe('Completed');
    expect(run.tone).toBe('success');
    expect(run.duration).toBe('0.4 s');
    expect(run.thresholds).toBe('30 / 90 d');
    expect(run.reference).toBe('main');
    expect(run.branchCount).toBe('8 branches');
    expect(run.isOpenable).toBe(true);
    expect(run.isDeletable).toBe(true);
  });

  it('does not offer to delete a run that is still going', () => {
    const [run] = toRuns([item({ status: 'Running', completedAtUtc: null, capturedAtUtc: null })]);
    expect(run.isDeletable).toBe(false);
  });

  it('compares a run to the previous one, which follows it in the list', () => {
    const runs = toRuns([item({ branchCount: 8 }), item({ branchCount: 7 })]);
    expect(runs[0].delta).toBe('+1 branch');
    expect(runs[1].delta).toBe('—');
    expect(toRuns([item({ branchCount: 9 }), item({ branchCount: 7 })])[0].delta).toBe(
      '+2 branches',
    );
  });

  it('renders "unchanged" when the branch count is equal', () => {
    expect(toRuns([item(), item()])[0].delta).toBe('unchanged');
  });

  it('does not open a failed run and keeps its reason', () => {
    const [run] = toRuns([
      item({
        status: 'Failed',
        completedAtUtc: null,
        failureCode: 'git.exit_code_128',
        failureMessage: 'The repository was locked.',
      }),
    ]);
    expect(run.isOpenable).toBe(false);
    expect(run.tone).toBe('danger');
    expect(run.statusLabel).toBe('Failed');
    expect(run.branchCount).toBe('—');
    expect(run.failureCode).toBe('git.exit_code_128');
  });
});
