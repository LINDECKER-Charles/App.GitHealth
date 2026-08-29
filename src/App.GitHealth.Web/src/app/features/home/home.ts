import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import {
  CreateProjectRequest,
  DirectoryListing,
  Project,
  RepositoryValidation,
  RuntimeInfo,
} from '../../core/api/api.models';
import { apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';

interface BranchNamespaceOption {
  readonly label: string;
  readonly value: string;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  selector: 'app-home',
  styleUrls: ['./home.scss', './home-projects.scss', './home-directory.scss'],
  templateUrl: './home.html',
})
export class Home {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly directoryDialog = viewChild<ElementRef<HTMLDialogElement>>('directoryDialog');

  protected readonly branchNamespaces: readonly BranchNamespaceOption[] = [
    { label: 'Branches locales', value: 'refs/heads/*' },
    { label: 'Branches distantes observées', value: 'refs/remotes/*' },
  ];
  protected readonly creating = signal(false);
  protected readonly directoryError = signal<string | null>(null);
  protected readonly directoryListing = signal<DirectoryListing | null>(null);
  protected readonly directoryLoading = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly projects = signal<readonly Project[]>([]);
  protected readonly repository = signal<RepositoryValidation | null>(null);
  protected readonly runtime = signal<RuntimeInfo | null>(null);
  protected readonly validating = signal(false);

  protected readonly pathControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required],
  });
  protected readonly detailsForm = new FormGroup({
    displayName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    referenceName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    branchNamespace: new FormControl('refs/heads/*', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  constructor() {
    this.loadProjects();
  }

  protected loadProjects(): void {
    this.loading.set(true);
    this.loadError.set(null);
    forkJoin({
      projects: this.api.listProjects(),
      runtime: this.api.getRuntime(),
    })
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: ({ projects, runtime }) => {
          this.projects.set(projects);
          this.configureRuntime(runtime);
        },
        error: (error: unknown) =>
          this.loadError.set(
            apiErrorMessage(
              error,
              'Impossible de charger les projets. Vérifiez que GitHealth est démarré.',
            ),
          ),
      });
  }

  protected validatePath(): void {
    this.pathControl.markAsTouched();
    if (this.pathControl.invalid || this.validating()) {
      return;
    }

    this.formError.set(null);
    this.validating.set(true);
    this.api
      .validateRepository(this.pathControl.value.trim())
      .pipe(
        finalize(() => this.validating.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (repository) => this.prepareProject(repository),
        error: (error: unknown) =>
          this.formError.set(
            apiErrorMessage(error, 'Ce chemin ne peut pas être validé comme dépôt Git.'),
          ),
      });
  }

  protected createProject(): void {
    this.detailsForm.markAllAsTouched();
    const repository = this.repository();
    if (repository === null || this.detailsForm.invalid || this.creating()) {
      return;
    }

    this.formError.set(null);
    this.creating.set(true);
    this.api
      .createProject(this.buildRequest(repository))
      .pipe(
        finalize(() => this.creating.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (project) => void this.router.navigate(['/projects', project.id]),
        error: (error: unknown) =>
          this.formError.set(apiErrorMessage(error, 'Le projet n’a pas pu être enregistré.')),
      });
  }

  protected openDirectoryBrowser(): void {
    const dialog = this.directoryDialog()?.nativeElement;
    if (dialog !== undefined && !dialog.open) {
      if (typeof dialog.showModal === 'function') {
        dialog.showModal();
      } else {
        dialog.setAttribute('open', '');
      }
    }

    this.directoryListing.set(null);
    this.browseDirectory(null);
  }

  protected browseDirectory(path: string | null): void {
    if (this.directoryLoading()) {
      return;
    }

    this.directoryError.set(null);
    this.directoryLoading.set(true);
    this.api
      .browseDirectories(path)
      .pipe(
        finalize(() => this.directoryLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (listing) => this.directoryListing.set(listing),
        error: (error: unknown) =>
          this.directoryError.set(apiErrorMessage(error, 'Ce dossier ne peut pas être parcouru.')),
      });
  }

  protected selectCurrentDirectory(): void {
    const path = this.directoryListing()?.currentPath;
    if (path === null || path === undefined) {
      return;
    }

    this.pathControl.setValue(path);
    this.pathControl.markAsTouched();
    this.editPath();
    this.closeDirectoryBrowser();
  }

  protected closeDirectoryBrowser(): void {
    const dialog = this.directoryDialog()?.nativeElement;
    if (dialog === undefined) {
      return;
    }

    if (typeof dialog.close === 'function') {
      dialog.close();
    } else {
      dialog.removeAttribute('open');
    }
  }

  protected editPath(): void {
    this.repository.set(null);
    this.formError.set(null);
  }

  protected pathChanged(): void {
    this.formError.set(null);
    if (this.repository() !== null) {
      this.repository.set(null);
    }
  }

  protected referenceLabel(referenceName: string | null): string {
    if (referenceName === null) {
      return 'Référence à choisir';
    }

    return referenceName.replace('refs/heads/', '').replace('refs/remotes/', '');
  }

  private prepareProject(repository: RepositoryValidation): void {
    const referenceName = repository.suggestedReference ?? repository.references[0] ?? '';
    this.repository.set(repository);
    this.detailsForm.setValue({
      branchNamespace: this.defaultNamespace(referenceName),
      displayName: this.defaultProjectName(repository.canonicalPath),
      referenceName,
    });
  }

  private configureRuntime(runtime: RuntimeInfo): void {
    this.runtime.set(runtime);
    if (runtime.initialRepositoryPath === null || this.pathControl.value.length > 0) {
      return;
    }

    this.pathControl.setValue(runtime.initialRepositoryPath);
    this.validatePath();
  }

  private buildRequest(repository: RepositoryValidation): CreateProjectRequest {
    const value = this.detailsForm.getRawValue();
    return {
      displayName: value.displayName.trim(),
      repositoryPath: repository.canonicalPath,
      settings: {
        activeUntilDays: 30,
        branchNamespace: value.branchNamespace,
        excludedPatterns: [],
        inactiveAfterDays: 90,
        protectedPatterns: [],
        referenceName: value.referenceName,
      },
    };
  }

  private defaultNamespace(referenceName: string): string {
    return referenceName.startsWith('refs/remotes/') ? 'refs/remotes/*' : 'refs/heads/*';
  }

  private defaultProjectName(path: string): string {
    const segments = path.replace(/[\\/]+$/, '').split(/[\\/]/);
    return segments.at(-1) ?? path;
  }
}
