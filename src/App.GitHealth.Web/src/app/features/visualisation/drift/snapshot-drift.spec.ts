import { BranchSnapshotResponse, RecommendationKind } from '../../../core/api/api.models';
import {
  Drift,
  DriftCapture,
  DriftKind,
  DriftRow,
  buildDrift,
  clampCaptureSelection,
  driftGridColumns,
  driftLegend,
  hasTruncatedBranchList,
} from './snapshot-drift';

type Verdicts = Readonly<Record<string, RecommendationKind>>;

function branch(name: string, recommendation: RecommendationKind, short: string) {
  return {
    id: `${name}-${short}`,
    referenceName: `refs/heads/${name}`,
    commitId: 'abc1234',
    aheadCount: 1,
    behindCount: 0,
    relationship: 'CommonAncestor',
    lastActivityAtUtc: null,
    tipAuthor: null,
    topology: 'Ahead',
    activity: 'Active',
    recommendation,
    reason: `reason ${short}`,
    isProtected: false,
    isExcluded: false,
  } satisfies BranchSnapshotResponse;
}

function capture(short: string, verdicts: Verdicts): DriftCapture {
  const entries = Object.entries(verdicts).map(
    ([name, recommendation]) =>
      [`refs/heads/${name}`, branch(name, recommendation, short)] as const,
  );
  return {
    analysisId: short,
    short,
    label: `${short} · abcd1234`,
    branches: new Map(entries),
    isBranchListTruncated: false,
  };
}

function rowOf(drift: Drift, kind: DriftKind, name: string): DriftRow | undefined {
  return drift.groups.find((group) => group.kind === kind)?.rows.find((row) => row.name === name);
}

function namesOf(drift: Drift, kind: DriftKind): readonly string[] {
  return (drift.groups.find((group) => group.kind === kind)?.rows ?? []).map((row) => row.name);
}

function statOf(drift: Drift, label: string): number {
  return drift.stats.find((stat) => stat.label === label)?.count ?? -1;
}

const captures: readonly DriftCapture[] = [
  capture('2 Jul', {
    alpha: 'Keep',
    beta: 'Review',
    gamma: 'Keep',
    delta: 'Keep',
    old: 'Keep',
  }),
  capture('16 Aug', { alpha: 'Keep', beta: 'Review', gamma: 'Keep', delta: 'Keep' }),
  capture('today', {
    alpha: 'CleanupCandidate',
    beta: 'Keep',
    delta: 'Keep',
    epsilon: 'Keep',
  }),
];

const drift = buildDrift({ captures, fromIndex: 1, toIndex: 2 });

