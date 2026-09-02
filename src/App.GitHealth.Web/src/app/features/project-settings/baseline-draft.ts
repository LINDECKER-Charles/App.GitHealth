/** Must stay aligned with `ProjectSettings.MaximumBaselineCount` on the API side. */
export const maximumBaselineCount = 8;

/**
 * The order carries the meaning: the first baseline is the primary one, the one a repository
 * opens on. Every helper returns a new list and never reorders what it was not asked to move.
 */
export function addBaselines(
  baselines: readonly string[],
  references: readonly string[],
): readonly string[] {
  return references.reduce(appendBaseline, baselines);
}

/** A project always compares against something: the last baseline cannot be dropped. */
export function removeBaseline(baselines: readonly string[], reference: string): readonly string[] {
  return baselines.length <= 1 ? baselines : baselines.filter((name) => name !== reference);
}

/** Moves one baseline by `offset` places; a move that leaves the list is refused. */
export function moveBaseline(
  baselines: readonly string[],
  reference: string,
  offset: number,
): readonly string[] {
  const from = baselines.indexOf(reference);
  const to = from + offset;
  if (from < 0 || to < 0 || to >= baselines.length) {
    return baselines;
  }

  const reordered = [...baselines];
  reordered.splice(from, 1);
  reordered.splice(to, 0, reference);
  return reordered;
}

export function canAddBaseline(baselines: readonly string[]): boolean {
  return baselines.length < maximumBaselineCount;
}

/** Order counts as a change: promoting a baseline changes which one is primary. */
export function isBaselineListDirty(draft: readonly string[], saved: readonly string[]): boolean {
  return draft.length !== saved.length || draft.some((name, index) => name !== saved[index]);
}

export function baselineRemoveLabel(reference: string): string {
  return $localize`:@@settings.baselines.remove:Remove the baseline ${reference}:baseline:`;
}

export function baselineMoveUpLabel(reference: string): string {
  return $localize`:@@settings.baselines.moveUp:Move ${reference}:baseline: up in the order`;
}

function appendBaseline(baselines: readonly string[], candidate: string): readonly string[] {
  const reference = candidate.trim();
  const isNew = reference.length > 0 && !baselines.includes(reference);
  return isNew && canAddBaseline(baselines) ? [...baselines, reference] : baselines;
}
