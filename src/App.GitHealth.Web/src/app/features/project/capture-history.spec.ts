import { AnalysisHistoryItem } from '../../core/api/api.models';
import { CompletedAnalysis, comparableAnalyses, toCaptureOptions } from './capture-history';

/**
 * Les libellés se lisent dans le fuseau du poste : les instants se posent donc en heure
 * locale, sinon l'heure attendue ne tient que dans le fuseau où le test a été écrit.
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
  it('ne garde que les analyses terminées qui portent une capture', () => {
    const kept = comparableAnalyses([
      analysis({ analysisId: 'ok' }),
      analysis({ analysisId: 'running', status: 'Running', capturedAtUtc: null }),
      analysis({ analysisId: 'failed', status: 'Failed', capturedAtUtc: null }),
      analysis({ analysisId: 'sans-commit', referenceCommit: null }),
    ]);

    expect(kept.map((item) => item.analysisId)).toEqual(['ok']);
  });

  it('remet les captures dans l’ordre chronologique, la plus ancienne en tête', () => {
    const ordered = comparableAnalyses([
      analysis({ analysisId: 'recente', capturedAtUtc: '2026-08-30T10:00:00.000Z' }),
      analysis({ analysisId: 'ancienne', capturedAtUtc: '2026-07-02T10:00:00.000Z' }),
      analysis({ analysisId: 'mediane', capturedAtUtc: '2026-08-16T10:00:00.000Z' }),
    ]);

    expect(ordered.map((item) => item.analysisId)).toEqual(['ancienne', 'mediane', 'recente']);
  });
});

describe('toCaptureOptions', () => {
  const captures = comparableAnalyses([
    analysis({ analysisId: 'ancienne', capturedAtUtc: julyNoon }),
    analysis({ analysisId: 'derniere', capturedAtUtc: todayNoon }),
  ]) as readonly CompletedAnalysis[];

  it('signale explicitement la plus récente dans son libellé', () => {
    const options = toCaptureOptions(captures, now);

    expect(options[1].isLatest).toBe(true);
    expect(options[1].label).toContain('dernière');
    expect(options[0].isLatest).toBe(false);
    expect(options[0].label).not.toContain('dernière');
  });

  it('date une capture du jour en toutes lettres et les autres au format court', () => {
    const options = toCaptureOptions(captures, now);

    expect(options[1].short).toBe("aujourd'hui 12:00");
    expect(options[0].short).toBe('2 juil 12:00');
  });

  it('accole les huit premiers caractères du commit de référence', () => {
    const options = toCaptureOptions(captures, now);

    expect(options[0].label).toBe('2 juil 12:00 · 4f2a91c3');
  });

  it('distingue deux captures du même jour sur le même commit par leur heure', () => {
    const sameDay = comparableAnalyses([
      analysis({ analysisId: 'matin', capturedAtUtc: localInstant(8, 30, 6, 15) }),
      analysis({ analysisId: 'soir', capturedAtUtc: localInstant(8, 30, 16, 42) }),
    ]);

    const labels = toCaptureOptions(sameDay, now).map((option) => option.label);

    expect(new Set(labels).size).toBe(2);
  });

  it('marque la seule capture d’un dépôt qui vient d’être analysé', () => {
    const options = toCaptureOptions(captures.slice(0, 1), now);

    expect(options).toHaveLength(1);
    expect(options[0].isLatest).toBe(true);
  });

  it('ne renvoie rien quand aucune analyse n’a abouti', () => {
    expect(toCaptureOptions([], now)).toEqual([]);
  });
});
