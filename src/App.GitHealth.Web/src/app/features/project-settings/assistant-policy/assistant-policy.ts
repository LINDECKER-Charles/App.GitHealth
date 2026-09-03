import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { Uuid } from '../../../core/api/api.models';
import { AssistantHistoryStore } from '../../../core/assistant/assistant-history-store';
import { AssistantStore } from '../../../core/assistant/assistant-store';
import { pluralMessage } from '../../../core/i18n/plural-message';
import { relativeTime } from '../../../core/workspace/relative-time';
import { DsButton } from '../../../ui/core/ds-button';
import { DsStatusDot } from '../../../ui/core/ds-status-dot';
import { DsPanel } from '../../../ui/surfaces/ds-panel';

/**
 * The assistant, seen from the policy screen: who may be sent this repository's captures,
 * and what is being kept of the answers. Both live here rather than in the panel because
 * this is where a reader comes to undo a decision, not to make one.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DsButton, DsPanel, DsStatusDot],
  selector: 'app-assistant-policy',
  styleUrl: './assistant-policy.scss',
  templateUrl: './assistant-policy.html',
})
export class AssistantPolicy {
  protected readonly history = inject(AssistantHistoryStore);
  protected readonly store = inject(AssistantStore);

  readonly projectId = input.required<Uuid>();

  protected readonly consentTitle = computed(() =>
    this.history.hasConsented()
      ? $localize`:@@settings.assistant.consent.on:Sending allowed for this repository`
      : $localize`:@@settings.assistant.consent.off:Sending not allowed`,
  );

  protected readonly consentDetail = computed(() => {
    const granted = this.history.consentGrantedAtUtc();
    return granted === null
      ? $localize`:@@settings.assistant.consent.never:The panel will ask before the first question.`
      : allowedNotice(relativeTime(granted));
  });

  /** The agent that would answer, named with the path it was found at. */
  protected readonly agentDetail = computed(() => {
    const agent = this.store.selectedAgent();
    return agent === null
      ? $localize`:@@settings.assistant.agent.none:No agent is installed on this machine.`
      : `${agent.name} ${agent.version ?? ''} · ${agent.executablePath ?? ''}`.trim();
  });

  protected readonly storedDetail = computed(() => storedNotice(this.history.conversationCount()));

  protected readonly hasConversations = computed(() => this.history.conversationCount() > 0);

  constructor() {
    effect(() => this.history.loadStatus(this.projectId()));
    effect(() => this.store.loadAgents());
  }

  protected allow(): void {
    this.history.setConsent(
      this.projectId(),
      true,
      $localize`:@@settings.assistant.toast.allowed:Sending allowed for this repository`,
    );
  }

  protected revoke(): void {
    this.history.setConsent(
      this.projectId(),
      false,
      $localize`:@@settings.assistant.toast.revoked:Sending revoked · the stored conversations are untouched`,
    );
  }

  protected purge(): void {
    this.history.purge(this.projectId(), purgedNotice);
  }
}

function allowedNotice(when: string): string {
  return $localize`:@@settings.assistant.consent.since:Allowed ${when}:when: · applies to every baseline`;
}

function storedNotice(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@settings.assistant.stored.one:${count}:count: conversation, in the local database with the captures.`,
    other: $localize`:@@settings.assistant.stored.many:${count}:count: conversations, in the local database with the captures.`,
  });
}

function purgedNotice(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@settings.assistant.purged.one:${count}:count: conversation deleted`,
    other: $localize`:@@settings.assistant.purged.many:${count}:count: conversations deleted`,
  });
}
