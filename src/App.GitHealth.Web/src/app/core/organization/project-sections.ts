import { ProjectResponse } from '../api/api.models';

export type SectionKind = 'favorites' | 'group' | 'ungrouped';

export interface ProjectSection {
  /** Clé stable du repli : elle survit au renommage d'un autre groupe. */
  readonly key: string;
  readonly kind: SectionKind;
  readonly title: string;
  readonly projects: readonly ProjectResponse[];
}

export const favoritesSectionKey = 'favorites';
export const ungroupedSectionKey = 'ungrouped';

const favoritesTitle = 'Favoris';
const ungroupedTitle = 'Sans groupe';

export function groupSectionKey(groupName: string): string {
  return `group:${groupName}`;
}

/** Noms de groupes existants, dédoublonnés et triés : de quoi proposer un rangement. */
export function knownGroupNames(projects: readonly ProjectResponse[]): readonly string[] {
  const names = new Set<string>();
  for (const project of projects) {
    if (project.groupName !== null) {
      names.add(project.groupName);
    }
  }

  return [...names].sort(compareLabels);
}

/**
 * Range les dépôts filtrés en sections : les favoris d'abord, puis les groupes par ordre
 * alphabétique, puis le reste. Un favori ne paraît que dans « Favoris » — le rail ne montre
 * jamais deux fois le même dépôt.
 */
export function buildProjectSections(
  projects: readonly ProjectResponse[],
  filter: string,
): readonly ProjectSection[] {
  const matching = matchingProjects(projects, filter);
  const favorites = matching.filter((project) => project.isFavorite);
  const remaining = matching.filter((project) => !project.isFavorite);
  return [
    ...section(favoritesSectionKey, 'favorites', favoritesTitle, favorites),
    ...groupSections(remaining),
    ...section(
      ungroupedSectionKey,
      'ungrouped',
      ungroupedTitle,
      remaining.filter((project) => project.groupName === null),
    ),
  ];
}

function matchingProjects(
  projects: readonly ProjectResponse[],
  filter: string,
): readonly ProjectResponse[] {
  const needle = filter.trim().toLowerCase();
  return projects
    .filter((project) => project.displayName.toLowerCase().includes(needle))
    .slice()
    .sort((left, right) => compareLabels(left.displayName, right.displayName));
}

function groupSections(projects: readonly ProjectResponse[]): readonly ProjectSection[] {
  return knownGroupNames(projects).map((groupName) => ({
    key: groupSectionKey(groupName),
    kind: 'group' as const,
    title: groupName,
    projects: projects.filter((project) => project.groupName === groupName),
  }));
}

function section(
  key: string,
  kind: SectionKind,
  title: string,
  projects: readonly ProjectResponse[],
): readonly ProjectSection[] {
  return projects.length === 0 ? [] : [{ key, kind, title, projects }];
}

function compareLabels(left: string, right: string): number {
  return left.localeCompare(right, 'fr', { sensitivity: 'base' });
}
