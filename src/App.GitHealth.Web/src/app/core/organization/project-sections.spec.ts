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
    referenceNames: ['refs/heads/main'],
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
  it('puts the favourites first, then the groups, then the rest', () => {
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

  it('shows a favourite only under "Favourites", never also in its group', () => {
    const sections = buildProjectSections(
      [project('beta', { isFavorite: true, groupName: 'Back-office' }), project('alpha')],
      '',
    );

    const favorites = sections.find((section) => section.key === favoritesSectionKey);
    expect(favorites?.projects.map((entry) => entry.displayName)).toEqual(['beta']);
    expect(sections.some((section) => section.key === groupSectionKey('Back-office'))).toBe(false);
  });

  it('sorts the repositories of a section by name and drops the empty sections', () => {
    const sections = buildProjectSections([project('zeta'), project('alpha')], '');

    expect(sections).toHaveLength(1);
    expect(sections[0].projects.map((entry) => entry.displayName)).toEqual(['alpha', 'zeta']);
  });

  it('applies the filter before building the sections', () => {
    const sections = buildProjectSections(
      [project('alpha', { isFavorite: true }), project('beta', { groupName: 'Api' })],
      'bet',
    );

    expect(sections.map((section) => section.key)).toEqual([groupSectionKey('Api')]);
  });
});

describe('knownGroupNames', () => {
  it('deduplicates and sorts the existing groups', () => {
    const names = knownGroupNames([
      project('alpha', { groupName: 'Web' }),
      project('beta', { groupName: 'Api' }),
      project('gamma', { groupName: 'Web' }),
      project('delta'),
    ]);

    expect(names).toEqual(['Api', 'Web']);
  });
});
