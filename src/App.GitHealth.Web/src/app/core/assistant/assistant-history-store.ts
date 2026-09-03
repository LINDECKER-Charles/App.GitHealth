import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { apiErrorMessage } from '../api/api-error';
import { GitHealthApiClient } from '../api/git-health-api-client';
import {
  AssistantConversationSummary,
  AssistantMessage,
  UtcDateTime,
  Uuid,
} from '../api/api.models';
import { ToastService } from '../workspace/toast';

const historyFailureMessage = $localize`:@@apiError.assistant.history:The stored conversations could not be read.`;
const consentFailureMessage = $localize`:@@apiError.assistant.consent:The permission could not be changed.`;

/**
 * What the machine remembers of past conversations, and whether it is allowed to hold any
 * more. Consent and history live together because the screen that revokes one is the screen
 * that empties the other, and both answer the same question: what is kept about me here.
 */
@Injectable({ providedIn: 'root' })
export class AssistantHistoryStore {
  private readonly api = inject(GitHealthApiClient);
  private readonly toasts = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly conversations = signal<readonly AssistantConversationSummary[]>([]);
  readonly conversationId = signal<Uuid | null>(null);
  readonly messages = signal<readonly AssistantMessage[]>([]);

  /** The agent that answered the loaded thread, which the turns are attributed to. */
  readonly agentName = signal('');
  readonly consentGrantedAtUtc = signal<UtcDateTime | null>(null);
  readonly conversationCount = signal(0);
  readonly isLoading = signal(false);
  readonly error = signal<string | null>(null);

  /** Consent is a moment, not a flag: the interface shows when it was given. */
  readonly hasConsented = computed(() => this.consentGrantedAtUtc() !== null);

  loadStatus(projectId: Uuid): void {
    if (projectId.length === 0) {
      return;
    }

    this.api
      .getAssistantStatus(projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (status) => {
          this.consentGrantedAtUtc.set(status.consentGrantedAtUtc);
          this.conversationCount.set(status.conversationCount);
        },
        error: (error: unknown) => this.fail(error, historyFailureMessage),
      });
  }

  setConsent(projectId: Uuid, granted: boolean, message: string): void {
    this.api
      .setAssistantConsent(projectId, granted)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (status) => {
          this.consentGrantedAtUtc.set(status.consentGrantedAtUtc);
          this.conversationCount.set(status.conversationCount);
          this.toasts.show(message);
        },
        error: (error: unknown) => this.fail(error, consentFailureMessage),
      });
  }

  loadConversations(projectId: Uuid): void {
    this.isLoading.set(true);
    this.api
      .listAssistantConversations(projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => {
          this.conversations.set(list.conversations);
          this.conversationCount.set(list.conversations.length);
          this.isLoading.set(false);
        },
        error: (error: unknown) => {
          this.isLoading.set(false);
          this.fail(error, historyFailureMessage);
        },
      });
  }

  /** Reads a thread back into the panel, replacing whatever it was showing. */
  open(conversationId: Uuid): void {
    this.isLoading.set(true);
    this.api
      .getAssistantConversation(conversationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (thread) => {
          this.conversationId.set(thread.id);
          this.messages.set(thread.messages);
          this.agentName.set(thread.agentName);
          this.isLoading.set(false);
        },
        error: (error: unknown) => {
          this.isLoading.set(false);
          this.fail(error, historyFailureMessage);
        },
      });
  }

  /**
   * Reads the thread back once a run has settled, and reports whether it could. The panel
   * keeps showing the live answer until this succeeds, so a failed read never loses one.
   */
  refresh(conversationId: Uuid, onLoaded: () => void): void {
    this.api
      .getAssistantConversation(conversationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (thread) => {
          this.conversationId.set(thread.id);
          this.messages.set(thread.messages);
          this.agentName.set(thread.agentName);
          onLoaded();
        },
        error: () => undefined,
      });
  }

  /** Starts a thread with nothing in it. The identifier comes back with the first run. */
  startNew(): void {
    this.conversationId.set(null);
    this.messages.set([]);
    this.agentName.set('');
    this.error.set(null);
  }

  remove(conversationId: Uuid, projectId: Uuid, message: string): void {
    this.api
      .deleteAssistantConversation(conversationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          if (this.conversationId() === conversationId) {
            this.startNew();
          }

          this.toasts.show(message);
          this.loadConversations(projectId);
        },
        error: (error: unknown) => this.fail(error, historyFailureMessage),
      });
  }

  purge(projectId: Uuid, describe: (deleted: number) => string): void {
    this.api
      .purgeAssistantConversations(projectId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.startNew();
          this.conversations.set([]);
          this.conversationCount.set(0);
          this.toasts.show(describe(result.deleted));
        },
        error: (error: unknown) => this.fail(error, historyFailureMessage),
      });
  }

  private fail(error: unknown, fallback: string): void {
    this.error.set(apiErrorMessage(error, fallback));
  }
}
