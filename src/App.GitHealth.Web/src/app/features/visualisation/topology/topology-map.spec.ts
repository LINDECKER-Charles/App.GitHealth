import { BranchSnapshotResponse } from '../../../core/api/api.models';
import { TopologyFilter } from './topology-layout';
import { TopologyMap, buildTopologyMap } from './topology-map';

const referencePrefix = 'refs/remotes/origin/';

/** A node's identity is its reference name: the tests target the same key as the view. */
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
    reason: 'Active divergence.',
    isProtected: false,
    isExcluded: false,
    topContributor: null,
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
  it('caps the scale at 12 px per commit on a nearly up-to-date repository', () => {
    const map = build([
      branch('ahead', { topology: 'Ahead', aheadCount: 3 }),
      branch('late', { behindCount: 2, aheadCount: 1 }),
    ]);

    expect(map.nodes[0].id).toBe(ref('late'));
    expect(map.nodes[0].junction.x).toBe(830 - 2 * 12);
  });

  it('floors the scale at 4.5 px per commit and pulls the fork back to the 120 clamp', () => {
    const map = build([branch('stale', { behindCount: 200 }), branch('near', { behindCount: 10 })]);

    expect(map.nodes[0].junction.x).toBe(120);
    expect(map.nodes[1].junction.x).toBe(830 - 10 * 4.5);
  });

  it('spaces the forks by at least 18 px without ever pushing them right', () => {
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

  it('bounds the tip of a far-ahead branch at 940', () => {
    const map = build([branch('long', { topology: 'Ahead', aheadCount: 100 })]);

    expect(map.nodes[0].tip.x).toBe(940);
  });

  it('lays the merged bridges under the trunk, 92 px wide, without passing HEAD', () => {
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

  it('draws every in-sync branch as concentric loops', () => {
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

  it('draws every branch with no common ancestor and keeps them inside the frame', () => {
    const map = build([
      branch('u1', { topology: 'Unrelated', aheadCount: 5 }),
      branch('u2', { topology: 'Unrelated', aheadCount: 400 }),
    ]);

    expect(map.nodes).toHaveLength(2);
    expect(map.nodes.map((node) => node.tip.x)).toEqual([120, 940]);
    expect(map.nodes[1].tip.y - map.nodes[0].tip.y).toBe(36);
    expect(map.nodes[0].dash).toBe('5 5');
  });

  it('the "open" filter removes only the merged branches', () => {
    const ids = build(mixed, 'open').nodes.map((node) => node.id);

    expect(ids).toEqual([ref('diverged'), ref('ahead'), ref('sync'), ref('unrelated')]);
  });

  it('the "merged" filter keeps only the merged branches', () => {
    const ids = build(mixed, 'merged').nodes.map((node) => node.id);

    expect(ids).toEqual([ref('merged')]);
  });

  it('grows the height of the frame with the number of open branches', () => {
    const few = build([branch('a'), branch('b'), branch('c')]);
    const many = build(Array.from({ length: 12 }, (_, index) => branch(`b${index}`)));

    expect(few.trunkY).toBe(56 + 2 * 36 + 44);
    expect(many.trunkY).toBe(56 + 11 * 36 + 44);
    expect(many.height).toBeGreaterThan(few.height);
    expect(few.viewBox).toBe(`0 0 990 ${few.height}`);
  });

  it('counts the whole snapshot, including what the active filter hides', () => {
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

  it('draws a branch covered by an excluded pattern with a 4 4 dash', () => {
    const map = build([
      branch('excluded', { isExcluded: true }),
      branch('isolated', { topology: 'Unrelated', aheadCount: 3 }),
      branch('plain', {}),
    ]);
    const dashes = new Map(map.nodes.map((node) => [node.id, node.dash]));

    expect(dashes.get(ref('excluded'))).toBe('4 4');
    expect(dashes.get(ref('isolated'))).toBe('5 5');
    expect(dashes.get(ref('plain'))).toBeNull();
  });

  it('thickens the focused branch and dims the others', () => {
    const map = build([branch('viewed'), branch('other')], 'all', ref('viewed'));
    const focused = map.nodes.find((node) => node.id === ref('viewed'));
    const dimmed = map.nodes.find((node) => node.id === ref('other'));

    expect(focused?.strokeWidth).toBe(3);
    expect(focused?.opacity).toBe(1);
    expect(dimmed?.strokeWidth).toBe(2);
    expect(dimmed?.opacity).toBe(0.22);
    expect(dimmed?.labelOpacity).toBe(0.25);
  });

  it('dims nobody when the focus names no branch of the plan', () => {
    const map = build([branch('viewed'), branch('other')], 'all', ref('previous-analysis'));

    expect(map.nodes).toHaveLength(2);
    for (const node of map.nodes) {
      expect(node.isFocused).toBe(false);
      expect(node.opacity).toBe(1);
      expect(node.labelOpacity).toBe(1);
      expect(node.strokeWidth).toBe(2);
    }
  });

  it('positions the labels as a percentage of the frame', () => {
    const map = build([branch('feat/map', { topology: 'Ahead', aheadCount: 2 })]);
    const node = map.nodes[0];

    expect(node.name).toBe('feat/map');
    expect(node.gap).toBe('+2 / 0');
    expect(node.namePosition.left).toMatch(/^\d+\.\d{2}%$/);
    expect(node.namePosition.top).toMatch(/^\d+\.\d{2}%$/);
  });

  it('flips left the labels anchored beyond 82% of the frame', () => {
    const map = build([
      branch('at-edge', { topology: 'Ahead', aheadCount: 1 }),
      branch('older', { behindCount: 60, aheadCount: 1 }),
    ]);
    const flipped = map.nodes.find((node) => node.id === ref('at-edge'));
    const kept = map.nodes.find((node) => node.id === ref('older'));

    expect(flipped?.namePosition.isTrailing).toBe(true);
    expect(flipped?.gapPosition.isTrailing).toBe(true);
    expect(kept?.namePosition.isTrailing).toBe(false);
    expect(kept?.gapPosition.isTrailing).toBe(false);
  });
});
