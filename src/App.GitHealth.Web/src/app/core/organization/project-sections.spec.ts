import { ProjectResponse } from '../api/api.models';
import {
  buildProjectSections,
  favoritesSectionKey,
  groupSectionKey,
  knownGroupNames,
  ungroupedSectionKey,
} from './project-sections';

function project(
  displayName: string,
  organization: Partial<Pick<ProjectResponse, 'isFavorite' | 'groupName'>> = {},
): ProjectResponse {
  return {
    id: displayName,
    displayName,
    repositoryPath: `/repositories/${displayName}`,
    isRepositoryAccessible: true,
    createdAtUtc: '2026-08-01T00:00:00Z',
    updatedAtUtc: '2026-08-01T00:00:00Z',
    referenceName: 'refs/heads/main',
    branchNamespace: 'refs/heads/*',
    activeUntilDays: 30,
    inactiveAfterDays: 90,
    excludedPatterns: [],
    protectedPatterns: [],
    isFavorite: organization.isFavorite ?? false,
    groupName: organization.groupName ?? null,
    lastSuccessfulAnalysisId: null,
  };
}

describe('buildProjectSections', () => {
  it('range les favoris en tête, puis les groupes, puis le reste', () => {
    const sections = buildProjectSections(
      [
        project('zeta'),
        project('alpha', { groupName: 'Back-office' }),
        project('beta', { isFavorite: true, groupName: 'Back-office' }),
        project('gamma', { groupName: 'Api' }),
      ],
      '',
    );

    expect(sections.map((section) => section.key)).toEqual([
      favoritesSectionKey,
      groupSectionKey('Api'),
      groupSectionKey('Back-office'),
      ungroupedSectionKey,
    ]);
  });

  it('ne montre un favori que dans « Favoris », jamais aussi dans son groupe', () => {
    const sections = buildProjectSections(
      [project('beta', { isFavorite: true, groupName: 'Back-office' }), project('alpha')],
      '',
    );

    const favorites = sections.find((section) => section.key === favoritesSectionKey);
    expect(favorites?.projects.map((entry) => entry.displayName)).toEqual(['beta']);
    expect(sections.some((section) => section.key === groupSectionKey('Back-office'))).toBe(false);
  });

  it('trie les dépôts d’une section par nom et laisse tomber les sections vides', () => {
    const sections = buildProjectSections([project('zeta'), project('alpha')], '');

    expect(sections).toHaveLength(1);
    expect(sections[0].projects.map((entry) => entry.displayName)).toEqual(['alpha', 'zeta']);
  });

  it('applique le filtre avant de constituer les sections', () => {
    const sections = buildProjectSections(
      [project('alpha', { isFavorite: true }), project('beta', { groupName: 'Api' })],
      'bet',
    );

    expect(sections.map((section) => section.key)).toEqual([groupSectionKey('Api')]);
  });
});

describe('knownGroupNames', () => {
  it('dédoublonne et trie les groupes existants', () => {
    const names = knownGroupNames([
      project('alpha', { groupName: 'Web' }),
      project('beta', { groupName: 'Api' }),
      project('gamma', { groupName: 'Web' }),
      project('delta'),
    ]);

    expect(names).toEqual(['Api', 'Web']);
  });
});
