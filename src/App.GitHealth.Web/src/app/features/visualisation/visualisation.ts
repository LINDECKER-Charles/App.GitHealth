import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { SelectOption } from '../../ui/forms/ds-select';
import { DsSegmentedControl } from '../../ui/surfaces/ds-segmented-control';

/** The members are URL segments, not prose: they stay exactly as the routes spell them. */
type SubViewId = 'topology' | 'register' | 'drift';

const subViews: readonly SelectOption[] = [
  { value: 'topology', label: $localize`:@@visualisation.subView.topology:Topology map` },
  { value: 'register', label: $localize`:@@visualisation.subView.activity:Activity register` },
  { value: 'drift', label: $localize`:@@visualisation.subView.drift:Drift between captures` },
];

const defaultSubView: SubViewId = 'topology';

/**
 * Frame of the Visualisation tab: three readings of one capture, each addressable by its own
 * URL, switched by a segmented control. Which capture they show is chosen in the repository
 * header, which holds for every tab.
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
