import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { AnalysisHistoryItem, ProjectResponse } from '../../core/api/api.models';
import { LoadedSnapshot } from '../../core/branches/snapshot-loader';
import { CaptureStore } from './capture-store';
import { ProjectContext } from './project-context';

const latestId = 'a2';
const olderId = 'a1';

describe('CaptureStore', () => {
  let store: CaptureStore;
  let context: ProjectContext;
  let http: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    router = TestBed.inject(Router);
    await router.navigateByUrl('/');
    context = TestBed.inject(ProjectContext);
    store = TestBed.inject(CaptureStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => TestBed.resetTestingModule());

  /** The store does not navigate during rendering: wait for the router to finish its turn. */
  async function settled(): Promise<void> {
    await TestBed.inject(ApplicationRef).whenStable();
    TestBed.tick();
  }

  /** The context carries the most recent capture; the store reads back only the earlier ones. */
  async function withHistory(): Promise<void> {
    context.project.set(aProject());
    TestBed.tick();
    http
      .expectOne((request) => request.url.endsWith('/api/projects/p1/analyses'))
      .flush({
        items: [anAnalysis(olderId), anAnalysis(latestId)],
        page: 1,
        pageSize: 100,
        totalCount: 2,
      });
    context.latestSnapshot.set(aSnapshot(latestId));
    context.isLoadingLatest.set(false);
    await settled();
  }

  function flushArchived(): void {
    http
      .expectOne((request) => request.url.endsWith(`/api/analyses/${olderId}/branches`))
      .flush({
        analysisId: olderId,
        capturedAtUtc: '2026-08-01T10:00:00Z',
        referenceName: 'refs/heads/main',
        policy: aSnapshot(olderId).policy,
        items: [],
        nextCursor: null,
      });
  }

  it('shows the most recent capture as long as the URL asks for none', async () => {
    await withHistory();

    expect(store.hasCaptures()).toBe(true);
    expect(store.isLatestSelected()).toBe(true);
    expect(store.selectedId()).toBe(latestId);
    expect(store.snapshot()?.analysisId).toBe(latestId);
    http.verify();
  });

  it('writes the chosen capture into the URL and replays it with its own policy', async () => {
    await withHistory();

    store.select(olderId);
    await settled();

    expect(router.url).toContain(`capture=${olderId}`);
    expect(store.isLatestSelected()).toBe(false);
    expect(store.selected()?.analysisId).toBe(olderId);

    flushArchived();
    await settled();

    expect(store.snapshot()?.analysisId).toBe(olderId);
    http.verify();
  });

  it('releases the selection as soon as the most recent capture is chosen again', async () => {
    await withHistory();
    store.select(olderId);
    await settled();
    flushArchived();

    store.select(latestId);
    await settled();

    expect(router.url).not.toContain('capture=');
    expect(store.isLatestSelected()).toBe(true);
    expect(store.snapshot()?.analysisId).toBe(latestId);
    http.verify();
  });

  it('does not read the most recent capture back when a link names it explicitly', async () => {
    await withHistory();

    await router.navigate([], { queryParams: { capture: latestId } });
    await settled();

    // No archive read: it is already in memory, with today's verdicts.
    expect(store.isLatestSelected()).toBe(true);
    expect(store.snapshot()?.analysisId).toBe(latestId);
    http.verify();
  });

  it('keeps the capture being read in the tab links', async () => {
    await withHistory();
    expect(store.captureLink()).toEqual({});

    store.select(olderId);
    await settled();
    flushArchived();

    expect(store.captureLink()).toEqual({ capture: olderId });
    http.verify();
  });
});

function aProject(): ProjectResponse {
  return {
    id: 'p1',
    displayName: 'Dépôt',
    repositoryPath: 'F:/dépôt',
    isRepositoryAccessible: true,
    createdAtUtc: '2026-08-30T10:00:00.000Z',
    updatedAtUtc: '2026-08-30T10:00:00.000Z',
    referenceName: 'refs/heads/main',
    branchNamespace: 'refs/heads/*',
    activeUntilDays: 30,
    inactiveAfterDays: 90,
    excludedPatterns: [],
    protectedPatterns: [],
    isFavorite: false,
    groupName: null,
    lastSuccessfulAnalysisId: latestId,
  };
}

function anAnalysis(analysisId: string): AnalysisHistoryItem {
  return {
    analysisId,
    status: 'Completed',
    startedAtUtc: '2026-08-01T10:00:00Z',
    completedAtUtc: '2026-08-01T10:00:30Z',
    capturedAtUtc: analysisId === olderId ? '2026-08-01T10:00:00Z' : '2026-08-29T10:00:00Z',
    referenceName: 'refs/heads/main',
    referenceCommit: `${analysisId}000000`,
    branchNamespace: 'refs/heads/*',
    activeUntilDays: 30,
    inactiveAfterDays: 90,
    excludedPatterns: [],
    protectedPatterns: [],
    gitVersion: '2.45.0',
    branchCount: 3,
    failureCode: null,
    failureMessage: null,
  };
}

function aSnapshot(analysisId: string): LoadedSnapshot {
  return {
    analysisId,
    capturedAtUtc: '2026-08-29T10:00:00Z',
    referenceName: 'refs/heads/main',
    policy: {
      activeUntilDays: 30,
      inactiveAfterDays: 90,
      excludedPatterns: [],
      protectedPatterns: [],
    },
    branches: [],
    isTruncated: false,
  };
}
