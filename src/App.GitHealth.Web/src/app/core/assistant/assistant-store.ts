import { DestroyRef, Injectable, computed, inject, linkedSignal, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, switchMap, timer } from 'rxjs';
import { apiErrorMessage } from '../api/api-error';
import { GitHealthApiClient } from '../api/git-health-api-client';
import {
  AssistantAgent,
  AssistantBriefing,
  AssistantRun,
  AssistantRunStep,
  Uuid,
} from '../api/api.models';
import { SelectOption } from '../../ui/forms/ds-select';

/** Re-read cadence of a running agent, shared with the tests. */
export const assistantPollIntervalMs = 700;

/** What a question needs beyond its text: which capture, and which thread it joins. */
export interface AssistantAskRequest {
  readonly projectId: Uuid;
  readonly baseline: string | null;
  /** Null opens a thread; the identifier of the new one comes back on the run. */
  readonly conversationId: Uuid | null;
}

const agentsFailureMessage = $localize`:@@apiError.assistant.agents:The installed agents could not be listed.`;
const briefingFailureMessage = $localize`:@@apiError.assistant.briefing:The capture could not be prepared.`;
const startFailureMessage = $localize`:@@apiError.assistant.start:The agent could not be started.`;

/** Named for what the level buys, not for the flag it becomes. */
const effortLabels: Readonly<Record<string, string>> = {
  low: $localize`:@@assistant.effort.low:Quick`,
  medium: $localize`:@@assistant.effort.medium:Balanced`,
  high: $localize`:@@assistant.effort.high:Thorough`,
  xhigh: $localize`:@@assistant.effort.xhigh:Very thorough`,
  max: $localize`:@@assistant.effort.max:Maximum`,
};

/** An unknown level still reads as itself rather than disappearing from the list. */
function effortLabel(level: string): string {
  return effortLabels[level] ?? level;
}

/**
 * Drives one conversation with a local agent. The briefing is loaded before anything is
 * sent and stays readable next to the answer: the user agrees to a text they have seen,
 * not to a promise about one.
 */
@Injectable({ providedIn: 'root' })
export class AssistantStore {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private polling?: Subscription;
  private traceOffset = 0;
  private loadedAgents = false;

  /** Moves with each poll, which is what makes the elapsed time move with it. */
  private readonly polledAt = signal(0);

  readonly isEnabled = signal(true);
  readonly agents = signal<readonly AssistantAgent[]>([]);
  readonly agentId = signal('');
  readonly question = signal('');
  readonly briefing = signal<AssistantBriefing | null>(null);

  readonly run = signal<AssistantRun | null>(null);
  readonly steps = signal<readonly AssistantRunStep[]>([]);
  readonly trace = signal('');
  readonly error = signal<string | null>(null);
  readonly isLoadingAgents = signal(false);
  readonly isLoadingBriefing = signal(false);
  readonly isStarting = signal(false);

  readonly availableAgents = computed(() => this.agents().filter((agent) => agent.isAvailable));

  readonly hasAvailableAgent = computed(() => this.availableAgents().length > 0);

  readonly agentOptions = computed<readonly SelectOption[]>(() =>
    this.availableAgents().map((agent) => ({ value: agent.id, label: agent.name })),
  );

  readonly selectedAgent = computed<AssistantAgent | null>(
    () => this.availableAgents().find((agent) => agent.id === this.agentId()) ?? null,
  );

  /**
   * How hard the agent is asked to think. Linked to the selection so changing agent falls
   * back to that agent's own default rather than carrying over a level it may not accept.
   */
  readonly effort = linkedSignal<string>(() => this.selectedAgent()?.defaultEffort ?? '');

  readonly effortOptions = computed<readonly SelectOption[]>(() =>
    (this.selectedAgent()?.efforts ?? []).map((level) => ({
      value: level,
      label: effortLabel(level),
    })),
  );

  readonly isRunning = computed(() => this.run()?.status === 'Running');

  /**
   * How long the agent has been at it. Read from the poll rather than from a timer of its
   * own: the run is already re-read every 700 ms, which is finer than a figure in seconds.
   */
  readonly elapsedMs = computed(() => {
    const run = this.run();
    if (run === null || run.status !== 'Running') {
      return null;
    }

    const started = Date.parse(run.startedAtUtc);
    return Number.isNaN(started) ? null : Math.max(0, this.polledAt() - started);
  });