describe('snapshot-drift', () => {
  it('files every branch under the movement that describes it', () => {
    expect(namesOf(drift, 'worse')).toEqual(['alpha']);
    expect(namesOf(drift, 'better')).toEqual(['beta']);
    expect(namesOf(drift, 'gone')).toEqual(['gamma']);
    expect(namesOf(drift, 'same')).toEqual(['delta']);
    expect(namesOf(drift, 'fresh')).toEqual(['epsilon']);
  });

  it('ignores a branch missing from both compared captures', () => {
    const total = drift.groups.reduce((sum, group) => sum + group.count, 0);
    expect(total).toBe(5);
    expect(namesOf(drift, 'gone')).not.toContain('old');
  });

  it('orders the journal from the degraded to the unchanged', () => {
    expect(drift.groups.map((group) => group.kind)).toEqual([
      'worse',
      'better',
      'fresh',
      'gone',
      'same',
    ]);
  });

  it('opens the summary on the resolutions and does not count the unchanged there', () => {
    expect(drift.summary).toBe(
      'Between 16 Aug and today: 1 resolution, 1 degradation, 1 new branch, 1 removed branch.',
    );
  });

  it('keeps the singular for one alone', () => {
    const pair = [
      capture('a', { one: 'Review', two: 'Review' }),
      capture('b', { one: 'Keep', two: 'Keep' }),
    ];
    expect(buildDrift({ captures: pair, fromIndex: 0, toIndex: 1 }).summary).toBe(
      'Between a and b: 2 resolutions, 0 degradations, 0 new branches, 0 removed branches.',
    );
  });

  it('counts an arrival at Done as a resolution', () => {
    const pair = [capture('a', { x: 'CleanupCandidate' }), capture('b', { x: 'Merged' })];
    expect(namesOf(buildDrift({ captures: pair, fromIndex: 0, toIndex: 1 }), 'better')).toEqual([
      'x',
    ]);
  });

  it('keeps both oddities of the rule: Done → Review and Keep → Excluded', () => {
    const pair = [
      capture('a', { x: 'Merged', y: 'Keep' }),
      capture('b', { x: 'Review', y: 'Excluded' }),
    ];
    const quirks = buildDrift({ captures: pair, fromIndex: 0, toIndex: 1 });
    expect(namesOf(quirks, 'better')).toEqual(['x']);
    expect(namesOf(quirks, 'worse')).toEqual(['y']);
  });

  it('marks the missing cells and lights up only the two compared captures', () => {
    const cells = rowOf(drift, 'fresh', 'epsilon')?.cells ?? [];
    expect(cells.map((cell) => cell.tone)).toEqual([null, null, 'success']);
    expect(cells.map((cell) => cell.isCompared)).toEqual([false, true, true]);
    expect(cells[0].title).toBe('2 Jul: absent');
    expect(cells[2].title).toBe('today: Keep');
  });

  it('names the missing verdicts "absent" at the start and "removed" at the end', () => {
    expect(rowOf(drift, 'fresh', 'epsilon')?.fromLabel).toBe('absent');
    expect(rowOf(drift, 'gone', 'gamma')?.toLabel).toBe('removed');
  });

  it('reads the reason of a branch that is gone from the starting capture', () => {
    expect(rowOf(drift, 'gone', 'gamma')?.note).toBe('reason 16 Aug');
    expect(rowOf(drift, 'fresh', 'epsilon')?.note).toBe(
      'created after the 16 Aug capture — reason today',
    );
  });

  it('drops an empty group without touching the five statistics', () => {
    const pair = [capture('a', { x: 'Keep' }), capture('b', { x: 'Keep' })];
    const quiet = buildDrift({ captures: pair, fromIndex: 0, toIndex: 1 });
    expect(quiet.groups.map((group) => group.kind)).toEqual(['same']);
    expect(quiet.stats).toHaveLength(5);
    expect(statOf(quiet, 'unchanged')).toBe(1);
    expect(statOf(quiet, 'degraded')).toBe(0);
  });

  it('makes only the unchanged group collapsible', () => {
    expect(drift.groups.filter((group) => group.isCollapsible).map((group) => group.kind)).toEqual([
      'same',
    ]);
  });
});

describe('clampCaptureSelection', () => {
  it('pushes the end one notch away when the start catches up with it', () => {
    expect(clampCaptureSelection({ count: 6, fromIndex: 4, toIndex: 2, moved: 'from' })).toEqual({
      fromIndex: 4,
      toIndex: 5,
    });
  });

  it('pulls the start one notch back when the end moves ahead of it', () => {
    expect(clampCaptureSelection({ count: 6, fromIndex: 3, toIndex: 1, moved: 'to' })).toEqual({
      fromIndex: 0,
      toIndex: 1,
    });
  });

  it('leaves an already ordered selection untouched', () => {
    expect(clampCaptureSelection({ count: 6, fromIndex: 1, toIndex: 4, moved: 'to' })).toEqual({
      fromIndex: 1,
      toIndex: 4,
    });
  });

  it('forbids the start from being the last capture', () => {
    expect(clampCaptureSelection({ count: 3, fromIndex: 2, toIndex: 2, moved: 'from' })).toEqual({
      fromIndex: 1,
      toIndex: 2,
    });
  });
});

describe('driftGridColumns', () => {
  it('measures the history strip on the number of loaded captures', () => {
    expect(driftGridColumns(6).startsWith('96px ')).toBe(true);
    expect(driftGridColumns(3).startsWith('51px ')).toBe(true);
  });
});

describe('driftLegend', () => {
  it('names the first and the last loaded capture', () => {
    expect(driftLegend(captures, false)).toContain('from the oldest (2 Jul) to today.');
  });

  it('says the history is cut rather than letting it look complete', () => {
    expect(driftLegend(captures, true)).toBe(
      'Each cell follows the verdict of one capture, from the first loaded (2 Jul) to today.' +
        ' Only the last 6 captures are shown.' +
        ' Pale cells are outside the chosen comparison.',
    );
  });
});

describe('hasTruncatedBranchList', () => {
  it('reports nothing as long as every capture was read in full', () => {
    expect(hasTruncatedBranchList(captures)).toBe(false);
  });

  it('reports a partial comparison as soon as a single capture was cut', () => {
    const partial = { ...capture('b', { x: 'Keep' }), isBranchListTruncated: true };
    expect(hasTruncatedBranchList([capture('a', { x: 'Keep' }), partial])).toBe(true);
  });
});
