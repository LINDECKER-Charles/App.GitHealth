/** One step of the opening sequence: what GitHealth does, in the order it does it. */
export interface BootStep {
  readonly label: string;
  readonly revealDelayMs: number;
  readonly settleDelayMs: number;
}

const firstStepDelayMs = 1050;
const stepIntervalMs = 540;
const settleOffsetMs = 430;

const stepLabels: readonly string[] = [
  $localize`:@@boot.step.openRepository:Opening the repository`,
  $localize`:@@boot.step.readReferences:Reading the references`,
  $localize`:@@boot.step.resolveBaseline:Resolving the baseline`,
  $localize`:@@boot.step.findMergeBases:Finding the merge bases`,
  $localize`:@@boot.step.countAheadBehind:Counting ahead / behind`,
  $localize`:@@boot.step.aggregateContributors:Aggregating the contributors`,
  $localize`:@@boot.step.applyPolicy:Applying the policy`,
  $localize`:@@boot.step.saveSnapshot:Saving the snapshot`,
];

export const bootSteps: readonly BootStep[] = stepLabels.map((label, index) => {
  const revealDelayMs = firstStepDelayMs + index * stepIntervalMs;
  return { label, revealDelayMs, settleDelayMs: revealDelayMs + settleOffsetMs };
});

/** The moment the veil starts to fade, then the moment the component withdraws. */
export const bootFadeStartMs = 8350;
export const bootCompleteMs = 8800;

/** The window during which the counters climb towards the snapshot's real figures. */
export const counterStartMs = 5500;
export const counterDurationMs = 1900;
