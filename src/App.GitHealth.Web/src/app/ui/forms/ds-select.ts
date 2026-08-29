import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { DsIcon } from '../core/ds-icon';

export interface SelectOption {
  readonly value: string;
  readonly label: string;
}

const chevronSize = 14;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'etb-select',
    '[class.etb-select--sm]': 'size() === "sm"',
  },
  imports: [DsIcon],
  selector: 'ds-select',
  template: `
    <select
      class="etb-select__control"
      [attr.aria-label]="ariaLabel()"
      [disabled]="disabled()"
      (change)="onChange($event)"
    >
      @for (option of options(); track option.value) {
        <option [value]="option.value" [selected]="option.value === value()">
          {{ option.label }}
        </option>
      }
    </select>
    <ds-icon class="etb-select__chevron" name="chevron-down" [size]="chevronSize" />
  `,
})
export class DsSelect {
  readonly value = model('');
  readonly options = input.required<readonly SelectOption[]>();
  readonly size = input<'sm' | 'md'>('md');
  readonly disabled = input(false);
  readonly ariaLabel = input<string | null>(null);

  protected readonly chevronSize = chevronSize;

  protected onChange(event: Event): void {
    this.value.set((event.target as HTMLSelectElement).value);
  }
}
