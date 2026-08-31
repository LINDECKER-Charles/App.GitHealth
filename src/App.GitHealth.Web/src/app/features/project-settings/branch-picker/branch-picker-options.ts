import { displayReference } from '../../../core/branches/branch-labels';
import { matchPattern } from '../../../core/branches/branch-policy';

/** Les deux listes de motifs d'une politique, distinguées par le dialogue de sélection. */
export type BranchPatternKind = 'protected' | 'excluded';

export interface BranchPickerOption {
  readonly referenceName: string;
  readonly displayName: string;
  readonly coveredBy: string | null;
}

/**
 * Une référence déjà couverte reste visible mais passe en fin de liste : l'utilisateur lit
 * le motif qui la capture au lieu de la chercher en vain parmi les cases cochables.
 */
export function buildBranchOptions(
  references: readonly string[],
  patterns: readonly string[],
  query: string,
): readonly BranchPickerOption[] {
  const needle = query.trim().toLowerCase();
  return Array.from(new Set(references))
    .map((referenceName) => toOption(referenceName, patterns))
    .filter((option) => retains(option, needle))
    .sort(byAvailabilityThenName);
}

function toOption(referenceName: string, patterns: readonly string[]): BranchPickerOption {
  return {
    referenceName,
    displayName: displayReference(referenceName),
    coveredBy: matchPattern(patterns, referenceName),
  };
}

/** Le nom court sert à chercher « release », le nom complet à chercher « refs/remotes ». */
function retains(option: BranchPickerOption, needle: string): boolean {
  return (
    needle.length === 0 ||
    option.displayName.toLowerCase().includes(needle) ||
    option.referenceName.toLowerCase().includes(needle)
  );
}

function byAvailabilityThenName(left: BranchPickerOption, right: BranchPickerOption): number {
  const leftRank = left.coveredBy === null ? 0 : 1;
  const rightRank = right.coveredBy === null ? 0 : 1;
  return leftRank === rightRank
    ? left.displayName.localeCompare(right.displayName)
    : leftRank - rightRank;
}
