import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { IconName, Tone } from '../icon-name';
import { DsIcon } from '../core/ds-icon';

export type CalloutTone = Extract<Tone, 'info' | 'success' | 'warning' | 'danger' | 'neutral'>;

const toneIcons: Readonly<Record<CalloutTone, IconName>> = {
  info: 'info',
  success: 'circle-check',
  warning: 'triangle-alert',
  danger: 'circle-alert',
  neutral: 'info',
};

const calloutGlyphSize = 15;

/** A callout has a tinted background and a full border: never a coloured side bar alone. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'etb-callout',
    '[class.etb-callout--success]': 'tone() === "success"',
    '[class.etb-callout--warning]': 'tone() === "warning"',
    '[class.etb-callout--danger]': 'tone() === "danger"',
    '[class.etb-callout--neutral]': 'tone() === "neutral"',
  },
  imports: [DsIcon],
  selector: 'ds-callout',
  template: `
    <ds-icon class="etb-callout__icon" [name]="glyph()" [size]="calloutGlyphSize" />
    <div>
      @if (title(); as heading) {
        <div class="etb-callout__title">{{ heading }}</div>
      }
      <div><ng-content /></div>
    </div>
  `,
})
export class DsCallout {
  readonly tone = input<CalloutTone>('info');
  readonly title = input<string | null>(null);

  protected readonly calloutGlyphSize = calloutGlyphSize;
  protected readonly glyph = computed(() => toneIcons[this.tone()]);
}
