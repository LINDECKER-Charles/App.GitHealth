import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { GitHealthApiClient } from '../api/git-health-api-client';
import { UpdateStatus } from '../api/api.models';

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
    this.api
      .applyUpdate()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        // Succès : l'hôte relance l'application, cette page ne survit pas à l'appel.
        error: () => this.isApplying.set(false),
      });
  }
}
