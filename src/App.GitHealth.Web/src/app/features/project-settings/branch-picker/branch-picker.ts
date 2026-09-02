import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  viewChild,
  viewChildren,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { GitHealthApiClient } from '../../../core/api/git-health-api-client';
import { pluralMessage } from '../../../core/i18n/plural-message';
import { DsBadge } from '../../../ui/core/ds-badge';
import { DsButton } from '../../../ui/core/ds-button';
import { DsIcon } from '../../../ui/core/ds-icon';
import { DsIconButton } from '../../../ui/core/ds-icon-button';
import { DsSpinner } from '../../../ui/core/ds-spinner';
import { DsCheckbox } from '../../../ui/forms/ds-checkbox';
import { DsCallout } from '../../../ui/surfaces/ds-callout';
import { BranchPatternKind, BranchPickerOption, buildBranchOptions } from './branch-picker-options';

const kindSubtitles: Readonly<Record<BranchPatternKind, string>> = {
  protected: $localize`:@@branchPicker.kind.protected:Protected pattern`,
  excluded: $localize`:@@branchPicker.kind.excluded:Exclusion pattern`,
  baseline: $localize`:@@branchPicker.kind.baseline:Comparison baseline`,
};

/**
 * The choice only feeds one field of the policy form: the dialog stays local rather than
 * going through the global surface service to rewrite a draft.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'close.emit()' },
  imports: [DsBadge, DsButton, DsCallout, DsCheckbox, DsIcon, DsIconButton, DsSpinner],
  selector: 'app-branch-picker',
  styleUrl: './branch-picker.scss',
  templateUrl: './branch-picker.html',
})
export class BranchPicker {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly rowElements = viewChildren<ElementRef<HTMLElement>>('row');
  private readonly searchField = viewChild.required<ElementRef<HTMLInputElement>>('search');

  readonly kind = input.required<BranchPatternKind>();
  readonly patterns = input.required<readonly string[]>();
  readonly repositoryPath = input.required<string>();
  readonly fallbackReferences = input.required<readonly string[]>();
  readonly close = output<void>();
  readonly confirm = output<readonly string[]>();

  private readonly references = signal<readonly string[]>([]);
  private readonly selection = signal<ReadonlySet<string>>(new Set<string>());

  protected readonly query = signal('');
  protected readonly highlighted = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly isFromLastCapture = signal(false);

  protected readonly subtitle = computed(() => kindSubtitles[this.kind()]);
  protected readonly options = computed<readonly BranchPickerOption[]>(() =>
    buildBranchOptions(this.references(), this.patterns(), this.query()),
  );
  protected readonly selectedCount = computed(() => this.selection().size);
  protected readonly confirmLabel = computed(() => confirmMessage(this.selectedCount()));

  constructor() {
    afterNextRender(() => this.searchField().nativeElement.focus());
    effect(() => this.revealHighlighted());
    effect(() => this.load(this.repositoryPath()));
  }

  protected onQuery(value: string): void {
    this.query.set(value);
    this.highlighted.set(0);
  }

  protected move(offset: number): void {
    const count = this.options().length;
    if (count > 0) {
      this.highlighted.update((index) => (index + offset + count) % count);
    }
  }

  protected toggleHighlighted(): void {
    const option = this.options()[this.highlighted()];
    if (option !== undefined) {
      this.toggle(option, !this.isSelected(option.referenceName));
    }
  }

  protected toggle(option: BranchPickerOption, isChecked: boolean): void {
    if (option.coveredBy !== null) {
      return;
    }

    this.selection.update((current) => {
      const next = new Set(current);
      return applySelection(next, option.referenceName, isChecked);
    });
  }

  protected isSelected(referenceName: string): boolean {
    return this.selection().has(referenceName);
  }

  protected submit(): void {
    if (this.selectedCount() > 0) {
      this.confirm.emit([...this.selection()]);
    }
  }

  /** The list can hold hundreds of references: the keyboard cursor must stay visible. */
  private revealHighlighted(): void {
    this.rowElements()[this.highlighted()]?.nativeElement.scrollIntoView({ block: 'nearest' });
  }

  private load(repositoryPath: string): void {
    this.api
      .validateRepository(repositoryPath)
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (repository) => this.references.set(repository.references),
        error: () => this.useLastCapture(),
      });
  }

  /** Repository unreachable: the last capture stays usable, provided it is announced. */
  private useLastCapture(): void {
    this.isFromLastCapture.set(true);
    this.references.set(this.fallbackReferences());
  }
}

/** Each count carries its whole sentence: word order around a number is not universal. */
function confirmMessage(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@branchPicker.confirmOne:Add ${count}:count: pattern`,
    other: $localize`:@@branchPicker.confirmMany:Add ${count}:count: patterns`,
  });
}

function applySelection(
  selection: Set<string>,
  referenceName: string,
  isChecked: boolean,
): ReadonlySet<string> {
  if (isChecked) {
    selection.add(referenceName);
  } else {
    selection.delete(referenceName);
  }

  return selection;
}
