import { AnalysisReferenceProgress, ReferenceProgressState } from '../api/api.models';
import { buildGraph } from './analysis-graph';

function reference(
  name: string,
  state: ReferenceProgressState,
  overrides: Partial<AnalysisReferenceProgress> = {},
): AnalysisReferenceProgress {
  return {
    referenceName: `refs/heads/${name}`,
    commitId: 'aaaaaaaabbbbbbbb',
    state,
    lastActivityAtUtc: null,
    tipAuthor: null,
    mergeBaseCommit: null,
    aheadCount: null,
    behindCount: null,
    topology: null,
    topContributor: null,
    contributorCount: null,
    ...overrides,
  };
}

function measured(name: string, ahead: number): AnalysisReferenceProgress {
  return reference(name, 'Measured', { aheadCount: ahead, behindCount: 2, topology: 'Diverged' });
}

describe('buildGraph', () => {
  it('draws only the references already measured, and counts them all', () => {
    const graph = buildGraph([
      measured('a', 1),
      reference('b', 'Measuring'),
      reference('c', 'Listed'),
    ]);

    expect(graph.nodes.map((node) => node.id)).toEqual(['refs/heads/a']);
    expect(graph.placed).toBe(1);
    expect(graph.total).toBe(3);
  });

  it('pushes a node further from the trunk the further ahead it has run', () => {
    const graph = buildGraph([measured('near', 1), measured('far', 5)]);

    expect(graph.nodes[1].x).toBeGreaterThan(graph.nodes[0].x);
    expect(graph.nodes[0].isHollow).toBe(false);
  });

  it('draws a reference with no own commits as a hollow node', () => {
    const graph = buildGraph([
      reference('main-copy', 'Measured', {
        aheadCount: 0,
        behindCount: 0,
        topology: 'Synchronized',
      }),
    ]);

    expect(graph.nodes[0].isHollow).toBe(true);
    expect(graph.nodes[0].tone).toBe('success');
  });

  it('ends the window on the reference being read, so the cursor stays in sight', () => {
    const many = Array.from({ length: 40 }, (_, index) => measured(`branch-${index}`, 1));
    const graph = buildGraph([...many, reference('current', 'Measuring')]);

    expect(graph.nodes).toHaveLength(13);
    expect(graph.nodes[graph.nodes.length - 1].id).toBe('refs/heads/branch-39');
    expect(graph.cursorY).toBe(14 + 13 * 16);
    expect(graph.placed).toBe(40);
  });

  it('leaves no cursor when nothing is being read', () => {
    const graph = buildGraph([measured('a', 1)]);

    expect(graph.cursorX).toBeNull();
    expect(graph.cursorY).toBeNull();
  });
});
