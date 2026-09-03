import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  AssistantAgent,
  AssistantBriefing,
  AssistantRun,
  AssistantRunStep,
} from '../api/api.models';
import { AssistantStore, assistantPollIntervalMs } from './assistant-store';

const projectId = '11111111-1111-1111-1111-111111111111';
const runId = '22222222-2222-2222-2222-222222222222';
const baseline = 'refs/heads/release';
const conversationId = '33333333-3333-3333-3333-333333333333';
const question = 'Which branches can go?';

const claude: AssistantAgent = {
  id: 'claude',
  name: 'Claude Code',
  isAvailable: true,
  version: '2.1.220 (Claude Code)',
  executablePath: '/usr/local/bin/claude',
  installationUrl: 'https://claude.com/claude-code',
  unavailableReason: null,
  efforts: ['low', 'medium', 'high', 'xhigh', 'max'],
  defaultEffort: 'medium',
};

const codex: AssistantAgent = {
  id: 'codex',
  name: 'Codex CLI',
  isAvailable: false,
  version: null,
  executablePath: null,
  installationUrl: 'https://developers.openai.com/codex/cli',
  unavailableReason: 'Codex CLI was not found.',
  efforts: ['low', 'medium', 'high', 'xhigh', 'max'],
  defaultEffort: 'low',
};

const waiting: AssistantRunStep = {
  kind: 'Waiting',
  label: '',
  detail: null,
  atUtc: '2026-09-02T10:40:01Z',
};

const reading: AssistantRunStep = {
  kind: 'Tool',
  label: 'list_branches',
  detail: 'verdict=merged',
  atUtc: '2026-09-02T10:40:03Z',
};

const briefing: AssistantBriefing = {
  baseline: 'refs/heads/main',
  capturedAtUtc: '2026-09-02T10:34:00Z',
  branchCount: 12,
  omittedBranchCount: 0,
  text: '# Branch capture',
};

