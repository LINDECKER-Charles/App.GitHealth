import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Params, Router } from '@angular/router';
import { displayReference } from '../../../core/branches/branch-labels';
import { SelectOption } from '../../../ui/forms/ds-select';
import { baselineQueryParam } from './baseline-history';
import { captureQueryParam } from '../capture-history';
import { ProjectContext } from '../project-context';

/**
 * Which baseline the repository is measured against, for all of its views at once. A project
 * declares an ordered list of them and each one keeps its own analysis history, so switching
 * baseline changes both the branches shown and the captures they can be compared with.
 */
@Injectable({ providedIn: 'root' })
export class BaselineStore {
  private readonly context = inject(ProjectContext);
  private readonly router = inject(Router);

  private readonly queryParams = toSignal(this.router.routerState.root.queryParams, {
    initialValue: this.router.routerState.root.snapshot.queryParams as Params,
  });

  /** `null` means the primary baseline: it needs no parameter, which keeps its links short. */
  readonly requested = computed<string | null>(
    () => this.queryParams()[baselineQueryParam] ?? null,
  );

  readonly selected = computed(
    () => this.requested() ?? this.context.project()?.referenceName ?? '',
  );

  readonly options = computed<readonly SelectOption[]>(() =>
    (this.context.project()?.referenceNames ?? []).map((reference) => ({
      value: reference,
      label: displayReference(reference),
    })),
  );

  /** A single baseline is no choice at all: the header states it instead of offering it. */
  readonly hasChoice = computed(() => this.options().length > 1);

  /** URL parameters leading to the baseline being read, for the links that must keep it. */
  baselineLink(): Params {
    const requested = this.requested();
    return requested === null ? {} : { [baselineQueryParam]: requested };
  }

  /**
   * Coming back to the primary baseline releases the parameter, otherwise two links would
   * point at the same view. A capture belongs to the history of one baseline alone: it is
   * dropped with the switch rather than read against another reference.
   */
  select(reference: string): void {
    const primary = this.context.project()?.referenceName ?? null;
    const value = reference === primary ? null : reference;
    if (value === this.requested()) {
      return;
    }

    void this.router.navigate([], {
      queryParams: { [baselineQueryParam]: value, [captureQueryParam]: null },
      queryParamsHandling: 'merge',
    });
  }
}
