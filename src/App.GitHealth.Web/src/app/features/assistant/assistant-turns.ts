import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { AssistantRunStep } from '../../core/api/api.models';
import { describeActivity } from '../../core/assistant/assistant-steps';
import { AssistantTurn } from '../../core/assistant/assistant-thread';
import { pluralMessage } from '../../core/i18n/plural-message';
import { ToastService } from '../../core/workspace/toast';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsSpinner } from '../../ui/core/ds-spinner';
import { DsCallout } from '../../ui/surfaces/ds-callout';
import { DsMarkdown } from '../../ui/surfaces/ds-markdown';

const copiedMessage = $localize`:@@assistant.turn.copied:Answer copied`;
const youLabel = $localize`:@@assistant.turn.you:You`;

/**
 * The exchanges of one thread. A stored answer and one still being written are the same
 * shape by the time they reach here, so this draws them once rather than twice.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'assistant-turns' },
  imports: [DatePipe, DsButton, DsCallout, DsIcon, DsMarkdown, DsSpinner],
  selector: 'app-assistant-turns',
  styleUrl: './assistant-turns.scss',
  templateUrl: './assistant-turns.html',
})
export class AssistantTurns {
  private readonly toasts = inject(ToastService);
  private readonly openCommands = signal<ReadonlySet<string>>(new Set());

  readonly turns = input.required<readonly AssistantTurn[]>();

  /** Branch names of the capture, which the renderer turns into openable rows. */
  readonly targets = input<readonly string[]>([]);

  readonly branchCount = input(0);

  /** What the agent has written so far, shown under the activity while it writes it. */
  readonly liveTrace = input('');

  /** What the agent has been doing, which is what a run shows instead of a spinner. */
  readonly steps = input<readonly AssistantRunStep[]>([]);

  /** How long the run has been going, or null once it is not going any more. */
  readonly elapsedMs = input<number | null>(null);

  readonly branchSelected = output<string>();
  readonly stopRequested = output<void>();
  readonly retryRequested = output<void>();

  protected readonly you = youLabel;

  protected readonly activity = computed(() => describeActivity(this.steps()));

  protected readonly elapsedLabel = computed(() => {
    const elapsed = this.elapsedMs();
    return elapsed === null ? null : secondsLabel(Math.round(elapsed / 1000));
  });

  /** Every answer is read from the same rows, so the note is written once, not per turn. */
  protected readonly footnote = computed(() => readNotice(this.branchCount()));

  protected isCommandOpen(key: string): boolean {
    return this.openCommands().has(key);
  }

  protected toggleCommand(key: string): void {
    this.openCommands.update((open) => {
      const next = new Set(open);
      if (!next.delete(key)) {
        next.add(key);
      }

      return next;
    });
  }

  protected durationLabel(milliseconds: number): string {
    const seconds = (milliseconds / 1000).toFixed(1);
    return $localize`:@@assistant.turn.duration:${seconds}:seconds: s`;
  }

  protected copy(text: string): void {
    void navigator.clipboard?.writeText(text).then(() => this.toasts.show(copiedMessage));
  }
}

function readNotice(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@assistant.turn.footnote.one:${count}:count: row read · nothing written`,
    other: $localize`:@@assistant.turn.footnote.many:${count}:count: rows read · nothing written`,
  });
}

function secondsLabel(seconds: number): string {
  return $localize`:@@assistant.turn.elapsed:${seconds}:seconds: s`;
}
