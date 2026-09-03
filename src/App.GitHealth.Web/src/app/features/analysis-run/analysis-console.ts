import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  input,
  viewChild,
} from '@angular/core';
import { AnalysisCommandTrace } from '../../core/api/api.models';
import { pluralMessage } from '../../core/i18n/plural-message';
import { DsIcon } from '../../ui/core/ds-icon';

/** How far from the bottom still counts as watching the tail rather than reading back. */
const tailThresholdPx = 24;

/**
 * Every Git command the run has just made, as it would read in a terminal. It is the proof
 * behind the promise: each line is a read, none of them writes.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsIcon],
  selector: 'app-analysis-console',
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-height: 0;
      margin: 6px 14px 14px;
      border: 1px solid var(--border-subtle);
      border-radius: var(--radius-code);
      background: var(--surface-code);
      overflow: hidden;
    }

    .console-head {
      display: flex;
      align-items: center;
      gap: 8px;
      height: 30px;
      flex: none;
      padding: 0 10px 0 12px;
      border-bottom: 1px solid var(--divider);
      background: var(--surface-chrome);
      color: var(--text-muted);
    }

    .console-title {
      font-family: var(--font-mono);
      font-size: var(--text-2xs);
      letter-spacing: var(--tracking-label);
      text-transform: uppercase;
    }

    .console-count {
      margin-left: auto;
      font: var(--type-code-sm);
      font-variant-numeric: tabular-nums;
    }

    .console-body {
      flex: 1;
      min-height: 0;
      overflow: auto;
      padding: 8px 12px;
      font: var(--type-code-sm);
      color: var(--text-code);
      tab-size: 4;
    }

    .console-line {
      display: flex;
      gap: 8px;
      align-items: baseline;
      min-height: 18px;
      animation: gh-fade 160ms var(--ease-out) both;
    }

    .console-prefix {
      flex: none;
      width: 8px;
      color: var(--syn-keyword);
    }

    .console-text {
      flex: 1;
      min-width: 0;
      white-space: pre;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .console-duration {
      flex: none;
      color: var(--text-muted);
      font-variant-numeric: tabular-nums;
    }

    .console-line.is-output .console-prefix,
    .console-line.is-output .console-text {
      color: var(--text-muted);
    }

    .console-line.is-failed .console-text {
      color: var(--status-danger-fg);
    }

    /* The caret says the run is still typing; it stops with the run. */
    .console-caret {
      display: inline-block;
      width: 6px;
      height: 12px;
      margin: 3px 0 0 16px;
      background: var(--text-link);
      animation: gh-caret 900ms steps(1, end) infinite;
    }
  `,
  template: `
    <div class="console-head">
      <ds-icon name="terminal" [size]="13" />
      <span class="console-title" i18n="@@analysisRun.console.title">git · read only</span>
      <span class="console-count">{{ countLabel() }}</span>
    </div>
    <div class="console-body" #body (scroll)="rememberPosition($event)">
      @for (command of commands(); track command.sequence) {
        <div class="console-line" [class.is-failed]="command.exitCode !== 0">
          <span class="console-prefix">$</span>
          <span class="console-text">{{ command.commandLine }}</span>
          <span class="console-duration">{{ command.durationMs }} ms</span>
        </div>
        @if (command.output; as output) {
          <div class="console-line is-output">
            <span class="console-prefix">›</span>
            <span class="console-text">{{ output }}</span>
          </div>
        }
      }
      @if (isWorking()) {
        <span class="console-caret"></span>
      }
    </div>
  `,
})
export class AnalysisConsole {
  readonly commands = input.required<readonly AnalysisCommandTrace[]>();
  readonly commandCount = input.required<number>();
  readonly isWorking = input.required<boolean>();

  protected readonly countLabel = computed(() => commandCountLabel(this.commandCount()));

  private readonly body = viewChild<ElementRef<HTMLElement>>('body');
  private isPinnedToTail = true;

  constructor() {
    effect(() => this.followTail(this.commands().length));
  }

  /**
   * Whether the reader is still at the tail. Read from the scroll event rather than from
   * the effect: by the time the effect runs the new lines are already in the DOM, so the
   * distance to the bottom no longer says where the reader was.
   */
  protected rememberPosition(event: Event): void {
    const body = event.target as HTMLElement;
    const distance = body.scrollHeight - body.clientHeight - body.scrollTop;
    this.isPinnedToTail = distance <= tailThresholdPx;
  }

  /** Follows the newest command, unless the reader has scrolled back to read one. */
  private followTail(length: number): void {
    const body = this.body()?.nativeElement;
    if (body === undefined || length === 0 || !this.isPinnedToTail) {
      return;
    }

    body.scrollTop = body.scrollHeight;
  }
}

function commandCountLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@analysisRun.console.one:${count}:count: command`,
    other: $localize`:@@analysisRun.console.many:${count}:count: commands`,
  });
}
