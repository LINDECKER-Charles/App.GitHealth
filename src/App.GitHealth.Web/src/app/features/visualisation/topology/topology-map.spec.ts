import { BranchSnapshotResponse } from '../../../core/api/api.models';
import { TopologyFilter } from './topology-layout';
import { TopologyMap, buildTopologyMap } from './topology-map';

const referencePrefix = 'refs/remotes/origin/';

/** L'identité d'un noeud est son nom de référence : les tests visent la même clé que la vue. */
function ref(name: string): string {
  return `${referencePrefix}${name}`;
}

function branch(
  id: string,
  overrides: Partial<BranchSnapshotResponse> = {},
): BranchSnapshotResponse {
  return {
    id,
    referenceName: ref(id),
    commitId: 'abc1234',
    aheadCount: 0,
    behindCount: 0,
    relationship: 'CommonAncestor',
    lastActivityAtUtc: null,
    tipAuthor: 'Ada Lovelace',
    topology: 'Diverged',
    activity: 'Active',
    recommendation: 'Review',
    reason: 'Divergence active.',
    isProtected: false,
    isExcluded: false,
    ...overrides,
  };
}

function build(
  branches: readonly BranchSnapshotResponse[],
  filter: TopologyFilter = 'all',
  focusedId: string | null = null,
): TopologyMap {
  return buildTopologyMap({ branches, filter, focusedId });
}

const mixed: readonly BranchSnapshotResponse[] = [
  branch('diverged', { behindCount: 3, aheadCount: 2 }),
  branch('ahead', { topology: 'Ahead', aheadCount: 4 }),
  branch('merged', { topology: 'Merged', behindCount: 8 }),
  branch('sync', { topology: 'Synchronized' }),
  branch('unrelated', { topology: 'Unrelated', aheadCount: 9 }),
];

