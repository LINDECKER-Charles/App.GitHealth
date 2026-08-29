import { BranchSnapshotResponse, PolicySnapshot } from '../../core/api/api.models';
import { projectMatches, projectStats } from './policy-projection';

const day = 86_400_000;

const savedPolicy: PolicySnapshot = {
  activeUntilDays: 30,
  inactiveAfterDays: 90,
  protectedPatterns: [],
  excludedPatterns: [],
};

function branch(
  name: string,
  overrides: Partial<BranchSnapshotResponse> = {},
): BranchSnapshotResponse {
  return {
    id: name,
    referenceName: `refs/heads/${name}`,
    commitId: 'abc',
    aheadCount: 2,
    behindCount: 0,
    relationship: 'CommonAncestor',
    lastActivityAtUtc: new Date(Date.now() - 3 * day).toISOString(),
    tipAuthor: 'Ada',
    topology: 'Ahead',
    activity: 'Active',
    recommendation: 'Keep',
    reason: '',
    isProtected: false,
    isExcluded: false,
    ...overrides,
  };
}

const branches = [branch('docs/guide'), branch('feature/export-csv')];

describe('projectStats', () => {
  it('annonce « inchangé » quand la politique éditée vaut la politique enregistrée', () => {
    const stats = projectStats(branches, savedPolicy);
    expect(stats.map((stat) => stat.label)).toEqual([
      'Conserver',
      'Terminée',
      'À examiner',
      'Nettoyage possible',
      'Exclue',
    ]);
    expect(stats.every((stat) => stat.delta === 'inchangé')).toBe(true);
    expect(stats[0].count).toBe(2);
  });

  it('chiffre l’écart introduit par un nouveau motif', () => {
    const stats = projectStats(branches, {
      ...savedPolicy,
      excludedPatterns: ['refs/heads/feature/*'],
    });
    const keep = stats.find((stat) => stat.label === 'Conserver');
    const excluded = stats.find((stat) => stat.label === 'Exclue');
    expect(keep?.count).toBe(1);
    expect(keep?.delta).toBe('−1 vs politique enregistrée');
    expect(excluded?.count).toBe(1);
    expect(excluded?.delta).toBe('+1 vs politique enregistrée');
  });
});

describe('projectMatches', () => {
  it('ne liste que les branches capturées par un motif', () => {
    const matches = projectMatches(branches, {
      ...savedPolicy,
      protectedPatterns: ['refs/heads/docs/*'],
    });
    expect(matches).toEqual([
      { referenceName: 'refs/heads/docs/guide', flag: 'Protégée', tone: 'brand' },
    ]);
  });

  it('donne la priorité à l’exclusion sur la protection', () => {
    const matches = projectMatches(branches, {
      ...savedPolicy,
      protectedPatterns: ['refs/heads/*'],
      excludedPatterns: ['refs/heads/docs/*'],
    });
    expect(matches[0].flag).toBe('Exclue');
    expect(matches[1].flag).toBe('Protégée');
  });
});
