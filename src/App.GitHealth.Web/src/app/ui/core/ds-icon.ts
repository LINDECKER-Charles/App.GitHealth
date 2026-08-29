import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { IconName } from '../icon-name';

const iconRoot = '/ds/icons';

/**
 * Applique le SVG en `mask-image` : le glyphe prend toujours `currentColor`.
 * On ne colore jamais une icône directement, on colore son parent.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'etb-icon',
    '[class.etb-icon--spin]': 'spin()',
    '[style.width.px]': 'size()',
    '[style.height.px]': 'size()',
    '[style.mask-image]': 'mask()',
    '[attr.role]': 'label() ? "img" : "presentation"',
    '[attr.aria-label]': 'label()',
    '[attr.aria-hidden]': 'label() ? null : "true"',
  },
  selector: 'ds-icon',
  template: '',
})
export class DsIcon {
  readonly name = input.required<IconName>();
  readonly size = input(16);
  readonly spin = input(false);
  readonly label = input<string | null>(null);

  protected readonly mask = computed(() => `url("${iconRoot}/${this.name()}.svg")`);
}
