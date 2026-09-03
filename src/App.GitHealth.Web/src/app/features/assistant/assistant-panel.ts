import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  afterRenderEffect,
  computed,
  effect,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { AssistantConversationSummary, Uuid } from '../../core/api/api.models';
import { AssistantHistoryStore } from '../../core/assistant/assistant-history-store';
import { AssistantPanelState } from '../../core/assistant/assistant-panel-state';
import { AssistantStore } from '../../core/assistant/assistant-store';
import { buildThread } from '../../core/assistant/assistant-thread';
import { displayReference } from '../../core/branches/branch-labels';
import { pluralMessage } from '../../core/i18n/plural-message';
import { DsBadge } from '../../ui/core/ds-badge';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsIconButton } from '../../ui/core/ds-icon-button';
import { DsStatusDot } from '../../ui/core/ds-status-dot';
import { DsSelect } from '../../ui/forms/ds-select';
import { DsCallout } from '../../ui/surfaces/ds-callout';
import { BaselineStore } from '../project/baseline/baseline-store';
import { CaptureStore } from '../project/capture-store';
import { AssistantHistory } from './assistant-history';
import { buildBranchIndex, emptyBranchIndex } from './assistant-branch-index';
import { AssistantTurns } from './assistant-turns';

const youLabel = $localize`:@@assistant.turn.you:You`;
const suggestions: readonly string[] = [
  $localize`:@@assistant.suggestion.cleanup:Which branches look safe to clean up, and why?`,
  $localize`:@@assistant.suggestion.review:Which branches need a review before anything else?`,
  $localize`:@@assistant.suggestion.owners:Group the branches by author and say who has the most work in flight.`,
];

