import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DsIcon } from './ds-icon';

const removeGlyphSize = 10;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'etb-tag' },
  imports: [DsIcon],
  selector: 'ds-tag',
  template: `
    <ng-content />
    @if (removable()) {
      <button
        class="etb-tag__x"
        type="button"
        [attr.aria-label]="removeLabel()"
        (click)="remove.emit()"
      >
        <ds-icon name="x" [size]="removeGlyphSize" />
      </button>
    }
  `,
})
export class DsTag {
  readonly removable = input(false);
  readonly removeLabel = input('Retirer');
  readonly remove = output<void>();

  protected readonly removeGlyphSize = removeGlyphSize;
}
