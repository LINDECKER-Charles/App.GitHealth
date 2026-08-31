import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AnalysisRunStatus, ProjectResponse } from '../api/api.models';
import { FolderScanStore, scanPollIntervalMs } from './folder-scan-store';
import { FolderScanTarget } from './folder-scan.models';

const reference = 'refs/heads/main';

describe('FolderScanStore', () => {
  let store: FolderScanStore;
  let http: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(FolderScanStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    store.reset();
    vi.useRealTimers();
  });

  it('saves the unknown repositories then analyses the whole selection', () => {
    store.start([target('/repos/a', null), target('/repos/b', 'p-b')]);

    const creation = http.expectOne('/api/projects');
    expect(creation.request.method).toBe('POST');
    expect(creation.request.body).toMatchObject({ repositoryPath: '/repos/a' });
    creation.flush(project('p-a', '/repos/a'));

    launch(http, 'p-a', 'an-a');
    launch(http, 'p-b', 'an-b');
    expect(states(store)).toEqual(['queued', 'queued']);

    vi.advanceTimersByTime(scanPollIntervalMs);
    complete(http, 'an-a');
    complete(http, 'an-b');
    flushProjectReload(http);

    expect(states(store)).toEqual(['done', 'done']);
    expect(store.summary()).toEqual({ total: 2, done: 2, failed: 0, active: 0 });
    expect(store.isRunning()).toBe(false);
  });

  it('analyses a repository as soon as it is saved, without waiting for the next ones', () => {
    store.start([target('/repos/a', null), target('/repos/b', null)]);

    http.expectOne('/api/projects').flush(project('p-a', '/repos/a'));
    const launchA = http.expectOne('/api/projects/p-a/analyses');
    const creationB = http.expectOne('/api/projects');
    expect(creationB.request.body).toMatchObject({ repositoryPath: '/repos/b' });

    launchA.flush({ analysisId: 'an-a', statusUrl: '', isDuplicate: false });
    creationB.flush(project('p-b', '/repos/b'));
    launch(http, 'p-b', 'an-b');
    expect(states(store)).toEqual(['queued', 'queued']);

    vi.advanceTimersByTime(scanPollIntervalMs);
    complete(http, 'an-a');
    complete(http, 'an-b');
    flushProjectReload(http);
    expect(states(store)).toEqual(['done', 'done']);
  });

  it('puts back a repository refused by a full queue, then relaunches it', () => {
    store.start([target('/repos/a', 'p-a'), target('/repos/b', 'p-b')]);

    launch(http, 'p-a', 'an-a');
    rejectAsQueueFull(http, 'p-b');
    expect(states(store)).toEqual(['queued', 'pending']);

    vi.advanceTimersByTime(scanPollIntervalMs);
    complete(http, 'an-a');
    launch(http, 'p-b', 'an-b');

    expect(states(store)).toEqual(['done', 'queued']);
    vi.advanceTimersByTime(scanPollIntervalMs);
    complete(http, 'an-b');
    flushProjectReload(http);
    expect(states(store)).toEqual(['done', 'done']);
  });

  it('reports a failed registration without blocking the other repositories', () => {
    store.start([target('/repos/a', null), target('/repos/b', 'p-b')]);

    http
      .expectOne('/api/projects')
      .flush(
        { detail: 'Another project already uses this repository.', code: 'project.already_exists' },
        { status: 409, statusText: 'Conflict' },
      );
    launch(http, 'p-b', 'an-b');
    vi.advanceTimersByTime(scanPollIntervalMs);
    complete(http, 'an-b');
    flushProjectReload(http);

    expect(states(store)).toEqual(['failed', 'done']);
    expect(store.jobs()[0].message).toBe('Another project already uses this repository.');
    expect(store.summary()).toMatchObject({ done: 1, failed: 1 });
  });
});

function target(canonicalPath: string, projectId: string | null): FolderScanTarget {
  return {
    canonicalPath,
    name: canonicalPath.split('/').at(-1) ?? canonicalPath,
    referenceName: reference,
    projectId,
  };
}

function project(id: string, repositoryPath: string): ProjectResponse {
  return {
    id,
    displayName: repositoryPath,
    repositoryPath,
    isRepositoryAccessible: true,
    createdAtUtc: '2026-08-29T08:00:00Z',
    updatedAtUtc: '2026-08-29T08:00:00Z',
    referenceName: reference,
    branchNamespace: 'refs/heads/*',
    activeUntilDays: 30,
    inactiveAfterDays: 90,
    excludedPatterns: [],
    protectedPatterns: [],
    isFavorite: false,
    groupName: null,
    lastSuccessfulAnalysisId: null,
  };
}

function launch(http: HttpTestingController, projectId: string, analysisId: string): void {
  http
    .expectOne(`/api/projects/${projectId}/analyses`)
    .flush({ analysisId, statusUrl: `/api/analyses/${analysisId}`, isDuplicate: false });
}

function rejectAsQueueFull(http: HttpTestingController, projectId: string): void {
  http
    .expectOne(`/api/projects/${projectId}/analyses`)
    .flush(
      { detail: 'The analysis queue is full.', code: 'analysis.queue_full' },
      { status: 503, statusText: 'Service Unavailable' },
    );
}

function complete(http: HttpTestingController, analysisId: string): void {
  http.expectOne(`/api/analyses/${analysisId}`).flush({
    analysisId,
    projectId: 'ignored',
    status: 'Completed' satisfies AnalysisRunStatus,
    phase: 'Finished',
    startedAtUtc: '2026-08-29T08:00:00Z',
    completedAtUtc: '2026-08-29T08:00:05Z',
    failureCode: null,
    failureMessage: null,
  });
}

/** A finished scan re-reads the repository list: these two calls close the scenario. */
function flushProjectReload(http: HttpTestingController): void {
  http.expectOne('/api/projects').flush([]);
  http.expectOne('/api/runtime').flush({
    mode: 'native',
    initialRepositoryPath: null,
    repositoriesRoot: null,
    canBrowseDirectories: true,
  });
}

function states(store: FolderScanStore): readonly string[] {
  return store.jobs().map((job) => job.state);
}