/**
 * The assistant, as a panel beside the table rather than a page of its own. It sits next to
 * the rows it is talking about on purpose: an answer names branches, and naming them is only
 * useful if the reader can look at them without leaving the answer.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'assistant-panel' },
  imports: [
    AssistantHistory,
    AssistantTurns,
    DsBadge,
    DsButton,
    DsCallout,
    DsIcon,
    DsIconButton,
    DsSelect,
    DsStatusDot,
  ],
  selector: 'app-assistant-panel',
  styleUrl: './assistant-panel.scss',
  templateUrl: './assistant-panel.html',
})
export class AssistantPanel {
  private readonly baselines = inject(BaselineStore);
  private readonly captures = inject(CaptureStore);

  readonly projectId = input.required<Uuid>();

  /** The snapshot id of a branch an answer named, so the shell can open its row. */
  readonly branchOpened = output<string>();

  private readonly body = viewChild<ElementRef<HTMLElement>>('body');

  protected readonly store = inject(AssistantStore);
  protected readonly history = inject(AssistantHistoryStore);
  protected readonly panel = inject(AssistantPanelState);
  protected readonly suggestions = suggestions;
  protected readonly askPlaceholder = $localize`:@@assistant.question.placeholder:Ask about this capture…`;
  protected readonly followPlaceholder = $localize`:@@assistant.question.followUp:Follow up…`;

  protected readonly hasCapture = computed(() => this.store.briefing() !== null);

  protected readonly isReady = computed(
    () => this.store.isEnabled() && this.store.hasAvailableAgent(),
  );

  protected readonly branches = computed(() => {
    const snapshot = this.captures.snapshot();
    return snapshot === null ? emptyBranchIndex : buildBranchIndex(snapshot.branches);
  });

  protected readonly turns = computed(() =>
    buildThread({
      messages: this.history.messages(),
      run: this.store.run(),
      you: youLabel,
      agent: this.history.agentName(),
    }),
  );

  /**
   * The version alone. A CLI answers its version flag with a whole sentence — "2.1.220
   * (Claude Code)" — and the panel is 404 pixels wide, where the name is already on screen.
   */
  protected readonly agentVersion = computed(() => {
    const version = this.store.selectedAgent()?.version ?? '';
    return version.split(' ')[0];
  });

  protected readonly baselineLabel = computed(() => displayReference(this.baselines.selected()));

  protected readonly captureLabel = computed(() => {
    const briefing = this.store.briefing();
    return briefing === null ? '' : branchCountLabel(briefing.branchCount);
  });

  protected readonly panelLabel = computed(() =>
    this.panel.isHistory()
      ? $localize`:@@assistant.panel.history:Assistant · conversations`
      : $localize`:@@assistant.panel.title:Assistant`,
  );

  protected readonly briefingLabel = computed(() =>
    this.panel.isBriefingOpen()
      ? $localize`:@@assistant.briefing.hide:Hide it`
      : $localize`:@@assistant.briefing.show:What it can query`,
  );

  protected readonly showComposer = computed(
    () =>
      this.isReady() && this.hasCapture() && this.history.hasConsented() && !this.panel.isHistory(),
  );

  protected readonly canAsk = computed(() => this.store.canRun() && this.history.hasConsented());

  constructor() {
    effect(() => this.store.loadAgents());
    effect(() => this.store.loadBriefing(this.projectId(), this.baselines.requested()));
    effect(() => this.loadHistory(this.projectId()));
    effect(() => this.keepSettledTurn());
    afterRenderEffect(() => this.followTheAnswer());
  }

  /**
   * Keeps the newest turn in view as it grows — the steps of a run as much as the answer.
   * Something that writes itself off the bottom of the panel is worse than nothing at all.
   */
  private followTheAnswer(): void {
    this.turns();
    this.store.trace();
    this.store.steps();
    const element = this.body()?.nativeElement;
    if (element !== undefined) {
      element.scrollTop = element.scrollHeight;
    }
  }

  protected ask(): void {
    this.store.start({
      projectId: this.projectId(),
      baseline: this.baselines.requested(),
      conversationId: this.history.conversationId(),
    });
  }

  protected retry(): void {
    const last = [...this.turns()].reverse().find((turn) => turn.isUser);
    if (last !== undefined) {
      this.store.question.set(last.text);
      this.ask();
    }
  }

  protected useSuggestion(suggestion: string): void {
    this.store.question.set(suggestion);
  }

  protected onComposerKey(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.ask();
    }
  }

  protected newConversation(): void {
    this.store.clear();
    this.store.question.set('');
    this.history.startNew();
    this.panel.showThread();
  }

  protected openConversation(conversationId: Uuid): void {
    this.store.clear();
    this.history.open(conversationId);
    this.panel.showThread();
  }

  protected removeConversation(conversation: AssistantConversationSummary): void {
    this.history.remove(
      conversation.id,
      this.projectId(),
      $localize`:@@assistant.history.removed:Conversation deleted`,
    );
  }

  protected allow(): void {
    this.history.setConsent(
      this.projectId(),
      true,
      $localize`:@@assistant.consent.allowed:Sending allowed for this repository`,
    );
  }

  protected openBranch(name: string): void {
    const snapshotId = this.branches().rows.get(name);
    if (snapshotId !== undefined) {
      this.branchOpened.emit(snapshotId);
    }
  }

  protected lookAgain(): void {
    this.store.loadAgents(true);
  }

  private loadHistory(projectId: Uuid): void {
    if (projectId.length > 0) {
      this.history.loadStatus(projectId);
      this.history.loadConversations(projectId);
    }
  }

  /**
   * Once a run settles its exchange is on disk, so the thread is read back and the live run
   * dropped. The order matters: dropping first would blank the answer if the read failed.
   */
  private keepSettledTurn(): void {
    const run = this.store.run();
    if (run === null || run.status === 'Running') {
      return;
    }

    this.history.refresh(run.conversationId, () => {
      this.store.clear();
      this.history.loadConversations(this.projectId());
    });
  }
}

function branchCountLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@assistant.branches.one:${count}:count: branch`,
    other: $localize`:@@assistant.branches.many:${count}:count: branches`,
  });
}
