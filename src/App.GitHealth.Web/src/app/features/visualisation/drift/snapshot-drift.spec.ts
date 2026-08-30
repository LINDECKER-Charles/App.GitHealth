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
    reason: `raison ${short}`,
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
  capture('2 juil', {
    alpha: 'Keep',
    beta: 'Review',
    gamma: 'Keep',
    delta: 'Keep',
    vieille: 'Keep',
  }),
  capture('16 août', { alpha: 'Keep', beta: 'Review', gamma: 'Keep', delta: 'Keep' }),
  capture("aujourd'hui", {
    alpha: 'CleanupCandidate',
    beta: 'Keep',
    delta: 'Keep',
    epsilon: 'Keep',
  }),
];

const drift = buildDrift({ captures, fromIndex: 1, toIndex: 2 });

describe('snapshot-drift', () => {
  it('range chaque branche dans le mouvement qui la décrit', () => {
    expect(namesOf(drift, 'worse')).toEqual(['alpha']);
    expect(namesOf(drift, 'better')).toEqual(['beta']);
    expect(namesOf(drift, 'gone')).toEqual(['gamma']);
    expect(namesOf(drift, 'same')).toEqual(['delta']);
    expect(namesOf(drift, 'fresh')).toEqual(['epsilon']);
  });

  it('ignore une branche absente des deux captures comparées', () => {
    const total = drift.groups.reduce((sum, group) => sum + group.count, 0);
    expect(total).toBe(5);
    expect(namesOf(drift, 'gone')).not.toContain('vieille');
  });

  it('ordonne le journal des dégradées aux inchangées', () => {
    expect(drift.groups.map((group) => group.kind)).toEqual([
      'worse',
      'better',
      'fresh',
      'gone',
      'same',
    ]);
  });

  it('ouvre le résumé sur les résolutions et n’y compte pas les inchangées', () => {
    expect(drift.summary).toBe(
      "Entre 16 août et aujourd'hui : 1 résolution, 1 dégradation, 1 nouvelle, 1 supprimée.",
    );
  });

  it('n’accorde le pluriel qu’au-delà de un', () => {
    const pair = [
      capture('a', { un: 'Review', deux: 'Review' }),
      capture('b', { un: 'Keep', deux: 'Keep' }),
    ];
    expect(buildDrift({ captures: pair, fromIndex: 0, toIndex: 1 }).summary).toBe(
      'Entre a et b : 2 résolutions, 0 dégradation, 0 nouvelle, 0 supprimée.',
    );
  });

  it('compte une arrivée à Terminée comme une résolution', () => {
    const pair = [capture('a', { x: 'CleanupCandidate' }), capture('b', { x: 'Merged' })];
    expect(namesOf(buildDrift({ captures: pair, fromIndex: 0, toIndex: 1 }), 'better')).toEqual([
      'x',
    ]);
  });

  it('conserve les deux bizarreries de la règle : Terminée → À examiner et Conserver → Exclue', () => {
    const pair = [
      capture('a', { x: 'Merged', y: 'Keep' }),
      capture('b', { x: 'Review', y: 'Excluded' }),
    ];
    const quirks = buildDrift({ captures: pair, fromIndex: 0, toIndex: 1 });
    expect(namesOf(quirks, 'better')).toEqual(['x']);
    expect(namesOf(quirks, 'worse')).toEqual(['y']);
  });

  it('marque les cases absentes et n’éclaire que les deux captures comparées', () => {
    const cells = rowOf(drift, 'fresh', 'epsilon')?.cells ?? [];
    expect(cells.map((cell) => cell.tone)).toEqual([null, null, 'success']);
    expect(cells.map((cell) => cell.isCompared)).toEqual([false, true, true]);
    expect(cells[0].title).toBe('2 juil : absente');
    expect(cells[2].title).toBe("aujourd'hui : Conserver");
  });

  it('nomme les verdicts absents « absente » au départ et « supprimée » à l’arrivée', () => {
    expect(rowOf(drift, 'fresh', 'epsilon')?.fromLabel).toBe('absente');
    expect(rowOf(drift, 'gone', 'gamma')?.toLabel).toBe('supprimée');
  });

  it('lit la raison d’une branche disparue dans la capture de départ', () => {
    expect(rowOf(drift, 'gone', 'gamma')?.note).toBe('raison 16 août');
    expect(rowOf(drift, 'fresh', 'epsilon')?.note).toBe(
      "créée après la capture du 16 août — raison aujourd'hui",
    );
  });

  it('fait disparaître un groupe vide sans toucher aux cinq statistiques', () => {
    const pair = [capture('a', { x: 'Keep' }), capture('b', { x: 'Keep' })];
    const quiet = buildDrift({ captures: pair, fromIndex: 0, toIndex: 1 });
    expect(quiet.groups.map((group) => group.kind)).toEqual(['same']);
    expect(quiet.stats).toHaveLength(5);
    expect(statOf(quiet, 'inchangées')).toBe(1);
    expect(statOf(quiet, 'dégradées')).toBe(0);
  });

  it('ne rend repliable que le groupe des inchangées', () => {
    expect(drift.groups.filter((group) => group.isCollapsible).map((group) => group.kind)).toEqual([
      'same',
    ]);
  });
});

