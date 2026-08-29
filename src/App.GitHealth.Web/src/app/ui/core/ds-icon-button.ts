import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { IconName } from '../icon-name';
import { DsIcon } from './ds-icon';

const smallGlyphSize = 14;
const defaultGlyphSize = 16;

/** Dans une barre d'outils, une icône seule exige toujours un `label` : infobulle et nom accessible. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'etb-iconbtn',
    type: 'button',
    '[class.etb-iconbtn--sm]': 'size() === "sm"',
    '[class.etb-iconbtn--bordered]': 'variant() === "bordered"',
    '[class.etb-iconbtn--active]': 'active()',
    '[attr.aria-label]': 'label()',
    '[attr.aria-pressed]': 'active() ? true : null',
    '[attr.title]': 'label()',
    '[disabled]': 'disabled()',
  },
  imports: [DsIcon],
  selector: 'button[dsIconButton]',
  template: '<ds-icon [name]="icon()" [size]="glyphSize()" />',
})
export class DsIconButton {
  readonly icon = input.required<IconName>();
  readonly label = input.required<string>();
  readonly size = input<'sm' | 'md'>('md');
  readonly variant = input<'ghost' | 'bordered'>('ghost');
  readonly active = input(false);
  readonly disabled = input(false);

  protected readonly glyphSize = computed(() =>
    this.size() === 'sm' ? smallGlyphSize : defaultGlyphSize,
  );
}
