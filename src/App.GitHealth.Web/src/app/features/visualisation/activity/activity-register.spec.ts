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
  return rowsFor([branch('feat/registre', overrides)], policy)[0];
}

describe('timelineDaysFor', () => {
  it('reproduit l’axe de 120 j de la politique par défaut', () => {
    expect(timelineDaysFor(90)).toBe(120);
  });

  it('garde le plancher de 120 j pour une politique courte', () => {
    expect(timelineDaysFor(30)).toBe(120);
  });

  it('s’étend au multiple de 30 j suivant pour une politique longue', () => {
    expect(timelineDaysFor(200)).toBe(270);
    expect(timelineDaysFor(300)).toBe(420);
  });
});

describe('thresholdBounds', () => {
  it('dérive les bornes des curseurs du domaine, en gardant l’écart minimal', () => {
    expect(defaultBounds).toEqual({
      activeMin: 1,
      activeMax: 112,
      inactiveMin: 9,
      inactiveMax: 120,
    });
  });

  it('abaisse les planchers jusqu’à une politique enregistrée sous les minimums', () => {
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
  it('laisse intacte une politique de 0 / 5 j, le pouce restant sous son étiquette', () => {
    const saved = { activeUntilDays: 0, inactiveAfterDays: 5 };
    const bounds = thresholdBounds({ saved, timelineDays });
    expect(seedDraft(saved, bounds)).toEqual(saved);
  });

  it('ramène dans le domaine un seuil enregistré au-delà de l’axe', () => {
    const saved = { activeUntilDays: 200, inactiveAfterDays: 300 };
    expect(seedDraft(saved, defaultBounds)).toEqual({
      activeUntilDays: 112,
      inactiveAfterDays: 120,
    });
  });
});

describe('clampThresholds', () => {
  it('écrête le curseur actif sans déplacer le curseur inactif', () => {
    const draft = { activeUntilDays: 88, inactiveAfterDays: 90 };
    expect(clampThresholds(draft, 'active', defaultBounds)).toEqual({
      activeUntilDays: 82,
      inactiveAfterDays: 90,
    });
  });

  it('écrête le curseur inactif sans déplacer le curseur actif', () => {
    const draft = { activeUntilDays: 30, inactiveAfterDays: 32 };
    expect(clampThresholds(draft, 'inactive', defaultBounds)).toEqual({
      activeUntilDays: 30,
      inactiveAfterDays: 38,
    });
  });

  it('laisse passer un brouillon qui respecte déjà l’écart', () => {
    const draft = { activeUntilDays: 30, inactiveAfterDays: 90 };
    expect(clampThresholds(draft, 'active', defaultBounds)).toEqual(draft);
  });

  it('descend le curseur actif jusqu’au plancher abaissé par la politique', () => {
    const saved = { activeUntilDays: 0, inactiveAfterDays: 5 };
    const bounds = thresholdBounds({ saved, timelineDays });
    const draft = { activeUntilDays: 0, inactiveAfterDays: 20 };
    expect(clampThresholds(draft, 'active', bounds).activeUntilDays).toBe(0);
  });
});

describe('buildPolicyBands', () => {
  it('ancre les trois zones à droite, aux pourcentages des seuils', () => {
    expect(buildPolicyBands(savedPolicy, timelineDays)).toEqual({
      activeEdgePercent: 25,
      inactiveEdgePercent: 75,
      agingWidthPercent: 50,
      activeLabel: '30 j',
      inactiveLabel: '90 j',
      isActiveLabelTrailing: false,
      isInactiveLabelTrailing: false,
    });
  });

  it('bascule l’étiquette à gauche du trait quand la règle colle au bord droit', () => {
    const policy: PolicySnapshot = { ...savedPolicy, activeUntilDays: 1, inactiveAfterDays: 9 };
    const bands = buildPolicyBands(policy, timelineDays);
    expect(bands.isActiveLabelTrailing).toBe(true);
    expect(bands.isInactiveLabelTrailing).toBe(true);
  });
});

describe('buildAxisTicks', () => {
  it('gradue le domaine en cinq repères, le présent à droite', () => {
    const ticks = buildAxisTicks(timelineDays);
    expect(ticks.map((tick) => tick.label)).toEqual([
      'il y a 120 j',
      '90 j',
      '60 j',
      '30 j',
      "aujourd'hui",
    ]);
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
  it('projette l’âge en décalage depuis la droite, la barre mesurant le silence', () => {
    const row = singleRow({ lastActivityAtUtc: daysAgo(30) });
    expect(row.offsetPercent).toBe(25);
    expect(row.barWidthPercent).toBe(25);
    expect(row.clampLabel).toBeNull();
  });

  it('écrête au bord gauche au-delà du domaine et dit l’âge réel', () => {
    const row = singleRow({ lastActivityAtUtc: daysAgo(200) });
    expect(row.offsetPercent).toBe(100);
    expect(row.clampLabel).toBe('200 j ▸');
  });

  it('ramène une date future au présent plutôt qu’à un décalage négatif', () => {
    const row = singleRow({ lastActivityAtUtc: new Date(Date.now() + 5 * day).toISOString() });
    expect(row.offsetPercent).toBe(0);
    expect(row.ageLabel).toBe("aujourd'hui");
  });

  it('marque l’activité inconnue sans point ni barre', () => {
    const row = singleRow({ lastActivityAtUtc: null });
    expect(row.activity).toBe('Unknown');
    expect(row.hasMark).toBe(false);
    expect(row.tone).toBe('neutral');
    expect(row.ageLabel).toBe('activité inconnue');
  });

  it('fait suivre le verdict aux seuils du brouillon', () => {
    const overrides = { lastActivityAtUtc: daysAgo(20) };
    expect(singleRow(overrides).verdictLabel).toBe('Conserver');

    const draft: PolicySnapshot = { ...savedPolicy, activeUntilDays: 10, inactiveAfterDays: 15 };
    expect(singleRow(overrides, draft).verdictLabel).toBe('À examiner');
  });

  it('classe « Exclue » aussi bien un motif protégé qu’un motif exclu', () => {
    const rows = rowsFor([
      branch('release/0.1.0', { lastActivityAtUtc: daysAgo(5) }),
      branch('wip/lindecker', { lastActivityAtUtc: daysAgo(5) }),
    ]);
    expect(rows.map((row) => row.verdictLabel)).toEqual(['Exclue', 'Exclue']);
    expect(rows.map((row) => row.flag?.icon)).toEqual(['lock', 'eye-off']);
  });

  it('applique l’échelle réduite à une branche fusionnée', () => {
    const overrides = { lastActivityAtUtc: daysAgo(10), topology: 'Merged' as const };
    const merged = singleRow(overrides);
    expect(merged.activity).toBe('Aging');
    expect(merged.verdictLabel).toBe('À examiner');
    expect(singleRow({ ...overrides, topology: 'Ahead' }).activity).toBe('Active');
  });

  it('signale l’échelle réduite, que la zone traversée ne dit pas', () => {
    const merged = singleRow({ lastActivityAtUtc: daysAgo(40), topology: 'Merged' });
    expect(merged.isReduced).toBe(true);
    expect(merged.activity).toBe('Inactive');
    expect(merged.scaleLabel).toBe('échelle réduite · active ≤ 7 j · inactive > 30 j');
    expect(merged.trackLabel).toContain('échelle réduite · active ≤ 7 j · inactive > 30 j');

    const diverged = singleRow({ lastActivityAtUtc: daysAgo(40), topology: 'Diverged' });
    expect(diverged.isReduced).toBe(false);
    expect(diverged.scaleLabel).toBeNull();
  });

  it('affiche le nom complet, préfixe distant compris', () => {
    expect(singleRow({}).name).toBe('origin/feat/registre');
  });
});

describe('activityCounts', () => {
  it('compte les branches par état recalculé, activité inconnue comprise', () => {
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
