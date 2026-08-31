import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { DsIconButton } from '../core/ds-icon-button';
import { ShellToken, tokenizeShell } from './shell-tokenizer';

const copiedFeedbackMs = 1400;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'etb-code',
    '[class.etb-code--sm]': 'size() === "sm"',
  },
  imports: [DsIconButton],
  selector: 'ds-code-block',
  template: `
    <div class="etb-code__bar">
      <span class="etb-code__lang">{{ language() }}</span>
      <span class="etb-code__spacer"></span>
      <button
        dsIconButton
        size="sm"
        [icon]="copied() ? 'check' : 'copy'"
        [label]="copied() ? copiedLabel : copyLabel"
        [style.color]="copied() ? 'var(--status-success-solid)' : null"
        (click)="copy()"
      ></button>
    </div>
    <pre class="etb-code__pre"><code>@for (token of tokens(); track $index) {<span
      [class]="token.className">{{ token.text }}</span>}</code></pre>
  `,
})
export class DsCodeBlock {
  protected readonly copyLabel = $localize`:@@ui.codeBlock.copy:Copy`;
  protected readonly copiedLabel = $localize`:@@ui.codeBlock.copied:Copied`;

  readonly code = input.required<string>();
  readonly language = input('bash');
  readonly size = input<'sm' | 'md'>('md');

  protected readonly copied = signal(false);
  protected readonly tokens = computed<readonly ShellToken[]>(() => tokenizeShell(this.code()));

  private timer?: ReturnType<typeof setTimeout>;

  protected copy(): void {
    void navigator.clipboard?.writeText(this.code());
    this.copied.set(true);
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.copied.set(false), copiedFeedbackMs);
  }
}
