import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { ProjectResponse } from '../../core/api/api.models';
import { pluralMessage } from '../../core/i18n/plural-message';
import { ProjectRemover } from '../../core/workspace/project-remover';
import { ProjectsStore } from '../../core/workspace/projects-store';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsIconButton } from '../../ui/core/ds-icon-button';

/** One page of history is enough: only its total is read. */
const countingPageSize = 1;

/**
 * Single-step confirmation: the repository on disk is untouched, and a full database export is
 * one click away in the top bar, so a typed-name challenge would only add ceremony.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'close.emit()' },
  imports: [DsButton, DsIcon, DsIconButton],
  selector: 'app-project-delete-dialog',
  styleUrl: './project-delete-dialog.scss',
  templateUrl: './project-delete-dialog.html',
})
export class ProjectDeleteDialog {
  private readonly api = inject(GitHealthApiClient);
  private readonly remover = inject(ProjectRemover);
  private readonly store = inject(ProjectsStore);
  private readonly destroyRef = inject(DestroyRef);

  readonly projectId = input.required<string>();
  readonly close = output<void>();

  /** `null` while the count is unknown: the sentence has to stay true meanwhile. */
  private readonly captureCount = signal<number | null>(null);

  protected readonly project = computed<ProjectResponse | null>(
    () => this.store.projects().find((candidate) => candidate.id === this.projectId()) ?? null,
  );

  protected readonly captureLabel = computed(() => captureMessage(this.captureCount()));

  constructor() {
    effect(() => this.countCaptures(this.projectId()));
  }

  protected confirm(): void {
    this.remover.remove(this.projectId());
    this.close.emit();
  }

  private countCaptures(projectId: string): void {
    this.api
      .getAnalysisHistory(projectId, countingPageSize)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (history) => this.captureCount.set(history.totalCount),
        error: () => this.captureCount.set(null),
      });
  }
}

/** Each count carries its whole sentence: word order around a number is not universal. */
function captureMessage(count: number | null): string {
  if (count === null) {
    return $localize`:@@projectDelete.captures.unknown:Every capture saved for this repository goes with it.`;
  }

  if (count === 0) {
    return $localize`:@@projectDelete.captures.none:This repository has no saved capture.`;
  }

  return pluralMessage(count, {
    one: $localize`:@@projectDelete.captures.one:${count}:count: saved capture goes with it.`,
    other: $localize`:@@projectDelete.captures.many:${count}:count: saved captures go with it.`,
  });
}
