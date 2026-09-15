import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, map, switchMap, tap } from 'rxjs';
import { apiErrorMessage } from '../api/api-error';
import { GitHealthApiClient } from '../api/git-health-api-client';
import { LocalApiState } from '../api/api.models';

const readFailureMessage = $localize`:@@apiError.localApi.read:The local API access could not be read.`;

const writeFailureMessage = $localize`:@@apiError.localApi.write:The local API access could not be changed.`;

const tokenFailureMessage = $localize`:@@apiError.localApi.token:No new token could be issued.`;

/**
 * State of the port GitHealth answers orchestrators on. The token lives here only between
 * the moment it is issued and the moment the user leaves the screen: it is never read back
 * from the API, because the API no longer holds it.
 */
@Injectable({ providedIn: 'root' })
export class LocalApiStore {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);

  readonly state = signal<LocalApiState | null>(null);
  readonly isBusy = signal(false);
  readonly error = signal<string | null>(null);

  /** Shown once, right after it is issued. Nothing can bring it back afterwards. */
  readonly issuedToken = signal<string | null>(null);

  readonly isListening = computed(() => this.state()?.status === 'Listening');
  readonly address = computed(() => this.state()?.address ?? null);

  load(): void {
    this.api
      .getLocalApi()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (state) => this.state.set(state),
        error: (failure: unknown) => this.fail(failure, readFailureMessage),
      });
  }

  /**
   * Opening an access that has never had a token issues one on the way through, and hands it
   * over in the same gesture: an open port with no secret in front of it must not exist, and
   * asking the user for two clicks to avoid it would only teach them to skip the first.
   */
  apply(isEnabled: boolean, port: number): void {
    if (this.isBusy()) {
      return;
    }

    this.begin();
    this.run(
      isEnabled && this.state()?.hasToken !== true
        ? this.api.issueLocalApiToken().pipe(
            tap((issued) => this.issuedToken.set(issued.token)),
            switchMap(() => this.api.updateLocalApi({ isEnabled, port })),
          )
        : this.api.updateLocalApi({ isEnabled, port }),
      writeFailureMessage,
    );
  }

  /** Issues a token and revokes the previous one, whether the access is open or not. */
  issueToken(): void {
    if (this.isBusy()) {
      return;
    }

    this.begin();
    this.run(
      this.api.issueLocalApiToken().pipe(
        tap((issued) => this.issuedToken.set(issued.token)),
        map((issued) => issued.access),
      ),
      tokenFailureMessage,
    );
  }

  /** Called when the user has copied the token, so it stops being on screen. */
  forgetToken(): void {
    this.issuedToken.set(null);
  }

  private run(source: Observable<LocalApiState>, fallback: string): void {
    source.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (state) => this.settle(state),
      error: (failure: unknown) => this.fail(failure, fallback),
    });
  }

  private begin(): void {
    this.isBusy.set(true);
    this.error.set(null);
  }

  private settle(state: LocalApiState): void {
    this.state.set(state);
    this.isBusy.set(false);
  }

  private fail(failure: unknown, fallback: string): void {
    this.isBusy.set(false);
    this.error.set(apiErrorMessage(failure, fallback));
  }
}
