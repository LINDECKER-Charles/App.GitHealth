import { buildPhaseSteps } from './analysis-phase-steps';

describe('buildPhaseSteps', () => {
  it('fills the stages already passed and leaves the ones still to come empty', () => {
    const steps = buildPhaseSteps('Enrichment', 0, 10);

    expect(steps.map((step) => step.fill)).toEqual(['100%', '100%', '4%', '0%', '0%']);
    expect(steps.map((step) => step.isCurrent)).toEqual([false, false, true, false, false]);
  });

  it('moves the current stage with the references it has read', () => {
    expect(buildPhaseSteps('Topology', 5, 10)[1].fill).toBe('50%');
    expect(buildPhaseSteps('Topology', 10, 10)[1].fill).toBe('100%');
  });

  it('shows a started stage even before its first reference lands', () => {
    expect(buildPhaseSteps('Topology', 0, 0)[1].fill).toBe('4%');
  });

  it('closes every stage once the run is finished', () => {
    const steps = buildPhaseSteps('Finished', 10, 10);

    expect(steps.every((step) => step.isDone)).toBe(true);
    expect(steps.some((step) => step.isCurrent)).toBe(false);
  });

  it('freezes the stages where they stood when a run fails', () => {
    const steps = buildPhaseSteps('Failed', 4, 10);

    expect(steps.every((step) => !step.isDone && !step.isCurrent)).toBe(true);
    expect(steps.map((step) => step.fill)).toEqual(['0%', '0%', '0%', '0%', '0%']);
  });
});