describe('AssistantStore', () => {
  let store: AssistantStore;
  let http: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(AssistantStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    store.clear();
    vi.useRealTimers();
  });

  it('selects the first agent that can actually run', () => {
    loadAgents();

    expect(store.agentId()).toBe('claude');
    expect(store.availableAgents()).toHaveLength(1);
    expect(store.hasAvailableAgent()).toBe(true);
  });

  it('reads the catalog once, and again only when asked to look for a new install', () => {
    loadAgents();
    store.loadAgents();
    http.verify();

    store.loadAgents(true);
    http
      .expectOne('/api/assistant/agents?refresh=true')
      .flush({ isEnabled: true, agents: [claude, codex] });
  });

  /**
   * Consent belongs to the repository, not to this store: it is read from the API and added
   * by the panel. A well-formed question is all this store is entitled to judge.
   */
  it('is ready as soon as an agent and a question are there', () => {
    loadAgents();
    expect(store.canRun()).toBe(false);

    store.question.set(question);
    expect(store.canRun()).toBe(true);
  });

  it('refuses to run on an empty question', () => {
    loadAgents();
    store.question.set('   ');

    expect(store.canRun()).toBe(false);
  });

  it('sends the trimmed question with the selected agent and baseline', () => {
    loadAgents();
    ask();

    const request = http.expectOne(`/api/projects/${projectId}/assistant/runs`);
    expect(request.request.body).toEqual({
      agentId: 'claude',
      question,
      baseline,
      effort: 'medium',
      conversationId: null,
    });
    request.flush(run());
  });

  /** A follow-up joins the thread it answers, rather than opening a second one beside it. */
  it('names the thread a follow-up continues', () => {
    loadAgents();
    store.question.set(question);
    store.start({ projectId, baseline, conversationId });

    const request = http.expectOne(`/api/projects/${projectId}/assistant/runs`);
    expect(request.request.body).toMatchObject({ conversationId });
    request.flush(run());
  });

  /**
   * A poll carries the trace since the offset it asked for, not the whole log. Appending it
   * the wrong way would either duplicate the answer or drop half of it.
   */
  it('appends each poll to the trace and asks for the rest from the new offset', () => {
    start({ trace: 'Reading', traceOffset: 7 });
    expect(store.trace()).toBe('Reading');

    poll({ trace: ' the capture', traceOffset: 19 });

    expect(store.trace()).toBe('Reading the capture');
  });

  it('holds the trace while the agent writes, and the answer once it has', () => {
    start({ trace: 'partial', traceOffset: 7 });
    expect(store.trace()).toBe('partial');
    expect(store.isRunning()).toBe(true);

    poll({ status: 'Completed', answer: 'Two branches can go.', traceOffset: 7 });

    expect(store.run()?.answer).toBe('Two branches can go.');
    expect(store.isRunning()).toBe(false);
  });

  /**
   * The steps come whole on every poll, so they are stated rather than accumulated: a list
   * that grew by appending would double every step the moment a poll repeated one.
   */
  it('replaces the steps with what the poll says the agent is doing', () => {
    start({ steps: [waiting] });
    expect(store.steps()).toEqual([waiting]);

    poll({ steps: [waiting, reading] });

    expect(store.steps()).toEqual([waiting, reading]);
  });

  it('drops the steps once the thread has been read back', () => {
    start({ steps: [waiting, reading] });

    store.clear();

    expect(store.steps()).toEqual([]);
  });

  /** The elapsed time is what moves while nothing else does; it stops when the run does. */
  it('counts the time a run has been going, and stops counting once it has settled', () => {
    start();
    expect(store.elapsedMs()).not.toBeNull();

    poll({ status: 'Completed', answer: 'done' });

    expect(store.elapsedMs()).toBeNull();
  });

  it('stops polling once the run has settled', () => {
    start();
    poll({ status: 'Failed', failureMessage: 'stopped' });

    vi.advanceTimersByTime(assistantPollIntervalMs * 3);

    http.verify();
    expect(store.run()?.status).toBe('Failed');
  });

  /** The question becomes a turn of the thread, so a copy left in the box would double it. */
  it('empties the composer once the question has been sent', () => {
    start();

    expect(store.question()).toBe('');
  });

  it('drops the settled run without touching what is in the composer', () => {
    start({ status: 'Completed', answer: 'done' });
    store.question.set('And by author?');

    store.clear();

    expect(store.run()).toBeNull();
    expect(store.trace()).toBe('');
    expect(store.question()).toBe('And by author?');
  });

  it('offers the levels the selected agent declares, starting on its default', () => {
    loadAgents();

    expect(store.effort()).toBe('medium');
    expect(store.effortOptions().map((option) => option.value)).toEqual([
      'low',
      'medium',
      'high',
      'xhigh',
      'max',
    ]);
    expect(store.effortOptions()[0].label).toBe('Quick');
  });

  it('sends the chosen level rather than the default', () => {
    loadAgents();
    store.effort.set('xhigh');
    ask();

    const request = http.expectOne(`/api/projects/${projectId}/assistant/runs`);
    expect(request.request.body).toMatchObject({ effort: 'xhigh' });
    request.flush(run());
  });

  /**
   * Levels are per agent. Carrying one over to an agent that does not declare it would send
   * a run the API is bound to refuse.
   */
  it('falls back to the new agent default when the agent changes', () => {
    loadAgents();
    store.effort.set('max');

    store.agents.set([{ ...codex, isAvailable: true, version: 'codex-cli 0.150.1' }]);
    store.agentId.set('codex');

    expect(store.effort()).toBe('low');
  });

  it('reports a disabled installation rather than an empty list', () => {
    store.loadAgents();
    http.expectOne('/api/assistant/agents').flush({ isEnabled: false, agents: [] });

    expect(store.isEnabled()).toBe(false);
    expect(store.hasAvailableAgent()).toBe(false);
    expect(store.canRun()).toBe(false);
  });

  it('keeps the reason an agent is unavailable so the screen can state it', () => {
    loadAgents();

    const missing = store.agents().find((agent) => !agent.isAvailable);
    expect(missing?.unavailableReason).toBe('Codex CLI was not found.');
  });

  it('loads the briefing that would be sent for the selected baseline', () => {
    store.loadBriefing(projectId, baseline);
    http
      .expectOne(`/api/projects/${projectId}/assistant/briefing?baseline=${baseline}`)
      .flush(briefing);

    expect(store.briefing()?.text).toBe('# Branch capture');
    expect(store.isLoadingBriefing()).toBe(false);
  });

  function loadAgents(): void {
    store.loadAgents();
    http.expectOne('/api/assistant/agents').flush({ isEnabled: true, agents: [claude, codex] });
  }

  function ask(): void {
    store.question.set(`  ${question}  `);
    store.start({ projectId, baseline, conversationId: null });
  }

  function start(overrides: Partial<AssistantRun> = {}): void {
    loadAgents();
    ask();
    http.expectOne(`/api/projects/${projectId}/assistant/runs`).flush(run(overrides));
  }

  function poll(overrides: Partial<AssistantRun> = {}): void {
    const from = store.run()?.traceOffset ?? 0;
    vi.advanceTimersByTime(assistantPollIntervalMs);
    http.expectOne(`/api/assistant/runs/${runId}?from=${from}`).flush(run(overrides));
  }

  function run(overrides: Partial<AssistantRun> = {}): AssistantRun {
    return {
      runId,
      projectId,
      agentId: 'claude',
      agentName: 'Claude Code',
      effort: 'medium',
      question,
      commandLine: '/usr/local/bin/claude --print',
      conversationId,
      branchCount: 12,
      status: 'Running',
      startedAtUtc: '2026-09-02T10:40:00Z',
      completedAtUtc: null,
      steps: [],
      trace: '',
      traceOffset: 0,
      answer: null,
      failureCode: null,
      failureMessage: null,
      isTruncated: false,
      ...overrides,
    };
  }
});
