import { ProjectResponse } from '../api/api.models';
import { sourceLocale } from '../i18n/locale';

export type SectionKind = 'favorites' | 'group' | 'ungrouped';

export interface ProjectSection {
  /** Stable collapse key: it survives the renaming of another group. */
  readonly key: string;
  readonly kind: SectionKind;
  readonly title: string;
  readonly projects: readonly ProjectResponse[];
}

const labelCollator = new Intl.Collator(sourceLocale, { sensitivity: 'base' });

export const favoritesSectionKey = 'favorites';
export const ungroupedSectionKey = 'ungrouped';

const favoritesTitle = $localize`:@@ui.projectSection.favorites:Favourites`;
const ungroupedTitle = $localize`:@@ui.projectSection.ungrouped:Ungrouped`;

export function groupSectionKey(groupName: string): string {
  return `group:${groupName}`;
}

/** Existing group names, deduplicated and sorted: enough to offer a destination. */
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
 * Arranges the filtered repositories into sections: favourites first, then the groups in
 * alphabetical order, then the rest. A favourite only appears under "Favourites" — the rail
 * never shows the same repository twice.
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
  return labelCollator.compare(left, right);
}
