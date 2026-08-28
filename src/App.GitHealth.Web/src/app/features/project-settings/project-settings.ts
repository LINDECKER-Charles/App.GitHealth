import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import {
  PolicyPreviewMatch,
  PolicyUpdateRequest,
  ProjectResponse,
} from '../../core/api/api.models';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  selector: 'app-project-settings',
  styleUrl: './project-settings.scss',
  templateUrl: './project-settings.html',
})
export class ProjectSettings {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly location = inject(Location);
  private readonly projectId = inject(ActivatedRoute).snapshot.paramMap.get('projectId') ?? '';

  protected readonly actionError = signal<string | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly loading = signal(true);
  protected readonly matches = signal<readonly PolicyPreviewMatch[]>([]);
  protected readonly previewed = signal(false);
  protected readonly previewLoading = signal(false);
  protected readonly project = signal<ProjectResponse | null>(null);
  protected readonly savedMessage = signal<string | null>(null);
  protected readonly saving = signal(false);

  protected readonly form = new FormGroup(
    {
      activeUntilDays: new FormControl(30, {
        nonNullable: true,
        validators: [Validators.required, Validators.min(0)],
      }),
      inactiveAfterDays: new FormControl(90, {
        nonNullable: true,
        validators: [Validators.required, Validators.min(1)],
      }),
      excludedPatterns: new FormControl('', { nonNullable: true }),
      protectedPatterns: new FormControl('', { nonNullable: true }),
    },
    { validators: thresholdOrderValidator },
  );

  constructor() {
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.matches.set([]);
      this.previewed.set(false);
      this.actionError.set(null);
      this.savedMessage.set(null);
    });
    this.load();
  }

  protected load(): void {
    if (this.projectId.length === 0) {
      this.loading.set(false);
      this.loadError.set('Aucun projet n’a été indiqué.');
      return;
    }

    this.loading.set(true);
    this.loadError.set(null);
    this.api
      .getProject(this.projectId)
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (project) => this.populate(project),
        error: (error: unknown) =>
          this.loadError.set(
            apiErrorMessage(error, 'Les politiques de ce projet ne peuvent pas être chargées.'),
          ),
      });
  }

  protected previewPolicy(): void {
    if (!this.ensureValid() || this.previewLoading()) {
      return;
    }

    this.previewLoading.set(true);
    this.actionError.set(null);
    this.api
      .previewPolicy(this.projectId, this.request())
      .pipe(
        finalize(() => this.previewLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (preview) => {
          this.matches.set(preview.matches);
          this.previewed.set(true);
        },
        error: (error: unknown) =>
          this.actionError.set(apiErrorMessage(error, 'L’aperçu des correspondances a échoué.')),
      });
  }

  protected save(): void {
    if (!this.ensureValid() || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.actionError.set(null);
    this.savedMessage.set(null);
    this.api
      .updatePolicy(this.projectId, this.request())
      .pipe(
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (project) => {
          this.populate(project);
          this.savedMessage.set('Politique enregistrée. Les faits Git sont restés inchangés.');
        },
        error: (error: unknown) =>
          this.actionError.set(apiErrorMessage(error, 'La politique n’a pas pu être enregistrée.')),
      });
  }

  protected goBack(): void {
    this.location.back();
  }

  private populate(project: ProjectResponse): void {
    this.project.set(project);
    this.form.reset(
      {
        activeUntilDays: project.activeUntilDays,
        excludedPatterns: project.excludedPatterns.join('\n'),
        inactiveAfterDays: project.inactiveAfterDays,
        protectedPatterns: project.protectedPatterns.join('\n'),
      },
      { emitEvent: false },
    );
  }

  private ensureValid(): boolean {
    this.form.markAllAsTouched();
    if (this.form.valid) {
      return true;
    }

    this.actionError.set('Corrigez les seuils avant de continuer.');
    return false;
  }

  private request(): PolicyUpdateRequest {
    const value = this.form.getRawValue();
    return {
      activeUntilDays: value.activeUntilDays,
      excludedPatterns: parsePatterns(value.excludedPatterns),
      inactiveAfterDays: value.inactiveAfterDays,
      protectedPatterns: parsePatterns(value.protectedPatterns),
    };
  }
}

function thresholdOrderValidator(control: AbstractControl): ValidationErrors | null {
  const active = control.get('activeUntilDays')?.value;
  const inactive = control.get('inactiveAfterDays')?.value;
  if (typeof active !== 'number' || typeof inactive !== 'number') {
    return null;
  }

  return inactive > active ? null : { thresholdOrder: true };
}

function parsePatterns(value: string): readonly string[] {
  return Array.from(
    new Set(
      value
        .split(/\r?\n/)
        .map((pattern) => pattern.trim())
        .filter((pattern) => pattern.length > 0),
    ),
  );
}