describe('clampCaptureSelection', () => {
  it('repousse l’arrivée d’un cran quand le départ la rattrape', () => {
    expect(clampCaptureSelection({ count: 6, fromIndex: 4, toIndex: 2, moved: 'from' })).toEqual({
      fromIndex: 4,
      toIndex: 5,
    });
  });

  it('tire le départ d’un cran quand l’arrivée passe devant', () => {
    expect(clampCaptureSelection({ count: 6, fromIndex: 3, toIndex: 1, moved: 'to' })).toEqual({
      fromIndex: 0,
      toIndex: 1,
    });
  });

  it('laisse une sélection déjà ordonnée intacte', () => {
    expect(clampCaptureSelection({ count: 6, fromIndex: 1, toIndex: 4, moved: 'to' })).toEqual({
      fromIndex: 1,
      toIndex: 4,
    });
  });

  it('interdit au départ d’être la dernière capture', () => {
    expect(clampCaptureSelection({ count: 3, fromIndex: 2, toIndex: 2, moved: 'from' })).toEqual({
      fromIndex: 1,
      toIndex: 2,
    });
  });
});

describe('driftGridColumns', () => {
  it('mesure la bande d’historique sur le nombre de captures chargées', () => {
    expect(driftGridColumns(6).startsWith('96px ')).toBe(true);
    expect(driftGridColumns(3).startsWith('51px ')).toBe(true);
  });
});

describe('driftLegend', () => {
  it('nomme la première et la dernière capture chargées', () => {
    expect(driftLegend(captures, false)).toContain("de la plus ancienne (2 juil) à aujourd'hui.");
  });

  it('dit que l’historique est coupé plutôt que de laisser croire qu’il est complet', () => {
    expect(driftLegend(captures, true)).toBe(
      "Chaque case suit le verdict d’une capture, de la première chargée (2 juil) à aujourd'hui." +
        ' Seules les 6 dernières captures sont affichées.' +
        ' Les cases pâlies sont hors de la comparaison choisie.',
    );
  });
});

describe('hasTruncatedBranchList', () => {
  it('ne signale rien tant que chaque capture a été lue en entier', () => {
    expect(hasTruncatedBranchList(captures)).toBe(false);
  });

  it('signale une comparaison partielle dès qu’une seule capture a été coupée', () => {
    const partial = { ...capture('b', { x: 'Keep' }), isBranchListTruncated: true };
    expect(hasTruncatedBranchList([capture('a', { x: 'Keep' }), partial])).toBe(true);
  });
});
