import { BranchSnapshotResponse, PolicySnapshot } from '../../../core/api/api.models';
import {
  ActivityRow,
  activityCounts,
  buildActivityRows,
  buildAxisTicks,
  buildPolicyBands,
  clampThresholds,
  seedDraft,
  thresholdBounds,
  timelineDaysFor,
} from './activity-register';

const day = 86_400_000;
const timelineDays = 120;

const savedPolicy: PolicySnapshot = {
  activeUntilDays: 30,
  inactiveAfterDays: 90,
  protectedPatterns: ['refs/remotes/origin/release/*'],
  excludedPatterns: ['refs/remotes/origin/wip/*'],
};

const defaultBounds = thresholdBounds({ saved: savedPolicy, timelineDays });

function daysAgo(days: number): string {
  return new Date(Date.now() - days * day).toISOString();
}

function branch(
  name: string,
  overrides: Partial<BranchSnapshotResponse> = {},
): BranchSnapshotResponse {
  return {
    id: name,
    referenceName: `refs/remotes/origin/${name}`,
    commitId: 'abc1234',
    aheadCount: 3,
    behindCount: 1,
    relationship: 'CommonAncestor',
    lastActivityAtUtc: daysAgo(5),
    tipAuthor: 'Ada',
    topology: 'Ahead',
    activity: 'Active',
    recommendation: 'Keep',
    reason: '',
    isProtected: false,
    isExcluded: false,
    ...overrides,
  };
}

function rowsFor(
  branches: readonly BranchSnapshotResponse[],
  policy: PolicySnapshot = savedPolicy,
): readonly ActivityRow[] {
  return buildActivityRows({ branches, policy, timelineDays });
}

function singleRow(
  overrides: Partial<BranchSnapshotResponse>,
  policy: PolicySnapshot = savedPolicy,
): ActivityRow {
  return rowsFor([branch('feat/register', overrides)], policy)[0];
}

describe('timelineDaysFor', () => {
  it('reproduces the 120 d axis of the default policy', () => {
    expect(timelineDaysFor(90)).toBe(120);
  });

  it('keeps the 120 d floor for a short policy', () => {
    expect(timelineDaysFor(30)).toBe(120);
  });

  it('extends to the next multiple of 30 d for a long policy', () => {
    expect(timelineDaysFor(200)).toBe(270);
    expect(timelineDaysFor(300)).toBe(420);
  });
});

describe('thresholdBounds', () => {
  it('derives the slider bounds from the domain, keeping the minimum gap', () => {
    expect(defaultBounds).toEqual({
      activeMin: 1,
      activeMax: 112,
      inactiveMin: 9,
      inactiveMax: 120,
    });
  });

  it('lowers the floors down to a saved policy below the minimums', () => {
    const saved = { activeUntilDays: 0, inactiveAfterDays: 5 };
    expect(thresholdBounds({ saved, timelineDays })).toEqual({
      activeMin: 0,
      activeMax: 112,
      inactiveMin: 5,
      inactiveMax: 120,
    });
  });
});

describe('seedDraft', () => {
  it('leaves a 0 / 5 d policy untouched, the thumb staying under its label', () => {
    const saved = { activeUntilDays: 0, inactiveAfterDays: 5 };
    const bounds = thresholdBounds({ saved, timelineDays });
    expect(seedDraft(saved, bounds)).toEqual(saved);
  });

  it('brings a saved threshold beyond the axis back into the domain', () => {
    const saved = { activeUntilDays: 200, inactiveAfterDays: 300 };
    expect(seedDraft(saved, defaultBounds)).toEqual({
      activeUntilDays: 112,
      inactiveAfterDays: 120,
    });
  });
});

describe('clampThresholds', () => {
  it('clamps the active slider without moving the inactive slider', () => {
    const draft = { activeUntilDays: 88, inactiveAfterDays: 90 };
    expect(clampThresholds(draft, 'active', defaultBounds)).toEqual({
      activeUntilDays: 82,
      inactiveAfterDays: 90,
    });
  });

  it('clamps the inactive slider without moving the active slider', () => {
    const draft = { activeUntilDays: 30, inactiveAfterDays: 32 };
    expect(clampThresholds(draft, 'inactive', defaultBounds)).toEqual({
      activeUntilDays: 30,
      inactiveAfterDays: 38,
    });
  });

  it('lets through a draft that already respects the gap', () => {
    const draft = { activeUntilDays: 30, inactiveAfterDays: 90 };
    expect(clampThresholds(draft, 'active', defaultBounds)).toEqual(draft);
  });

  it('takes the active slider down to the floor lowered by the policy', () => {
    const saved = { activeUntilDays: 0, inactiveAfterDays: 5 };
    const bounds = thresholdBounds({ saved, timelineDays });
    const draft = { activeUntilDays: 0, inactiveAfterDays: 20 };
    expect(clampThresholds(draft, 'active', bounds).activeUntilDays).toBe(0);
  });
});

