import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AssistantConversationSummary, Uuid } from '../../core/api/api.models';
import { displayReference } from '../../core/branches/branch-labels';
import { pluralMessage } from '../../core/i18n/plural-message';
import { relativeTime } from '../../core/workspace/relative-time';
import { DsIconButton } from '../../ui/core/ds-icon-button';

/**
 * Past threads of this repository, whichever baseline they read. Listing them together is
 * deliberate: a question is remembered by what was asked, not by which baseline was open.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'assistant-history' },
  imports: [DsIconButton],
  selector: 'app-assistant-history',
  styleUrl: './assistant-history.scss',
  templateUrl: './assistant-history.html',
})
export class AssistantHistory {
  readonly conversations = input.required<readonly AssistantConversationSummary[]>();
  readonly selectedId = input<Uuid | null>(null);

  readonly opened = output<Uuid>();
  readonly removed = output<AssistantConversationSummary>();

  protected readonly baselineOf = displayReference;

  protected when(conversation: AssistantConversationSummary): string {
    return relativeTime(conversation.updatedAtUtc);
  }

  protected answers(count: number): string {
    return pluralMessage(count, {
      one: $localize`:@@assistant.history.answers.one:${count}:count: answer`,
      other: $localize`:@@assistant.history.answers.many:${count}:count: answers`,
    });
  }

  protected removeLabel(conversation: AssistantConversationSummary): string {
    return $localize`:@@assistant.history.remove:Delete the conversation ${conversation.title}:title:`;
  }
}
