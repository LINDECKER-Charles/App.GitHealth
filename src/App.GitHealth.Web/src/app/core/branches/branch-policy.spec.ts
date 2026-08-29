import { BranchSnapshotResponse, PolicySnapshot } from '../api/api.models';
import {
  appliedThresholds,
  matchPattern,
  parsePatterns,
  projectActivity,
  projectRecommendation,
} from './branch-policy';

const day = 86_400_000;

const policy: PolicySnapshot = {
  activeUntilDays: 30,
  inactiveAfterDays: 90,
  protectedPatterns: [],
  excludedPatterns: [],
};

function branch(overrides: Partial<BranchSnapshotResponse> = {}): BranchSnapshotResponse {
  return {
    id: 'b1',
    referenceName: 'refs/heads/feature/export-csv',
    commitId: 'abc',
    aheadCount: 3,
    behindCount: 4,
    relationship: 'CommonAncestor',
    lastActivityAtUtc: new Date(Date.now() - 5 * day).toISOString(),
    tipAuthor: 'Ada',
    topology: 'Diverged',
    activity: 'Active',
    recommendation: 'Review',
    reason: '',
    isProtected: false,
    isExcluded: false,
    ...overrides,
  };
}

describe('matchPattern', () => {
  it('accepte le joker * sur plusieurs segments', () => {
    expect(matchPattern(['refs/*'], 'refs/remotes/origin/main')).toBe('refs/*');
  });

  it('accepte le joker ? sur un seul caractère', () => {
    expect(matchPattern(['refs/heads/v?'], 'refs/heads/v2')).toBe('refs/heads/v?');
    expect(matchPattern(['refs/heads/v?'], 'refs/heads/v20')).toBeNull();
  });

  it('ancre le motif sur la référence entière', () => {
    expect(matchPattern(['refs/heads/main'], 'refs/heads/maintenance')).toBeNull();
  });

  it('rend le premier motif qui correspond', () => {
    expect(matchPattern(['refs/tags/*', 'refs/heads/*'], 'refs/heads/main')).toBe('refs/heads/*');
  });
});

describe('projectActivity', () => {
  it('classe sous le seuil actif', () => {
    expect(projectActivity(branch(), policy)).toBe('Active');
  });

  it('classe entre les deux seuils', () => {
    const aging = branch({ lastActivityAtUtc: new Date(Date.now() - 45 * day).toISOString() });
    expect(projectActivity(aging, policy)).toBe('Aging');
  });

  it('classe au-delà du seuil inactif', () => {
    const inactive = branch({ lastActivityAtUtc: new Date(Date.now() - 200 * day).toISOString() });
    expect(projectActivity(inactive, policy)).toBe('Inactive');
  });

  it('rend Unknown sans date de sommet', () => {
    expect(projectActivity(branch({ lastActivityAtUtc: null }), policy)).toBe('Unknown');
  });
});

describe('projectRecommendation', () => {
  it('retire des recommandations une branche protégée', () => {
    const guarded = { ...policy, protectedPatterns: ['refs/heads/feature/*'] };
    expect(projectRecommendation(branch(), guarded)).toBe('Excluded');
  });

  it('retire des recommandations une branche exclue', () => {
    const hidden = { ...policy, excludedPatterns: ['refs/heads/feature/*'] };
    expect(projectRecommendation(branch(), hidden)).toBe('Excluded');
  });

  it('propose le nettoyage d’une branche fusionnée et inactive', () => {
    const merged = branch({
      topology: 'Merged',
      lastActivityAtUtc: new Date(Date.now() - 200 * day).toISOString(),
    });
    expect(projectRecommendation(merged, policy)).toBe('CleanupCandidate');
  });

  it('demande un examen pour une divergence récente', () => {
    expect(projectRecommendation(branch(), policy)).toBe('Review');
  });

  it('demande un examen sans ancêtre commun', () => {
    expect(projectRecommendation(branch({ topology: 'Unrelated' }), policy)).toBe('Review');
  });

  it('conserve une branche en avance et active', () => {
    expect(projectRecommendation(branch({ topology: 'Ahead' }), policy)).toBe('Keep');
  });
});

/**
 * Ce tableau doit rester identique à `MergedBranchScaleTests` côté serveur : c'est le
 * seul garde-fou contre une dérive entre la règle et son miroir dans l'interface.
 */
describe('échelle réduite des branches sans commit propre', () => {
  const aged = (days: number, topology: 'Merged' | 'Synchronized' | 'Diverged') =>
    branch({ topology, lastActivityAtUtc: new Date(Date.now() - days * day).toISOString() });

  it.each([
    [3, 'Active', 'Merged'],
    [7, 'Active', 'Merged'],
    [8, 'Aging', 'Review'],
    [30, 'Aging', 'Review'],
    [31, 'Inactive', 'CleanupCandidate'],
  ] as const)('fusionnée depuis %i j : %s → %s', (days, activity, recommendation) => {
    const merged = aged(days, 'Merged');
    expect(projectActivity(merged, policy)).toBe(activity);
    expect(projectRecommendation(merged, policy)).toBe(recommendation);
  });

  it('traite de la même façon une branche au même sommet que la référence', () => {
    expect(projectRecommendation(aged(60, 'Synchronized'), policy)).toBe('CleanupCandidate');
    expect(projectRecommendation(aged(2, 'Synchronized'), policy)).toBe('Merged');
  });

  it('ne recommande jamais « Conserver » sans commit propre', () => {
    for (const days of [1, 14, 120]) {
      expect(projectRecommendation(aged(days, 'Merged'), policy)).not.toBe('Keep');
    }
  });

  it('laisse l’échelle du projet aux branches qui portent des commits propres', () => {
    const diverged = aged(45, 'Diverged');
    expect(projectActivity(diverged, policy)).toBe('Aging');
    expect(projectRecommendation(diverged, policy)).toBe('Review');
  });

  it('ne rallonge jamais une échelle de projet déjà plus courte', () => {
    const tight = { ...policy, activeUntilDays: 3, inactiveAfterDays: 10 };
    expect(appliedThresholds('Merged', tight)).toEqual({
      activeUntilDays: 3,
      inactiveAfterDays: 10,
      isReduced: false,
    });
  });

  it('signale l’échelle réduite pour que la fiche puisse l’expliquer', () => {
    expect(appliedThresholds('Merged', policy)).toEqual({
      activeUntilDays: 7,
      inactiveAfterDays: 30,
      isReduced: true,
    });
    expect(appliedThresholds('Diverged', policy).isReduced).toBe(false);
  });
});

describe('parsePatterns', () => {
  it('découpe, nettoie et déduplique', () => {
    expect(parsePatterns('  refs/heads/main \n\n refs/heads/main \r\n refs/tags/*')).toEqual([
      'refs/heads/main',
      'refs/tags/*',
    ]);
  });
});
