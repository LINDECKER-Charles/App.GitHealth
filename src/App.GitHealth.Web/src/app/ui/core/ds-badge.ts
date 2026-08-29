import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { IconName, Tone } from '../icon-name';
import { DsIcon } from './ds-icon';

const badgeIconSize = 11;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'etb-badge',
    '[class.etb-badge--success]': 'tone() === "success"',
    '[class.etb-badge--warning]': 'tone() === "warning"',
    '[class.etb-badge--danger]': 'tone() === "danger"',
    '[class.etb-badge--info]': 'tone() === "info"',
    '[class.etb-badge--brand]': 'tone() === "brand"',
    '[class.etb-badge--merged]': 'tone() === "merged"',
    '[class.etb-badge--solid]': 'solid()',
    '[class.etb-badge--sm]': 'size() === "sm"',
    '[class.etb-badge--mono]': 'mono()',
  },
  imports: [DsIcon],
  selector: 'ds-badge',
  template: `
    @if (icon(); as glyph) {
      <ds-icon [name]="glyph" [size]="iconSize" />
    }
    <ng-content />
  `,
})
export class DsBadge {
  readonly tone = input<Tone>('neutral');
  readonly solid = input(false);
  readonly size = input<'sm' | 'md'>('md');
  readonly mono = input(false);
  readonly icon = input<IconName | null>(null);

  protected readonly iconSize = badgeIconSize;
}
