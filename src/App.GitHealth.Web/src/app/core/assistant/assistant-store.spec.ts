import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AssistantAgent, AssistantBriefing, AssistantRun } from '../api/api.models';
import { AssistantStore, assistantPollIntervalMs } from './assistant-store';

const projectId = '11111111-1111-1111-1111-111111111111';
const runId = '22222222-2222-2222-2222-222222222222';
const baseline = 'refs/heads/release';
const question = 'Which branches can go?';

const claude: AssistantAgent = {
  id: 'claude',
  name: 'Claude Code',
  isAvailable: true,
  version: '2.1.220 (Claude Code)',
  executablePath: '/usr/local/bin/claude',
  installationUrl: 'https://claude.com/claude-code',
  unavailableReason: null,
};

const codex: AssistantAgent = {
  id: 'codex',
  name: 'Codex CLI',
  isAvailable: false,
  version: null,
  executablePath: null,
  installationUrl: 'https://developers.openai.com/codex/cli',
  unavailableReason: 'Codex CLI was not found.',
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
   * The consent is what turns a capture into something that may leave the machine. Without
   * it the button stays out of reach, whatever else is filled in.
   */
  it('refuses to run until the capture has been agreed to', () => {
    loadAgents();
    store.question.set(question);
    expect(store.canRun()).toBe(false);

    store.hasConsented.set(true);
    expect(store.canRun()).toBe(true);
  });

  it('refuses to run on an empty question', () => {
    loadAgents();
    store.hasConsented.set(true);
    store.question.set('   ');

    expect(store.canRun()).toBe(false);
  });

  it('sends the trimmed question with the selected agent and baseline', () => {
    loadAgents();
    ask();

    const request = http.expectOne(`/api/projects/${projectId}/assistant/runs`);
    expect(request.request.body).toEqual({ agentId: 'claude', question, baseline });
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

  it('shows the answer once it exists, and the live trace until then', () => {
    start({ trace: 'partial', traceOffset: 7 });
    expect(store.output()).toBe('partial');
    expect(store.isRunning()).toBe(true);

    poll({ status: 'Completed', answer: 'Two branches can go.', traceOffset: 7 });

    expect(store.output()).toBe('Two branches can go.');
    expect(store.isRunning()).toBe(false);
  });

  it('stops polling once the run has settled', () => {
    start();
    poll({ status: 'Failed', failureMessage: 'stopped' });

    vi.advanceTimersByTime(assistantPollIntervalMs * 3);

    http.verify();
    expect(store.run()?.status).toBe('Failed');
  });

  it('clears the answer without clearing the question', () => {
    start({ status: 'Completed', answer: 'done' });

    store.clear();

    expect(store.run()).toBeNull();
    expect(store.output()).toBe('');
    expect(store.question()).toContain(question);
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
    store.hasConsented.set(true);
    store.question.set(`  ${question}  `);
    store.start(projectId, baseline);
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
      question,
      commandLine: '/usr/local/bin/claude --print',
      status: 'Running',
      startedAtUtc: '2026-09-02T10:40:00Z',
      completedAtUtc: null,
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
