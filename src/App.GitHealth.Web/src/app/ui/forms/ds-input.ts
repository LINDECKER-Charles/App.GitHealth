import { ChangeDetectionStrategy, Component, input, model, output } from '@angular/core';
import { IconName } from '../icon-name';
import { DsIcon } from '../core/ds-icon';
import { ControlSize } from '../core/ds-button';

const affixGlyphSize = 14;

/** Text field of the system: it sinks in (inner shadow) where the buttons stand out. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'etb-input',
    '[class.etb-input--sm]': 'size() === "sm"',
    '[class.etb-input--lg]': 'size() === "lg"',
    '[class.etb-input--mono]': 'mono()',
    '[class.etb-input--invalid]': 'invalid()',
    '[class.etb-input--disabled]': 'disabled()',
  },
  imports: [DsIcon],
  selector: 'ds-input',
  template: `
    @if (iconLeft(); as glyph) {
      <ds-icon class="etb-input__affix" [name]="glyph" [size]="affixGlyphSize" />
    }
    <input
      class="etb-input__control"
      [attr.id]="inputId()"
      [attr.name]="name()"
      [attr.type]="type()"
      [attr.inputmode]="inputMode()"
      [attr.min]="min()"
      [attr.placeholder]="placeholder()"
      [attr.aria-label]="ariaLabel()"
      [attr.aria-invalid]="invalid() ? true : null"
      [attr.autocomplete]="autocomplete()"
      [attr.spellcheck]="false"
      [disabled]="disabled()"
      [value]="value()"
      (input)="onInput($event)"
      (keydown.enter)="submit.emit()"
    />
    @if (suffix(); as text) {
      <span class="etb-input__affix">{{ text }}</span>
    }
  `,
})
export class DsInput {
  readonly value = model('');
  readonly type = input<'text' | 'number' | 'search'>('text');
  readonly size = input<ControlSize>('md');
  readonly mono = input(false);
  readonly invalid = input(false);
  readonly disabled = input(false);
  readonly iconLeft = input<IconName | null>(null);
  readonly suffix = input<string | null>(null);
  readonly placeholder = input<string | null>(null);
  readonly ariaLabel = input<string | null>(null);
  readonly autocomplete = input<string | null>('off');
  readonly inputMode = input<string | null>(null);
  readonly min = input<number | null>(null);
  readonly name = input<string | null>(null);
  readonly inputId = input<string | null>(null);
  readonly submit = output<void>();

  protected readonly affixGlyphSize = affixGlyphSize;

  protected onInput(event: Event): void {
    this.value.set((event.target as HTMLInputElement).value);
  }
}
