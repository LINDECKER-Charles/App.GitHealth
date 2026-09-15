import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../../core/api/api-error';
import { GitHealthApiClient } from '../../../core/api/git-health-api-client';
import { ScanScheduleRequest, ScanScheduleResponse, Uuid } from '../../../core/api/api.models';
import { ToastService } from '../../../core/workspace/toast';

const loadFailureMessage = $localize`:@@apiError.schedule.load:The schedule could not be read.`;
const saveFailureMessage = $localize`:@@apiError.schedule.save:The schedule could not be saved.`;

/**
 * The schedule of the repository being looked at. It is read from the API rather than from the
 * project payload because the next firing is computed at the moment of the read: a value that
 * stale-dates the instant it is cached is better asked for than carried.
 */
@Injectable({ providedIn: 'root' })
export class ScheduleStore {
  private readonly api = inject(GitHealthApiClient);
  private readonly toasts = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly schedule = signal<ScanScheduleResponse | null>(null);
  readonly isSaving = signal(false);
  readonly error = signal<string | null>(null);

  readonly isEnabled = computed(() => this.schedule()?.isEnabled ?? false);

  /** True only while the schedule is on and the installation lets schedules fire. */
  readonly isFiring = computed(() => {
    const schedule = this.schedule();
    return schedule !== null && schedule.isEnabled && schedule.isSchedulerRunning;
  });

  load(projectId: Uuid): void {
    if (projectId.length === 0) {
      return;
    }

    this.error.set(null);
    this.api
      .getSchedule(projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (schedule) => this.schedule.set(schedule),
        error: (error: unknown) => this.fail(error, loadFailureMessage),
      });
  }

  save(projectId: Uuid, request: ScanScheduleRequest, message: string): void {
    this.isSaving.set(true);
    this.error.set(null);
    this.api
      .updateSchedule(projectId, request)
      .pipe(
        finalize(() => this.isSaving.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (schedule) => {
          this.schedule.set(schedule);
          this.toasts.show(message);
        },
        error: (error: unknown) => this.fail(error, saveFailureMessage),
      });
  }

  private fail(error: unknown, fallback: string): void {
    this.error.set(apiErrorMessage(error, fallback));
  }
}
