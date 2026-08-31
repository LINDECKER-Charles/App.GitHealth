import { displayReference } from '../../../core/branches/branch-labels';
import { matchPattern } from '../../../core/branches/branch-policy';
import { sourceLocale } from '../../../core/i18n/locale';

const nameCollator = new Intl.Collator(sourceLocale);

/** The two pattern lists of a policy, told apart by the picker dialog. */
export type BranchPatternKind = 'protected' | 'excluded';

export interface BranchPickerOption {
  readonly referenceName: string;
  readonly displayName: string;
  readonly coveredBy: string | null;
}

/**
 * A reference already covered stays visible but moves to the end of the list: the reader
 * sees the pattern that catches it instead of hunting for it among the tickable boxes.
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

/** The short name searches for "release", the full name for "refs/remotes". */
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
    ? nameCollator.compare(left.displayName, right.displayName)
    : leftRank - rightRank;
}
