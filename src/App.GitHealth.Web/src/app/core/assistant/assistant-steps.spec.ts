import { AssistantRunStep } from '../api/api.models';
import { describeActivity } from './assistant-steps';

const at = '2026-09-03T09:12:00Z';

function step(overrides: Partial<AssistantRunStep> = {}): AssistantRunStep {
  return { kind: 'Waiting', label: '', detail: null, atUtc: at, ...overrides };
}

describe('describeActivity', () => {
  /**
   * The CLI takes a moment to start, and that moment is the one this list exists to fill.
   * A run with nothing to say yet still has to say that it is starting.
   */
  it('says the agent is starting before it has said anything', () => {
    const activity = describeActivity([]);

    expect(activity).toHaveLength(1);
    expect(activity[0].text).toBe('Starting the agent');
    expect(activity[0].isCurrent).toBe(true);
  });

  it('names each capture tool by what it reads', () => {
    const activity = describeActivity([
      step({ kind: 'Tool', label: 'get_capture' }),
      step({ kind: 'Tool', label: 'list_branches', detail: 'verdict=merged' }),
      step({ kind: 'Tool', label: 'count_branches', detail: 'groupBy=author' }),
    ]);

    expect(activity.map((line) => line.text)).toEqual([
      'Reading the capture',
      'Reading the branches',
      'Counting the branches',
    ]);
    expect(activity[1].detail).toBe('verdict=merged');
  });

  /** A tool this build does not know still reads as the call it was, never as nothing. */
  it('falls back to the tool name for a tool it does not know', () => {
    const activity = describeActivity([step({ kind: 'Tool', label: 'read_the_future' })]);

    expect(activity[0].text).toContain('read_the_future');
  });

  /** Only the last line is happening; the ones above it have happened. */
  it('marks the last step as the one in progress', () => {
    const activity = describeActivity([
      step({ kind: 'Waiting' }),
      step({ kind: 'Thinking' }),
      step({ kind: 'Writing' }),
    ]);

    expect(activity.map((line) => line.isCurrent)).toEqual([false, false, true]);
    expect(activity.map((line) => line.text)).toEqual(['Asking the model', 'Thinking', 'Writing']);
  });

  it('keeps every line distinct, so redrawing does not shuffle them', () => {
    const activity = describeActivity([step({ kind: 'Waiting' }), step({ kind: 'Waiting' })]);

    expect(new Set(activity.map((line) => line.key)).size).toBe(2);
  });
});
