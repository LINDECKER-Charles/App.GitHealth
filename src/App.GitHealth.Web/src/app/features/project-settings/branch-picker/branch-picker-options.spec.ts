import { buildBranchOptions } from './branch-picker-options';

const references: readonly string[] = [
  'refs/heads/release/2024.1',
  'refs/heads/main',
  'refs/remotes/origin/feature/export',
  'refs/heads/main',
];

function displayNames(patterns: readonly string[] = [], query = ''): readonly string[] {
  return buildBranchOptions(references, patterns, query).map((option) => option.displayName);
}

describe('buildBranchOptions', () => {
  it('rend toutes les références, dédupliquées, quand la requête est vide', () => {
    expect(displayNames()).toEqual(['main', 'origin/feature/export', 'release/2024.1']);
  });

  it('filtre sur le nom affiché sans tenir compte de la casse', () => {
    expect(displayNames([], 'RELEASE')).toEqual(['release/2024.1']);
  });

  it('filtre aussi sur le nom de référence complet', () => {
    expect(displayNames([], 'refs/remotes')).toEqual(['origin/feature/export']);
  });

  it('ignore les espaces qui entourent la requête', () => {
    expect(displayNames([], '  main  ')).toEqual(['main']);
  });

  it('renvoie une liste vide quand rien ne correspond', () => {
    expect(displayNames([], 'hotfix')).toEqual([]);
  });

  it('nomme le motif qui couvre déjà une référence', () => {
    const options = buildBranchOptions(references, ['refs/heads/release/*'], 'release');

    expect(options).toEqual([
      {
        referenceName: 'refs/heads/release/2024.1',
        displayName: 'release/2024.1',
        coveredBy: 'refs/heads/release/*',
      },
    ]);
  });

  it('laisse à null la couverture d’une référence qu’aucun motif ne capture', () => {
    const options = buildBranchOptions(references, ['refs/heads/release/*'], 'main');

    expect(options[0]?.coveredBy).toBeNull();
  });

  it('place les références cochables avant celles déjà couvertes', () => {
    expect(displayNames(['refs/heads/main', 'refs/remotes/*'])).toEqual([
      'release/2024.1',
      'main',
      'origin/feature/export',
    ]);
  });
});