describe('buildTopologyMap', () => {
  it('plafonne l’échelle à 12 px par commit sur un dépôt presque à jour', () => {
    const map = build([
      branch('ahead', { topology: 'Ahead', aheadCount: 3 }),
      branch('late', { behindCount: 2, aheadCount: 1 }),
    ]);

    expect(map.nodes[0].id).toBe(ref('late'));
    expect(map.nodes[0].junction.x).toBe(830 - 2 * 12);
  });

  it('plancher l’échelle à 4,5 px par commit et ramène le fork sur la butée de 120', () => {
    const map = build([
      branch('vieille', { behindCount: 200 }),
      branch('proche', { behindCount: 10 }),
    ]);

    expect(map.nodes[0].junction.x).toBe(120);
    expect(map.nodes[1].junction.x).toBe(830 - 10 * 4.5);
  });

  it('écarte les forks d’au moins 18 px sans jamais les pousser vers la droite', () => {
    const map = build([
      branch('a', { topology: 'Ahead', aheadCount: 1 }),
      branch('b', { topology: 'Ahead', aheadCount: 1 }),
      branch('c', { topology: 'Ahead', aheadCount: 1 }),
    ]);

    expect(map.nodes.map((node) => node.junction.x)).toEqual([794, 812, 830]);
    for (const node of map.nodes) {
      expect(node.junction.x).toBeLessThanOrEqual(830);
    }
  });

  it('borne la pointe d’une branche très en avance à 940', () => {
    const map = build([branch('longue', { topology: 'Ahead', aheadCount: 100 })]);

    expect(map.nodes[0].tip.x).toBe(940);
  });

  it('pose les ponts fusionnés sous le tronc, larges de 92 px, sans dépasser HEAD', () => {
    const map = build([
      branch('m1', { topology: 'Merged', behindCount: 10 }),
      branch('m2', { topology: 'Merged', behindCount: 0 }),
    ]);

    for (const node of map.nodes) {
      expect(node.tip.x - node.junction.x).toBe(92);
      expect(node.junction.y).toBe(map.trunkY);
      expect(node.tip.y).toBe(map.trunkY);
    }

    expect(map.nodes[0].path).toContain('Q 664 152');
    expect(map.nodes[1].tip.x).toBe(map.headX);
  });

  it('dessine toutes les branches synchronisées en boucles concentriques', () => {
    const map = build([
      branch('s1', { topology: 'Synchronized' }),
      branch('s2', { topology: 'Synchronized' }),
      branch('s3', { topology: 'Synchronized' }),
    ]);

    expect(map.nodes).toHaveLength(3);
    expect(map.nodes.map((node) => node.path.includes('A 11 11'))).toEqual([true, false, false]);
    expect(map.nodes[1].path).toContain('A 17 17');
    expect(map.nodes[2].path).toContain('A 23 23');
  });

  it('dessine toutes les branches sans base commune et les garde dans le cadre', () => {
    const map = build([
      branch('u1', { topology: 'Unrelated', aheadCount: 5 }),
      branch('u2', { topology: 'Unrelated', aheadCount: 400 }),
    ]);

    expect(map.nodes).toHaveLength(2);
    expect(map.nodes.map((node) => node.tip.x)).toEqual([120, 940]);
    expect(map.nodes[1].tip.y - map.nodes[0].tip.y).toBe(36);
    expect(map.nodes[0].dash).toBe('5 5');
  });

  it('le filtre « ouvertes » ne retire que les fusionnées', () => {
    const ids = build(mixed, 'open').nodes.map((node) => node.id);

    expect(ids).toEqual([ref('diverged'), ref('ahead'), ref('sync'), ref('unrelated')]);
  });

  it('le filtre « fusionnées » ne garde que les fusionnées', () => {
    const ids = build(mixed, 'merged').nodes.map((node) => node.id);

    expect(ids).toEqual([ref('merged')]);
  });

  it('fait grandir la hauteur du cadre avec le nombre de branches ouvertes', () => {
    const few = build([branch('a'), branch('b'), branch('c')]);
    const many = build(Array.from({ length: 12 }, (_, index) => branch(`b${index}`)));

    expect(few.trunkY).toBe(56 + 2 * 36 + 44);
    expect(many.trunkY).toBe(56 + 11 * 36 + 44);
    expect(many.height).toBeGreaterThan(few.height);
    expect(few.viewBox).toBe(`0 0 990 ${few.height}`);
  });

  it('compte tout le snapshot, y compris ce que le filtre actif masque', () => {
    const map = build(mixed, 'merged');

    expect(map.nodes).toHaveLength(1);
    expect(map.counts).toEqual({
      total: 5,
      open: 2,
      merged: 1,
      synchronized: 1,
      unrelated: 1,
    });
  });

  it('trace en pointillés 4 4 une branche couverte par un motif exclu', () => {
    const map = build([
      branch('exclue', { isExcluded: true }),
      branch('isolee', { topology: 'Unrelated', aheadCount: 3 }),
      branch('normale', {}),
    ]);
    const dashes = new Map(map.nodes.map((node) => [node.id, node.dash]));

    expect(dashes.get(ref('exclue'))).toBe('4 4');
    expect(dashes.get(ref('isolee'))).toBe('5 5');
    expect(dashes.get(ref('normale'))).toBeNull();
  });

  it('épaissit la branche au focus et estompe les autres', () => {
    const map = build([branch('vue'), branch('autre')], 'all', ref('vue'));
    const focused = map.nodes.find((node) => node.id === ref('vue'));
    const dimmed = map.nodes.find((node) => node.id === ref('autre'));

    expect(focused?.strokeWidth).toBe(3);
    expect(focused?.opacity).toBe(1);
    expect(dimmed?.strokeWidth).toBe(2);
    expect(dimmed?.opacity).toBe(0.22);
    expect(dimmed?.labelOpacity).toBe(0.25);
  });

  it('n’estompe personne quand le focus ne désigne aucune branche du plan', () => {
    const map = build([branch('vue'), branch('autre')], 'all', ref('analyse-precedente'));

    expect(map.nodes).toHaveLength(2);
    for (const node of map.nodes) {
      expect(node.isFocused).toBe(false);
      expect(node.opacity).toBe(1);
      expect(node.labelOpacity).toBe(1);
      expect(node.strokeWidth).toBe(2);
    }
  });

  it('positionne les étiquettes en pourcentage du cadre', () => {
    const map = build([branch('feat/carte', { topology: 'Ahead', aheadCount: 2 })]);
    const node = map.nodes[0];

    expect(node.name).toBe('feat/carte');
    expect(node.gap).toBe('+2 / 0');
    expect(node.namePosition.left).toMatch(/^\d+\.\d{2}%$/);
    expect(node.namePosition.top).toMatch(/^\d+\.\d{2}%$/);
  });

  it('bascule à gauche les étiquettes ancrées au-delà de 82 % du cadre', () => {
    const map = build([
      branch('au-bord', { topology: 'Ahead', aheadCount: 1 }),
      branch('ancienne', { behindCount: 60, aheadCount: 1 }),
    ]);
    const flipped = map.nodes.find((node) => node.id === ref('au-bord'));
    const kept = map.nodes.find((node) => node.id === ref('ancienne'));

    expect(flipped?.namePosition.isTrailing).toBe(true);
    expect(flipped?.gapPosition.isTrailing).toBe(true);
    expect(kept?.namePosition.isTrailing).toBe(false);
    expect(kept?.gapPosition.isTrailing).toBe(false);
  });
});
