import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { IconName } from '../icon-name';
import { DsIcon } from '../core/ds-icon';

const emptyGlyphSize = 18;

/** A fact, then the rule that fills it. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'etb-empty' },
  imports: [DsIcon],
  selector: 'ds-empty-state',
  template: `
    <span class="etb-empty__icon">
      <ds-icon [name]="icon()" [size]="emptyGlyphSize" />
    </span>
    <div class="etb-empty__title">{{ title() }}</div>
    @if (description(); as text) {
      <p class="etb-empty__desc">{{ text }}</p>
    }
    <div style="margin-top:var(--space-2)"><ng-content /></div>
  `,
})
export class DsEmptyState {
  readonly icon = input<IconName>('folder');
  readonly title = input.required<string>();
  readonly description = input<string | null>(null);

  protected readonly emptyGlyphSize = emptyGlyphSize;
}