describe('buildPolicyBands', () => {
  it('anchors the three bands to the right, at the threshold percentages', () => {
    expect(buildPolicyBands(savedPolicy, timelineDays)).toEqual({
      activeEdgePercent: 25,
      inactiveEdgePercent: 75,
      agingWidthPercent: 50,
      activeLabel: '30 d',
      inactiveLabel: '90 d',
      isActiveLabelTrailing: false,
      isInactiveLabelTrailing: false,
    });
  });

  it('flips the label to the left of the line when the rule hugs the right edge', () => {
    const policy: PolicySnapshot = { ...savedPolicy, activeUntilDays: 1, inactiveAfterDays: 9 };
    const bands = buildPolicyBands(policy, timelineDays);
    expect(bands.isActiveLabelTrailing).toBe(true);
    expect(bands.isInactiveLabelTrailing).toBe(true);
  });
});

describe('buildAxisTicks', () => {
  it('graduates the domain into five ticks, the present on the right', () => {
    const ticks = buildAxisTicks(timelineDays);
    expect(ticks.map((tick) => tick.label)).toEqual(['120 d ago', '90 d', '60 d', '30 d', 'today']);
    expect(ticks.map((tick) => tick.leftPercent)).toEqual([0, 25, 50, 75, 100]);
    expect(ticks.map((tick) => tick.anchor)).toEqual([
      'start',
      'center',
      'center',
      'center',
      'end',
    ]);
  });
});

describe('buildActivityRows', () => {
  it('projects the age as an offset from the right, the bar measuring the silence', () => {
    const row = singleRow({ lastActivityAtUtc: daysAgo(30) });
    expect(row.offsetPercent).toBe(25);
    expect(row.barWidthPercent).toBe(25);
    expect(row.clampLabel).toBeNull();
  });

  it('clamps to the left edge beyond the domain and states the real age', () => {
    const row = singleRow({ lastActivityAtUtc: daysAgo(200) });
    expect(row.offsetPercent).toBe(100);
    expect(row.clampLabel).toBe('200 d ▸');
  });

  it('brings a future date back to the present rather than to a negative offset', () => {
    const row = singleRow({ lastActivityAtUtc: new Date(Date.now() + 5 * day).toISOString() });
    expect(row.offsetPercent).toBe(0);
    expect(row.ageLabel).toBe('today');
  });

  it('marks unknown activity with no dot and no bar', () => {
    const row = singleRow({ lastActivityAtUtc: null });
    expect(row.activity).toBe('Unknown');
    expect(row.hasMark).toBe(false);
    expect(row.tone).toBe('neutral');
    expect(row.ageLabel).toBe('unknown activity');
  });

  it('makes the verdict follow the draft thresholds', () => {
    const overrides = { lastActivityAtUtc: daysAgo(20) };
    expect(singleRow(overrides).verdictLabel).toBe('Keep');

    const draft: PolicySnapshot = { ...savedPolicy, activeUntilDays: 10, inactiveAfterDays: 15 };
    expect(singleRow(overrides, draft).verdictLabel).toBe('Review');
  });

  it('classes both a protected pattern and an excluded pattern as "Excluded"', () => {
    const rows = rowsFor([
      branch('release/0.1.0', { lastActivityAtUtc: daysAgo(5) }),
      branch('wip/lindecker', { lastActivityAtUtc: daysAgo(5) }),
    ]);
    expect(rows.map((row) => row.verdictLabel)).toEqual(['Excluded', 'Excluded']);
    expect(rows.map((row) => row.flag?.icon)).toEqual(['lock', 'eye-off']);
  });

  it('applies the shortened scale to a merged branch', () => {
    const overrides = { lastActivityAtUtc: daysAgo(10), topology: 'Merged' as const };
    const merged = singleRow(overrides);
    expect(merged.activity).toBe('Aging');
    expect(merged.verdictLabel).toBe('Review');
    expect(singleRow({ ...overrides, topology: 'Ahead' }).activity).toBe('Active');
  });

  it('signals the shortened scale, which the band it crosses does not state', () => {
    const merged = singleRow({ lastActivityAtUtc: daysAgo(40), topology: 'Merged' });
    expect(merged.isReduced).toBe(true);
    expect(merged.activity).toBe('Inactive');
    expect(merged.scaleLabel).toBe('shortened scale · active ≤ 7 d · inactive > 30 d');
    expect(merged.trackLabel).toContain('shortened scale · active ≤ 7 d · inactive > 30 d');

    const diverged = singleRow({ lastActivityAtUtc: daysAgo(40), topology: 'Diverged' });
    expect(diverged.isReduced).toBe(false);
    expect(diverged.scaleLabel).toBeNull();
  });

  it('shows the full name, remote prefix included', () => {
    expect(singleRow({}).name).toBe('origin/feat/register');
  });
});

describe('activityCounts', () => {
  it('counts the branches by recalculated state, unknown activity included', () => {
    const rows = rowsFor([
      branch('a', { lastActivityAtUtc: daysAgo(2) }),
      branch('b', { lastActivityAtUtc: daysAgo(10) }),
      branch('c', { lastActivityAtUtc: daysAgo(45) }),
      branch('d', { lastActivityAtUtc: daysAgo(200) }),
      branch('e', { lastActivityAtUtc: null }),
    ]);
    expect(activityCounts(rows)).toEqual({ active: 2, aging: 1, inactive: 1, unknown: 1 });
  });
});
