import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export interface KeyValueItem {
  readonly label: string;
  readonly value: string;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'ds-key-value-list',
  styles: `
    :host {
      display: block;
    }
  `,
  template: `
    <dl class="etb-kv" [class.etb-kv--bordered]="bordered()">
      @for (item of items(); track item.label) {
        <dt>{{ item.label }}</dt>
        <dd>{{ item.value }}</dd>
      }
    </dl>
  `,
})
export class DsKeyValueList {
  readonly items = input.required<readonly KeyValueItem[]>();
  readonly bordered = input(false);
}
