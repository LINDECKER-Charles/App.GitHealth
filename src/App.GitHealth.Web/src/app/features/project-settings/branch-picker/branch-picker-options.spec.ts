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
  it('renders every reference, deduplicated, when the query is empty', () => {
    expect(displayNames()).toEqual(['main', 'origin/feature/export', 'release/2024.1']);
  });

  it('filters on the display name regardless of case', () => {
    expect(displayNames([], 'RELEASE')).toEqual(['release/2024.1']);
  });

  it('filters on the full reference name as well', () => {
    expect(displayNames([], 'refs/remotes')).toEqual(['origin/feature/export']);
  });

  it('ignores the whitespace around the query', () => {
    expect(displayNames([], '  main  ')).toEqual(['main']);
  });

  it('returns an empty list when nothing matches', () => {
    expect(displayNames([], 'hotfix')).toEqual([]);
  });

  it('names the pattern that already covers a reference', () => {
    const options = buildBranchOptions(references, ['refs/heads/release/*'], 'release');

    expect(options).toEqual([
      {
        referenceName: 'refs/heads/release/2024.1',
        displayName: 'release/2024.1',
        coveredBy: 'refs/heads/release/*',
      },
    ]);
  });

  it('leaves the coverage null for a reference no pattern catches', () => {
    const options = buildBranchOptions(references, ['refs/heads/release/*'], 'main');

    expect(options[0]?.coveredBy).toBeNull();
  });

  it('puts the tickable references before the ones already covered', () => {
    expect(displayNames(['refs/heads/main', 'refs/remotes/*'])).toEqual([
      'release/2024.1',
      'main',
      'origin/feature/export',
    ]);
  });
});
