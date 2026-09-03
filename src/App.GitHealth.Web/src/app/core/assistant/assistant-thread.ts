import { AssistantMessage, AssistantRun, UtcDateTime } from '../api/api.models';

/** How an agent turn ended, or `running` while it has not. */
export type AssistantTurnState = 'running' | 'answered' | 'failed' | 'cancelled';

/**
 * One turn as the panel draws it. The stored half of a thread and the run still in flight
 * are projected into the same shape here, so the template renders a live answer and a
 * remembered one with the same markup instead of two near-copies of it.
 */
export interface AssistantTurn {
  readonly key: string;
  readonly isUser: boolean;
  readonly who: string;
  readonly text: string;
  readonly at: UtcDateTime;
  readonly state: AssistantTurnState | null;
  readonly durationMs: number | null;
  readonly commandLine: string | null;
  readonly failureMessage: string | null;
  readonly isTruncated: boolean;
}

/** What a thread is built from: what was kept, and what is happening right now. */
export interface AssistantThreadSource {
  readonly messages: readonly AssistantMessage[];
  readonly run: AssistantRun | null;
  readonly you: string;
  /** Who answered the stored turns. A live turn names its own agent instead. */
  readonly agent: string;
}

/**
 * The stored turns, then the live one. A settled run is dropped from the live half as soon
 * as its thread has been read back, so an answer is never drawn twice.
 */
export function buildThread(source: AssistantThreadSource): readonly AssistantTurn[] {
  const stored = [...source.messages]
    .sort((left, right) => left.position - right.position)
    .map((message) => fromMessage(message, source));
  return source.run === null ? stored : [...stored, ...fromRun(source.run, source.you)];
}

function fromMessage(message: AssistantMessage, source: AssistantThreadSource): AssistantTurn {
  const isUser = message.role === 'user';
  return {
    key: message.id,
    isUser,
    who: isUser ? source.you : source.agent,
    text: message.text,
    at: message.writtenAtUtc,
    state: isUser ? null : stateOf(message.status),
    durationMs: message.durationMs,
    commandLine: message.commandLine,
    failureMessage: message.failureMessage,
    isTruncated: message.isTruncated,
  };
}

/** A run is two turns: the question as it was asked, and the answer as it stands. */
function fromRun(run: AssistantRun, you: string): readonly AssistantTurn[] {
  return [
    {
      key: `${run.runId}:question`,
      isUser: true,
      who: you,
      text: run.question,
      at: run.startedAtUtc,
      state: null,
      durationMs: null,
      commandLine: null,
      failureMessage: null,
      isTruncated: false,
    },
    {
      key: `${run.runId}:answer`,
      isUser: false,
      who: run.agentName,
      text: run.answer ?? '',
      at: run.completedAtUtc ?? run.startedAtUtc,
      state: stateOf(run.status),
      durationMs: elapsed(run),
      commandLine: run.commandLine,
      failureMessage: run.failureMessage,
      isTruncated: run.isTruncated,
    },
  ];
}

function stateOf(status: string | null): AssistantTurnState {
  switch (status) {
    case 'Running':
      return 'running';
    case 'Failed':
      return 'failed';
    case 'Cancelled':
      return 'cancelled';
    default:
      return 'answered';
  }
}

function elapsed(run: AssistantRun): number | null {
  if (run.completedAtUtc === null) {
    return null;
  }

  const span = Date.parse(run.completedAtUtc) - Date.parse(run.startedAtUtc);
  return Number.isNaN(span) || span < 0 ? null : span;
}
