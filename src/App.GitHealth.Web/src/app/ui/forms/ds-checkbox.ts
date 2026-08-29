import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { DsIcon } from '../core/ds-icon';

const checkGlyphSize = 11;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ds-checkbox' },
  imports: [DsIcon],
  selector: 'ds-checkbox',
  styles: `
    :host {
      display: inline-flex;
    }
  `,
  template: `
    <label class="etb-check" [class.etb-check--disabled]="disabled()">
      <input
        type="checkbox"
        [attr.aria-label]="ariaLabel()"
        [checked]="checked()"
        [disabled]="disabled()"
        (change)="onChange($event)"
      />
      <span class="etb-check__box" [class.etb-check__box--on]="checked()">
        @if (checked()) {
          <ds-icon name="check" [size]="checkGlyphSize" />
        }
      </span>
    </label>
  `,
})
export class DsCheckbox {
  readonly checked = model(false);
  readonly disabled = input(false);
  readonly ariaLabel = input<string | null>(null);

  protected readonly checkGlyphSize = checkGlyphSize;

  protected onChange(event: Event): void {
    this.checked.set((event.target as HTMLInputElement).checked);
  }
}
