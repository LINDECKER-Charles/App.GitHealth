import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { SelectOption } from '../forms/ds-select';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'etb-seg',
    role: 'tablist',
    '[class.etb-seg--sm]': 'size() === "sm"',
  },
  selector: 'ds-segmented-control',
  template: `
    @for (option of options(); track option.value) {
      <button
        class="etb-seg__item"
        type="button"
        role="tab"
        [class.etb-seg__item--on]="option.value === value()"
        [attr.aria-selected]="option.value === value()"
        (click)="value.set(option.value)"
      >
        {{ option.label }}
      </button>
    }
  `,
})
export class DsSegmentedControl {
  readonly value = model('');
  readonly options = input.required<readonly SelectOption[]>();
  readonly size = input<'sm' | 'md'>('md');
}
