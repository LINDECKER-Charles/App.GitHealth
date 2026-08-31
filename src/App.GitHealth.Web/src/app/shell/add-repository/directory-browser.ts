import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { DirectoryListing } from '../../core/api/api.models';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';

/** Browses the folders the server can read, so a path can be set without typing it. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsButton, DsIcon],
  selector: 'app-directory-browser',
  styleUrl: './directory-browser.scss',
  templateUrl: './directory-browser.html',
})
export class DirectoryBrowser {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);

  readonly selected = output<string>();
  readonly cancelled = output<void>();

  protected readonly listing = signal<DirectoryListing | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.browse(null);
  }

  protected browse(path: string | null): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.api
      .browseDirectories(path)
      .pipe(
        finalize(() => this.isLoading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (listing) => this.listing.set(listing),
        error: (error: unknown) =>
          this.error.set(
            apiErrorMessage(
              error,
              $localize`:@@addRepository.browser.error:This folder cannot be browsed.`,
            ),
          ),
      });
  }

  protected useCurrent(): void {
    const current = this.listing()?.currentPath;
    if (current !== undefined) {
      this.selected.emit(current);
    }
  }
}