  /**
   * Everything but the permission to send. Consent belongs to the repository rather than to
   * this run, so the panel adds it: the store must not read as though a question were
   * askable merely because it is well formed.
   */
  readonly canRun = computed(
    () =>
      this.isEnabled() &&
      this.selectedAgent() !== null &&
      this.question().trim().length > 0 &&
      !this.isRunning() &&
      !this.isStarting(),
  );

  loadAgents(refresh = false): void {
    if (this.loadedAgents && !refresh) {
      return;
    }

    this.loadedAgents = true;
    this.isLoadingAgents.set(true);
    this.api
      .listAssistantAgents(refresh)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (list) => this.applyAgents(list.isEnabled, list.agents),
        error: (error: unknown) => this.failLoading(error),
      });
  }

  loadBriefing(projectId: Uuid, baseline: string | null): void {
    this.isLoadingBriefing.set(true);
    this.error.set(null);
    this.api
      .getAssistantBriefing(projectId, baseline)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (briefing) => {
          this.briefing.set(briefing);
          this.isLoadingBriefing.set(false);
        },
        error: (error: unknown) => {
          this.briefing.set(null);
          this.isLoadingBriefing.set(false);
          this.error.set(apiErrorMessage(error, briefingFailureMessage));
        },
      });
  }

  start(request: AssistantAskRequest): void {
    const agent = this.selectedAgent();
    if (agent === null || !this.canRun()) {
      return;
    }

    this.stopPolling();
    this.traceOffset = 0;
    this.trace.set('');
    this.steps.set([]);
    this.error.set(null);
    this.isStarting.set(true);
    this.api
      .startAssistantRun(request.projectId, {
        agentId: agent.id,
        question: this.question().trim(),
        baseline: request.baseline,
        effort: this.effort(),
        conversationId: request.conversationId,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (run) => this.applyStarted(run),
        error: (error: unknown) => {
          this.isStarting.set(false);
          this.error.set(apiErrorMessage(error, startFailureMessage));
        },
      });
  }

  cancel(): void {
    const run = this.run();
    if (run === null || !this.isRunning()) {
      return;
    }

    this.api
      .cancelAssistantRun(run.runId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ error: () => this.stopPolling() });
  }

  /** Drops the live run once its thread has been read back, so no answer is drawn twice. */
  clear(): void {
    this.stopPolling();
    this.run.set(null);
    this.steps.set([]);
    this.trace.set('');
    this.traceOffset = 0;
    this.error.set(null);
  }

  private applyAgents(isEnabled: boolean, agents: readonly AssistantAgent[]): void {
    this.isEnabled.set(isEnabled);
    this.agents.set(agents);
    this.isLoadingAgents.set(false);
    const first = agents.find((agent) => agent.isAvailable);
    if (this.selectedAgent() === null && first !== undefined) {
      this.agentId.set(first.id);
    }
  }

  private failLoading(error: unknown): void {
    this.isLoadingAgents.set(false);
    this.error.set(apiErrorMessage(error, agentsFailureMessage));
  }

  /** The question moves into the thread, so the box it was typed in is emptied. */
  private applyStarted(run: AssistantRun): void {
    this.isStarting.set(false);
    this.question.set('');
    this.apply(run);
    this.ensurePolling();
  }

  private ensurePolling(): void {
    if (this.polling !== undefined || !this.isRunning()) {
      return;
    }

    this.polling = timer(assistantPollIntervalMs, assistantPollIntervalMs)
      .pipe(
        switchMap(() => this.api.getAssistantRun(this.run()!.runId, this.traceOffset)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (run) => this.applyPolled(run),
        error: () => this.stopPolling(),
      });
  }

  private applyPolled(run: AssistantRun): void {
    this.apply(run);
    if (run.status !== 'Running') {
      this.stopPolling();
    }
  }

  /**
   * The payload carries the trace since the offset asked for, so it is appended, not set.
   * The steps come whole every time and are replaced: there are few of them, and a list
   * that is stated rather than accumulated cannot drift out of step with the run.
   */
  private apply(run: AssistantRun): void {
    this.run.set(run);
    this.steps.set(run.steps);
    this.polledAt.set(Date.now());
    this.traceOffset = run.traceOffset;
    if (run.trace.length > 0) {
      this.trace.update((current) => current + run.trace);
    }
  }

  private stopPolling(): void {
    this.polling?.unsubscribe();
    this.polling = undefined;
  }
}
