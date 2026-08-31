import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

const glyphs: Readonly<Record<string, string>> = {
  cmd: '⌘',
  shift: '⇧',
  alt: '⌥',
  ctrl: '⌃',
  enter: '↵',
  esc: 'esc',
  tab: '⇥',
  up: '↑',
  down: '↓',
  left: '←',
  right: '→',
};

/** GitHealth is driven from the keyboard: the shortcuts are shown, never hidden. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'etb-kbd-group' },
  selector: 'ds-kbd',
  template: `
    @for (key of parsedKeys(); track $index) {
      <kbd class="etb-kbd">{{ key }}</kbd>
    }
  `,
})
export class DsKbd {
  readonly keys = input.required<string>();

  protected readonly parsedKeys = computed(() =>
    this.keys()
      .split('+')
      .map((key) => glyphs[key.toLowerCase()] ?? key),
  );
}
