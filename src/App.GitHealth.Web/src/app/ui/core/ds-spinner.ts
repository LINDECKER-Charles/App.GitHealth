import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

const thinStrokeMaximumSize = 14;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'etb-spinner',
    role: 'status',
    '[attr.aria-label]': 'label()',
    '[style.width.px]': 'size()',
    '[style.height.px]': 'size()',
    '[style.borderWidth.px]': 'strokeWidth()',
  },
  selector: 'ds-spinner',
  template: '',
})
export class DsSpinner {
  readonly size = input(14);
  readonly label = input('Chargement');

  protected readonly strokeWidth = computed(() => (this.size() <= thinStrokeMaximumSize ? 2 : 2.5));
}
