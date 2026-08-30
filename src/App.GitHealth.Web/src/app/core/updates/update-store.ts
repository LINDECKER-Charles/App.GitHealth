import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { apiErrorMessage } from '../api/api-error';
import { GitHealthApiClient } from '../api/git-health-api-client';
import { UpdateStatus } from '../api/api.models';

const applyFailureMessage = 'La mise à jour n’a pas pu être appliquée.';

const notReadyMessage =
  'Aucune mise à jour n’a pu être téléchargée. La source des releases est peut-être ' +
  'injoignable.';

/**
 * État des mises à jour de l'application. Hors installation gérée — Docker, mode
 * navigateur, Linux — l'API répond « non pris en charge » et rien ne s'affiche.
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
        // Une mise à jour indisponible n'est pas une panne : le bouton reste absent.
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
   * L'hôte accepte sans corps quand il va relancer l'application : cette page ne survit
   * alors pas à l'appel. Un statut en réponse signifie l'inverse — rien n'était
   * applicable, et il dit pourquoi.
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
