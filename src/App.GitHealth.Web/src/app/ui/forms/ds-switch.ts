import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ds-switch' },
  selector: 'ds-switch',
  styles: `
    :host {
      display: inline-flex;
    }
  `,
  template: `
    <label class="etb-switch" [class.etb-switch--disabled]="disabled()">
      <input
        type="checkbox"
        role="switch"
        [attr.aria-label]="ariaLabel()"
        [checked]="checked()"
        [disabled]="disabled()"
        (change)="onChange($event)"
      />
      <span class="etb-switch__track" [class.etb-switch__track--on]="checked()">
        <span class="etb-switch__thumb"></span>
      </span>
    </label>
  `,
})
export class DsSwitch {
  readonly checked = model(false);
  readonly disabled = input(false);
  readonly ariaLabel = input<string | null>(null);

  protected onChange(event: Event): void {
    this.checked.set((event.target as HTMLInputElement).checked);
  }
}
