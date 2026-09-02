import { TestBed } from '@angular/core/testing';
import {
  AnalysisCommandTrace,
  AnalysisReferenceProgress,
  AnalysisStatusResponse,
  ReferenceProgressState,
} from '../api/api.models';
import { AnalysisRunStore, appendCommands } from './analysis-run-store';

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

function command(sequence: number): AnalysisCommandTrace {
  return {
    sequence,
    commandLine: `git merge-base main branch-${sequence}`,
    durationMs: 4,
    exitCode: 0,
    output: 'aaaaaaaa',
  };
}

function status(overrides: Partial<AnalysisStatusResponse> = {}): AnalysisStatusResponse {
  return {
    analysisId: 'a1',
    projectId: 'p1',
    status: 'Running',
    phase: 'Topology',
    startedAtUtc: '2026-09-02T10:00:00Z',
    completedAtUtc: null,
    failureCode: null,
    failureMessage: null,
    progress: null,
    ...overrides,
  };
}

describe('AnalysisRunStore', () => {
  let store: AnalysisRunStore;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    store = TestBed.inject(AnalysisRunStore);
    store.start(1000);
  });

  it('counts the references the current stage is done with', () => {
    store.apply(
      status({
        progress: {
          references: [
            reference('a', 'Read'),
            reference('b', 'Measured'),
            reference('c', 'Measuring'),
            reference('d', 'Listed'),
          ],
          commands: [],
          commandCount: 0,
        },
      }),
    );

    expect(store.total()).toBe(4);
    expect(store.processed()).toBe(2);
    expect(store.reading().map((item) => item.referenceName)).toEqual(['refs/heads/c']);
  });

  it('keeps the commands already read and appends only the newer ranks', () => {
    store.apply(
      status({
        progress: { references: [], commands: [command(1), command(2)], commandCount: 2 },
      }),
    );
    store.apply(
      status({
        progress: { references: [], commands: [command(2), command(3)], commandCount: 3 },
      }),
    );

    expect(store.commands().map((item) => item.sequence)).toEqual([1, 2, 3]);
    expect(store.commandCount()).toBe(3);
  });

  it('leaves what was read in place when a status carries no progress', () => {
    store.apply(
      status({
        progress: { references: [reference('a', 'Read')], commands: [command(1)], commandCount: 1 },
      }),
    );
    store.apply(status({ status: 'Completed', phase: 'Finished' }));

    expect(store.phase()).toBe('Finished');
    expect(store.total()).toBe(1);
    expect(store.commands()).toHaveLength(1);
  });

  it('holds the closing frame once the run has landed, then lets it go', () => {
    vi.useFakeTimers();
    store.close();
    expect(store.isClosing()).toBe(true);

    vi.advanceTimersByTime(1000);
    expect(store.isClosing()).toBe(false);
    vi.useRealTimers();
  });

  it('shows no closing frame for a run that failed', () => {
    vi.useFakeTimers();
    store.close();
    store.abandon();

    expect(store.isClosing()).toBe(false);
    vi.advanceTimersByTime(1000);
    expect(store.isClosing()).toBe(false);
    vi.useRealTimers();
  });

  it('starts a run folded open, and follows the reader after that', () => {
    expect(store.isCollapsed()).toBe(false);

    store.collapse();
    expect(store.isCollapsed()).toBe(true);

    store.expand();
    expect(store.isCollapsed()).toBe(false);
  });
});

describe('appendCommands', () => {
  it('drops the oldest commands once the kept window is full', () => {
    const known = Array.from({ length: 240 }, (_, index) => command(index + 1));

    const merged = appendCommands(known, [command(241)]);

    expect(merged).toHaveLength(240);
    expect(merged[0].sequence).toBe(2);
    expect(merged[merged.length - 1].sequence).toBe(241);
  });
});
