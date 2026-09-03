import { AssistantMessage, AssistantRun } from '../api/api.models';
import { buildThread } from './assistant-thread';

const you = 'You';
const agent = 'Claude Code';
const conversationId = '33333333-3333-3333-3333-333333333333';
const runId = '22222222-2222-2222-2222-222222222222';

describe('buildThread', () => {
  it('reads the stored turns in their recorded order, not the order they arrived in', () => {
    const turns = buildThread({
      messages: [answer({ position: 1, text: 'Two can go.' }), question({ position: 0 })],
      run: null,
      you,
      agent,
    });

    expect(turns.map((turn) => turn.text)).toEqual(['Which branches can go?', 'Two can go.']);
    expect(turns[0]).toMatchObject({ isUser: true, who: you });
    expect(turns[1]).toMatchObject({ isUser: false, who: agent });
  });

  /**
   * A run in flight is two turns, not one: the question has to appear the moment it is asked,
   * or the panel would look like it swallowed it until the agent replied.
   */
  it('adds the question and the answer of a run still in flight', () => {
    const turns = buildThread({ messages: [], run: running(), you, agent });

    expect(turns).toHaveLength(2);
    expect(turns[0]).toMatchObject({ isUser: true, text: 'Which branches can go?', who: you });
    expect(turns[1]).toMatchObject({ isUser: false, state: 'running', who: 'Claude Code' });
  });

  it('places the live turn after everything already stored', () => {
    const turns = buildThread({
      messages: [question({ position: 0 }), answer({ position: 1 })],
      run: running(),
      you,
      agent,
    });

    expect(turns).toHaveLength(4);
    expect(turns[3].state).toBe('running');
  });

  it.each([
    ['Completed', 'answered'],
    ['Failed', 'failed'],
    ['Cancelled', 'cancelled'],
  ])('reports a %s run as %s', (status, state) => {
    const turns = buildThread({
      messages: [],
      run: { ...running(), status: status as AssistantRun['status'] },
      you,
      agent,
    });

    expect(turns[1].state).toBe(state);
  });

  it('measures how long a settled run took, and refuses to guess for one still going', () => {
    const settled = buildThread({
      messages: [],
      run: {
        ...running(),
        status: 'Completed',
        completedAtUtc: '2026-09-02T10:40:07.800Z',
      },
      you,
      agent,
    });

    expect(settled[1].durationMs).toBe(7800);
    expect(buildThread({ messages: [], run: running(), you, agent })[1].durationMs).toBeNull();
  });

  /** A question cannot fail, so it carries none of the machinery an answer needs. */
  it('leaves a question without a state, a duration or a command', () => {
    const [turn] = buildThread({ messages: [question({ position: 0 })], run: null, you, agent });

    expect(turn.state).toBeNull();
    expect(turn.commandLine).toBeNull();
    expect(turn.failureMessage).toBeNull();
  });

  function question(overrides: Partial<AssistantMessage> = {}): AssistantMessage {
    return message({ role: 'user', text: 'Which branches can go?', ...overrides });
  }

  function answer(overrides: Partial<AssistantMessage> = {}): AssistantMessage {
    return message({
      role: 'agent',
      text: 'Two can go.',
      status: 'Completed',
      durationMs: 8400,
      commandLine: '/usr/local/bin/claude --print',
      ...overrides,
    });
  }

  function message(overrides: Partial<AssistantMessage> = {}): AssistantMessage {
    return {
      id: `message-${overrides.position ?? 0}`,
      position: 0,
      role: 'user',
      text: '',
      writtenAtUtc: '2026-09-02T10:40:00Z',
      status: null,
      effort: null,
      commandLine: null,
      failureCode: null,
      failureMessage: null,
      durationMs: null,
      isTruncated: false,
      ...overrides,
    };
  }

  function running(): AssistantRun {
    return {
      runId,
      projectId: '11111111-1111-1111-1111-111111111111',
      agentId: 'claude',
      agentName: 'Claude Code',
      effort: 'medium',
      question: 'Which branches can go?',
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
    };
  }
});
