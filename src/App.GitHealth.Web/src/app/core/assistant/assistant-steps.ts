import { AssistantRunStep } from '../api/api.models';

/**
 * One line of the live activity. The API names what the agent is doing; the phrasing is
 * this application's, in this application's language, like every other label it shows.
 */
export interface AssistantActivityLine {
  readonly key: string;
  readonly text: string;
  /** What the call asked for, or what the agent said of its own reasoning. */
  readonly detail: string | null;
  /** The last line, which is what is happening right now rather than what happened. */
  readonly isCurrent: boolean;
}

const startingLabel = $localize`:@@assistant.step.starting:Starting the agent`;
const waitingLabel = $localize`:@@assistant.step.waiting:Asking the model`;
const thinkingLabel = $localize`:@@assistant.step.thinking:Thinking`;
const writingLabel = $localize`:@@assistant.step.writing:Writing`;

/** Named for what each capture tool reads, not for the tool it is. */
const toolLabels: Readonly<Record<string, string>> = {
  get_capture: $localize`:@@assistant.step.tool.capture:Reading the capture`,
  list_branches: $localize`:@@assistant.step.tool.list:Reading the branches`,
  get_branch: $localize`:@@assistant.step.tool.branch:Reading one branch`,
  count_branches: $localize`:@@assistant.step.tool.count:Counting the branches`,
};

/**
 * What the agent has been doing, as a reader follows it. A run that has said nothing yet
 * still gets a line: the CLI takes a moment to start, and that moment is exactly the one
 * this list exists to fill.
 */
export function describeActivity(
  steps: readonly AssistantRunStep[],
): readonly AssistantActivityLine[] {
  if (steps.length === 0) {
    return [{ key: 'starting', text: startingLabel, detail: null, isCurrent: true }];
  }

  return steps.map((step, index) => ({
    key: `${index}:${step.atUtc}`,
    text: describe(step),
    detail: step.detail,
    isCurrent: index === steps.length - 1,
  }));
}

function describe(step: AssistantRunStep): string {
  switch (step.kind) {
    case 'Waiting':
      return waitingLabel;
    case 'Thinking':
      return thinkingLabel;
    case 'Writing':
      return writingLabel;
    default:
      return toolLabels[step.label] ?? otherTool(step.label);
  }
}

/** A tool this build does not know the name of still reads as the call it was. */
function otherTool(label: string): string {
  return $localize`:@@assistant.step.tool.other:Calling ${label}:tool:`;
}
