import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { Tone } from '../icon-name';

/** Dense rows cannot carry a badge: a dot is enough to hold the state. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'etb-dot',
    '[class.etb-dot--success]': 'tone() === "success"',
    '[class.etb-dot--warning]': 'tone() === "warning"',
    '[class.etb-dot--danger]': 'tone() === "danger"',
    '[class.etb-dot--info]': 'tone() === "info"',
    '[class.etb-dot--brand]': 'tone() === "brand"',
    '[class.etb-dot--merged]': 'tone() === "merged"',
    '[style.width.px]': 'size()',
    '[style.height.px]': 'size()',
    '[attr.role]': 'label() ? "img" : "presentation"',
    '[attr.aria-label]': 'label()',
  },
  selector: 'ds-status-dot',
  template: '',
})
export class DsStatusDot {
  readonly tone = input<Tone>('neutral');
  readonly size = input(8);
  readonly label = input<string | null>(null);
}
