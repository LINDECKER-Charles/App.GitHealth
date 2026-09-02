import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, switchMap, timer } from 'rxjs';
import { apiErrorMessage } from '../api/api-error';
import { GitHealthApiClient } from '../api/git-health-api-client';
import { AssistantAgent, AssistantBriefing, AssistantRun, Uuid } from '../api/api.models';
import { SelectOption } from '../../ui/forms/ds-select';

/** Re-read cadence of a running agent, shared with the tests. */
export const assistantPollIntervalMs = 700;

const agentsFailureMessage = $localize`:@@apiError.assistant.agents:The installed agents could not be listed.`;
const briefingFailureMessage = $localize`:@@apiError.assistant.briefing:The capture could not be prepared.`;
const startFailureMessage = $localize`:@@apiError.assistant.start:The agent could not be started.`;

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

  readonly isEnabled = signal(true);
  readonly agents = signal<readonly AssistantAgent[]>([]);
  readonly agentId = signal('');
  readonly question = signal('');
  readonly briefing = signal<AssistantBriefing | null>(null);
  readonly isBriefingOpen = signal(false);

  /**
   * Agreement that the capture may leave the machine. Kept for the session rather than
   * asked on every question: the briefing stays one click away the whole time.
   */
  readonly hasConsented = signal(false);

  readonly run = signal<AssistantRun | null>(null);
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

  readonly isRunning = computed(() => this.run()?.status === 'Running');

  readonly canRun = computed(
    () =>
      this.isEnabled() &&
      this.selectedAgent() !== null &&
      this.question().trim().length > 0 &&
      this.hasConsented() &&
      !this.isRunning() &&
      !this.isStarting(),
  );

  /** The answer once it exists, the live trace until then: the panel is never blank. */
  readonly output = computed(() => this.run()?.answer ?? this.trace());

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

  toggleBriefing(): void {
    this.isBriefingOpen.update((open) => !open);
  }

  start(projectId: Uuid, baseline: string | null): void {
    const agent = this.selectedAgent();
    if (agent === null || !this.canRun()) {
      return;
    }

    this.stopPolling();
    this.traceOffset = 0;
    this.trace.set('');
    this.error.set(null);
    this.isStarting.set(true);
    this.api
      .startAssistantRun(projectId, {
        agentId: agent.id,
        question: this.question().trim(),
        baseline,
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

  /** Clears the answer without clearing the question: asking again is the common case. */
  clear(): void {
    this.stopPolling();
    this.run.set(null);
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

  private applyStarted(run: AssistantRun): void {
    this.isStarting.set(false);
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

  /** The payload carries the trace since the offset asked for, so it is appended, not set. */
  private apply(run: AssistantRun): void {
    this.run.set(run);
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
