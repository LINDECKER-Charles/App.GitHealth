import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { apiErrorMessage } from '../api/api-error';
import { GitHealthApiClient } from '../api/git-health-api-client';
import { UpdateStatus } from '../api/api.models';

const applyFailureMessage = $localize`:@@apiError.update.apply:The update could not be applied.`;

const notReadyMessage = $localize`:@@apiError.update.notReady:No update could be downloaded. The release source may be unreachable.`;

/**
 * State of the application updates. Outside a managed installation — Docker, browser
 * mode, Linux — the API answers "unsupported" and nothing is shown.
 */
@Injectable({ providedIn: 'root' })
export class UpdateStore {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);

  readonly status = signal<UpdateStatus | null>(null);
  readonly isApplying = signal(false);
  readonly error = signal<string | null>(null);

  readonly isAvailable = computed(() => this.status()?.availability === 'Available');
  readonly availableVersion = computed(() => this.status()?.availableVersion ?? null);

  load(): void {
    this.api
      .getUpdateStatus()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (status) => this.status.set(status),
        // An unavailable update is not a failure: the button simply stays absent.
        error: () => this.status.set(null),
      });
  }

  apply(): void {
    if (this.isApplying() || !this.isAvailable()) {
      return;
    }

    this.isApplying.set(true);
    this.error.set(null);
    this.api
      .applyUpdate()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (status) => this.settle(status),
        error: (failure: unknown) => this.fail(apiErrorMessage(failure, applyFailureMessage)),
      });
  }

  /**
   * The host accepts with no body when it is about to restart the application: this page
   * then does not survive the call. A status in the response means the opposite — nothing
   * was applicable, and it says why.
   */
  private settle(status: UpdateStatus | null): void {
    if (status === null) {
      return;
    }

    this.status.set(status);
    this.isApplying.set(false);
    this.error.set(notReadyMessage);
  }

  private fail(message: string): void {
    this.isApplying.set(false);
    this.error.set(message);
  }
}
