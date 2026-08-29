import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { ProjectResponse } from '../../core/api/api.models';

const maximumPathLength = 32_768;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  selector: 'app-repository-relocation',
  styleUrl: './repository-relocation.scss',
  templateUrl: './repository-relocation.html',
})
export class RepositoryRelocation {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);

  readonly project = input.required<ProjectResponse>();
  readonly relocated = output<ProjectResponse>();

  protected readonly error = signal<string | null>(null);
  protected readonly repositoryPath = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(maximumPathLength)],
  });
  protected readonly relocating = signal(false);
  protected readonly success = signal<string | null>(null);

  protected relocate(): void {
    this.repositoryPath.markAsTouched();
    if (this.repositoryPath.invalid || this.relocating()) {
      return;
    }

    const repositoryPath = this.repositoryPath.getRawValue().trim();
    if (repositoryPath === this.project().repositoryPath) {
      this.error.set('Indiquez le nouveau chemin du dépôt.');
      return;
    }

    this.relocating.set(true);
    this.error.set(null);
    this.success.set(null);
    this.api
      .relocateProject(this.project().id, { repositoryPath })
      .pipe(
        finalize(() => this.relocating.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (project) => this.handleSuccess(project),
        error: (error: unknown) =>
          this.error.set(apiErrorMessage(error, 'Le dépôt n’a pas pu être relocalisé.')),
      });
  }

  private handleSuccess(project: ProjectResponse): void {
    this.repositoryPath.reset('');
    this.success.set('Dépôt relocalisé. Les analyses précédentes sont conservées.');
    this.relocated.emit(project);
  }
}
