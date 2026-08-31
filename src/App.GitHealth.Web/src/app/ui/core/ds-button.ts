import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { IconName } from '../icon-name';
import { DsIcon } from './ds-icon';
import { DsSpinner } from './ds-spinner';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ControlSize = 'sm' | 'md' | 'lg';

const largeGlyphSize = 16;
const defaultGlyphSize = 14;

/**
 * Applies straight to a `<button>`: no wrapping element, so the grids and the `flex`
 * layouts of the system keep their exact metrics.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'etb-btn',
    '[class.etb-btn--primary]': 'variant() === "primary"',
    '[class.etb-btn--secondary]': 'variant() === "secondary"',
    '[class.etb-btn--ghost]': 'variant() === "ghost"',
    '[class.etb-btn--danger]': 'variant() === "danger"',
    '[class.etb-btn--sm]': 'size() === "sm"',
    '[class.etb-btn--lg]': 'size() === "lg"',
    '[class.etb-btn--block]': 'block()',
    '[disabled]': 'disabled() || loading()',
  },
  imports: [DsIcon, DsSpinner],
  selector: 'button[dsButton]',
  template: `
    @if (loading()) {
      <ds-spinner [size]="glyphSize()" />
    } @else if (iconLeft(); as glyph) {
      <ds-icon [name]="glyph" [size]="glyphSize()" />
    }
    <ng-content />
    @if (iconRight(); as glyph) {
      <ds-icon [name]="glyph" [size]="glyphSize()" />
    }
  `,
})
export class DsButton {
  readonly variant = input<ButtonVariant>('secondary');
  readonly size = input<ControlSize>('md');
  readonly iconLeft = input<IconName | null>(null);
  readonly iconRight = input<IconName | null>(null);
  readonly block = input(false);
  readonly loading = input(false);
  readonly disabled = input(false);

  protected readonly glyphSize = computed(() =>
    this.size() === 'lg' ? largeGlyphSize : defaultGlyphSize,
  );
}
