import { SnapshotDetailResponse } from '../../core/api/api.models';
import { buildTrace } from './branch-trace';

const day = 86_400_000;

function detail(overrides: Partial<SnapshotDetailResponse> = {}): SnapshotDetailResponse {
  return {
    analysisId: 'a1',
    referenceName: 'refs/heads/main',
    referenceCommit: '6f9f137c08ee',
    capturedAtUtc: '2026-08-29T11:21:16Z',
    snapshot: {
      id: 'b1',
      referenceName: 'refs/heads/feature/export-csv',
      commitId: '1f484960946c',
      aheadCount: 4,
      behindCount: 3,
      relationship: 'CommonAncestor',
      lastActivityAtUtc: new Date(Date.now() - 3 * day).toISOString(),
      tipAuthor: 'Camille Rousseau',
      topology: 'Diverged',
      activity: 'Active',
      recommendation: 'Review',
      reason: 'Historique divergent à examiner',
      isProtected: false,
      isExcluded: false,
    },
    contributors: [],
    attributionStatus: 'Available',
    mailmapApplied: true,
    policy: {
      activeUntilDays: 30,
      inactiveAfterDays: 90,
      excludedPatterns: ['refs/heads/archive/*'],
      protectedPatterns: ['refs/heads/main', 'refs/heads/release/*'],
    },
    ...overrides,
  };
}

describe('buildTrace', () => {
  it('énonce les motifs évalués, la topologie, l’activité puis la conclusion', () => {
    const lines = buildTrace(detail());
    expect(lines).toHaveLength(5);
    expect(lines[0].text).toBe('Aucun motif d’exclusion ne correspond');
    expect(lines[0].rule).toBe('1 motif évalué');
    expect(lines[1].rule).toBe('2 motifs évalués');
    expect(lines[2].text).toBe('Divergente : +4 / −3');
    expect(lines[2].rule).toBe('git merge-base --is-ancestor + git rev-list --count');
    expect(lines[3].text).toBe('Active : 3 j ≤ seuil 30 j');
    expect(lines[4].text).toBe('Conclusion : à examiner');
  });

  it('nomme le motif protégé qui capture la branche', () => {
    const lines = buildTrace(
      detail({
        snapshot: {
          ...detail().snapshot,
          referenceName: 'refs/heads/release/2026.08',
          isProtected: true,
        },
      }),
    );
    expect(lines[1].text).toBe('Protégée par « refs/heads/release/* »');
    expect(lines[1].rule).toContain('retirée des recommandations');
  });

  it('nomme le motif d’exclusion qui capture la branche', () => {
    const lines = buildTrace(
      detail({
        snapshot: {
          ...detail().snapshot,
          referenceName: 'refs/heads/archive/2023-legacy',
          isExcluded: true,
        },
      }),
    );
    expect(lines[0].text).toBe('Exclue par « refs/heads/archive/* »');
  });

  it('décrit une branche fusionnée par rapport à la référence', () => {
    const lines = buildTrace(
      detail({ snapshot: { ...detail().snapshot, topology: 'Merged', aheadCount: 0 } }),
    );
    expect(lines[2].text).toBe('Fusionnée : 0 commit en avance sur main');
  });

  it('assume l’absence de date de sommet', () => {
    const lines = buildTrace(
      detail({
        snapshot: { ...detail().snapshot, lastActivityAtUtc: null, activity: 'Unknown' },
      }),
    );
    expect(lines[3].text).toContain('Activité inconnue');
  });
});
