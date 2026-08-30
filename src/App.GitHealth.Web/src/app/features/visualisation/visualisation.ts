import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { SelectOption } from '../../ui/forms/ds-select';
import { DsSegmentedControl } from '../../ui/surfaces/ds-segmented-control';

type SubViewId = 'topologie' | 'registre' | 'ecart';

const subViews: readonly SelectOption[] = [
  { value: 'topologie', label: 'Plan de topologie' },
  { value: 'registre', label: "Registre d'activité" },
  { value: 'ecart', label: 'Écart entre captures' },
];

const defaultSubView: SubViewId = 'topologie';

/**
 * Cadre de l'onglet Visualisation : trois lectures d'une même capture, chacune adressable
 * par son URL, commutées par un segmented control. Quelle capture elles montrent se choisit
 * dans l'en-tête du dépôt, qui vaut pour tous les onglets.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsSegmentedControl, RouterOutlet],
  selector: 'app-visualisation',
  styleUrl: './visualisation.scss',
  templateUrl: './visualisation.html',
})
export class Visualisation {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  protected readonly subViews = subViews;
  protected readonly active = computed<SubViewId>(() => subViewFromUrl(this.url()));

  protected open(subView: string): void {
    void this.router.navigate([subView], {
      queryParamsHandling: 'preserve',
      relativeTo: this.route,
    });
  }
}

function subViewFromUrl(url: string): SubViewId {
  const match = subViews.find(({ value }) => url.includes(`/${value}`));
  return (match?.value as SubViewId | undefined) ?? defaultSubView;
}
