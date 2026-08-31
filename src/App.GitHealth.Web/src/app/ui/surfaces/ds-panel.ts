import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { IconName } from '../icon-name';
import { DsIcon } from '../core/ds-icon';

const headerGlyphSize = 14;

/** The header of a panel never scrolls with its content. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'etb-panel' },
  imports: [DsIcon],
  selector: 'ds-panel',
  template: `
    <header class="etb-panel__header">
      @if (icon(); as glyph) {
        <ds-icon [name]="glyph" [size]="headerGlyphSize" style="color:var(--text-muted)" />
      }
      <span class="etb-panel__title">{{ title() }}</span>
      <span class="etb-panel__spacer"></span>
      <ng-content select="[panelActions]" />
    </header>
    <div class="etb-panel__body">
      <ng-content />
    </div>
  `,
})
export class DsPanel {
  readonly title = input.required<string>();
  readonly icon = input<IconName | null>(null);

  protected readonly headerGlyphSize = headerGlyphSize;
}
