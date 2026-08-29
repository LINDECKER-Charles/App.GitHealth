import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import {
  Observable,
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  switchMap,
  tap,
} from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { RepositoryValidation } from '../../core/api/api.models';
import { ProjectsStore } from '../../core/workspace/projects-store';
import { ToastService } from '../../core/workspace/toast';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsIconButton } from '../../ui/core/ds-icon-button';
import { DsInput } from '../../ui/forms/ds-input';
import { DsSelect, SelectOption } from '../../ui/forms/ds-select';
import { DsSegmentedControl } from '../../ui/surfaces/ds-segmented-control';
import { DirectoryBrowser } from './directory-browser';
import { displayReference } from '../../core/branches/branch-labels';

const validationDelayMs = 400;
const defaultActiveUntilDays = 30;
const defaultInactiveAfterDays = 90;

const scopeOptions: readonly SelectOption[] = [
  { value: 'refs/*', label: 'Toutes' },
  { value: 'refs/heads/*', label: 'Locales' },
  { value: 'refs/remotes/*', label: 'Suivi distant' },
];

type Validation =
  | { readonly kind: 'idle' }
  | { readonly kind: 'ok'; readonly repository: RepositoryValidation }
  | { readonly kind: 'error'; readonly message: string };

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DirectoryBrowser,
    DsButton,
    DsIcon,
    DsIconButton,
    DsInput,
    DsSegmentedControl,
    DsSelect,
  ],
  selector: 'app-add-repository-dialog',
  styleUrl: './add-repository-dialog.scss',
  templateUrl: './add-repository-dialog.html',
})
export class AddRepositoryDialog {
  private readonly api = inject(GitHealthApiClient);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly store = inject(ProjectsStore);
  readonly close = output<void>();

  protected readonly path = signal('');
  protected readonly displayName = signal('');
  protected readonly referenceName = signal('');
  protected readonly scope = signal('refs/heads/*');
  protected readonly validation = signal<Validation>({ kind: 'idle' });
  protected readonly isValidating = signal(false);
  protected readonly isCreating = signal(false);
  protected readonly isBrowsing = signal(false);
  protected readonly createError = signal<string | null>(null);

  protected readonly scopeOptions = scopeOptions;
  protected readonly defaultActiveUntilDays = defaultActiveUntilDays;
  protected readonly defaultInactiveAfterDays = defaultInactiveAfterDays;

  protected readonly repository = computed(() => {
    const state = this.validation();
    return state.kind === 'ok' ? state.repository : null;
  });

  protected readonly errorMessage = computed(() => {
    const state = this.validation();
    return state.kind === 'error' ? state.message : this.createError();
  });

  protected readonly referenceOptions = computed<readonly SelectOption[]>(
    () =>
      this.repository()?.references.map((reference) => ({
        value: reference,
        label: `${displayReference(reference)} (${reference.startsWith('refs/remotes/') ? 'distante' : 'locale'})`,
      })) ?? [],
  );

  protected readonly canCreate = computed(
    () =>
      this.repository() !== null &&
      this.displayName().trim().length > 0 &&
      this.referenceName().length > 0 &&
      !this.isCreating(),
  );

  constructor() {
    toObservable(this.path)
      .pipe(
        map((value) => value.trim()),
        distinctUntilChanged(),
        tap((value) => this.startValidation(value)),
        debounceTime(validationDelayMs),
        switchMap((value) =>
          value.length === 0 ? of<Validation>({ kind: 'idle' }) : this.check(value),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => this.applyValidation(result));
  }

  protected useDirectory(path: string): void {
    this.isBrowsing.set(false);
    this.path.set(path);
  }

  protected create(): void {
    const repository = this.repository();
    if (repository === null || !this.canCreate()) {
      return;
    }

    this.isCreating.set(true);
    this.createError.set(null);
    this.api
      .createProject({
        displayName: this.displayName().trim(),
        repositoryPath: repository.canonicalPath,
        settings: {
          referenceName: this.referenceName(),
          branchNamespace: this.scope(),
          activeUntilDays: defaultActiveUntilDays,
          inactiveAfterDays: defaultInactiveAfterDays,
          excludedPatterns: [],
          protectedPatterns: [],
        },
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (project) => {
          this.store.upsert(project);
          this.close.emit();
          this.toast.show('Dépôt ajouté · lance la première analyse pour le mesurer');
          void this.router.navigate(['/projects', project.id]);
        },
        error: (error: unknown) => {
          this.isCreating.set(false);
          this.createError.set(apiErrorMessage(error, 'Le dépôt n’a pas pu être enregistré.'));
        },
      });
  }

  private startValidation(value: string): void {
    this.createError.set(null);
    this.validation.set({ kind: 'idle' });
    this.isValidating.set(value.length > 0);
  }

  private check(value: string): Observable<Validation> {
    return this.api.validateRepository(value).pipe(
      map((repository): Validation => ({ kind: 'ok', repository })),
      catchError((error: unknown) =>
        of<Validation>({
          kind: 'error',
          message: apiErrorMessage(error, 'Ce chemin ne peut pas être lu comme un dépôt Git.'),
        }),
      ),
    );
  }

  private applyValidation(result: Validation): void {
    this.isValidating.set(false);
    this.validation.set(result);
    if (result.kind !== 'ok') {
      return;
    }

    this.displayName.set(lastSegment(result.repository.canonicalPath));
    const reference = result.repository.suggestedReference ?? result.repository.references[0] ?? '';
    this.referenceName.set(reference);
    this.scope.set(reference.startsWith('refs/remotes/') ? 'refs/remotes/*' : 'refs/heads/*');
  }
}

function lastSegment(path: string): string {
  const segments = path.replace(/[\\/]+$/, '').split(/[\\/]/);
  return segments.at(-1) ?? path;
}
