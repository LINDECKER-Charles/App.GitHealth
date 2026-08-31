import { AnalysisHistoryItem } from '../../core/api/api.models';
import { CompletedAnalysis, comparableAnalyses, toCaptureOptions } from './capture-history';

/**
 * The labels are read in the machine's time zone: the instants are therefore built in local
 * time, otherwise the expected hour only holds in the zone the test was written in.
 */
function localInstant(month: number, day: number, hour: number, minute: number): string {
  return new Date(2026, month - 1, day, hour, minute).toISOString();
}

const now = new Date(2026, 7, 30, 18, 0);
const julyNoon = localInstant(7, 2, 12, 0);
const todayNoon = localInstant(8, 30, 12, 0);

function analysis(overrides: Partial<AnalysisHistoryItem> = {}): AnalysisHistoryItem {
  return {
    analysisId: 'a1',
    status: 'Completed',
    startedAtUtc: '2026-08-30T17:00:00.000Z',
    completedAtUtc: '2026-08-30T17:01:00.000Z',
    capturedAtUtc: '2026-08-30T17:01:00.000Z',
    referenceName: 'refs/heads/main',
    referenceCommit: '4f2a91c3d5e6f7a8',
    branchNamespace: 'refs/heads/*',
    activeUntilDays: 30,
    inactiveAfterDays: 90,
    excludedPatterns: [],
    protectedPatterns: [],
    gitVersion: 'git 2.45',
    branchCount: 12,
    failureCode: null,
    failureMessage: null,
    ...overrides,
  };
}

describe('comparableAnalyses', () => {
  it('keeps only the completed analyses that carry a capture', () => {
    const kept = comparableAnalyses([
      analysis({ analysisId: 'ok' }),
      analysis({ analysisId: 'running', status: 'Running', capturedAtUtc: null }),
      analysis({ analysisId: 'failed', status: 'Failed', capturedAtUtc: null }),
      analysis({ analysisId: 'no-commit', referenceCommit: null }),
    ]);

    expect(kept.map((item) => item.analysisId)).toEqual(['ok']);
  });

  it('puts the captures back in chronological order, the oldest first', () => {
    const ordered = comparableAnalyses([
      analysis({ analysisId: 'recent', capturedAtUtc: '2026-08-30T10:00:00.000Z' }),
      analysis({ analysisId: 'oldest', capturedAtUtc: '2026-07-02T10:00:00.000Z' }),
      analysis({ analysisId: 'middle', capturedAtUtc: '2026-08-16T10:00:00.000Z' }),
    ]);

    expect(ordered.map((item) => item.analysisId)).toEqual(['oldest', 'middle', 'recent']);
  });
});

describe('toCaptureOptions', () => {
  const captures = comparableAnalyses([
    analysis({ analysisId: 'oldest', capturedAtUtc: julyNoon }),
    analysis({ analysisId: 'latest', capturedAtUtc: todayNoon }),
  ]) as readonly CompletedAnalysis[];

  it('marks the most recent capture explicitly in its label', () => {
    const options = toCaptureOptions(captures, now);

    expect(options[1].isLatest).toBe(true);
    expect(options[1].label).toContain('latest');
    expect(options[0].isLatest).toBe(false);
    expect(options[0].label).not.toContain('latest');
  });

  it('spells out a capture taken today and dates the others in short form', () => {
    const options = toCaptureOptions(captures, now);

    expect(options[1].short).toContain('today');
    expect(options[1].short).toContain('12:00');
    expect(options[0].short).toContain('Jul 2');
  });

  it('appends the first eight characters of the baseline commit', () => {
    const options = toCaptureOptions(captures, now);

    expect(options[0].label).toBe(`${options[0].short} · 4f2a91c3`);
  });

  it('tells two captures of the same day on the same commit apart by their time', () => {
    const sameDay = comparableAnalyses([
      analysis({ analysisId: 'morning', capturedAtUtc: localInstant(8, 30, 6, 15) }),
      analysis({ analysisId: 'evening', capturedAtUtc: localInstant(8, 30, 16, 42) }),
    ]);

    const labels = toCaptureOptions(sameDay, now).map((option) => option.label);

    expect(new Set(labels).size).toBe(2);
  });

  it('marks the single capture of a repository that has just been analysed', () => {
    const options = toCaptureOptions(captures.slice(0, 1), now);

    expect(options).toHaveLength(1);
    expect(options[0].isLatest).toBe(true);
  });

  it('returns nothing when no analysis has succeeded', () => {
    expect(toCaptureOptions([], now)).toEqual([]);
  });
});
