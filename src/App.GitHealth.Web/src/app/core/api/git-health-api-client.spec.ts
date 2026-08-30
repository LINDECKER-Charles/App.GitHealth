import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { ApiError } from './api-error';
import {
  AnalysisLaunchResponse,
  AnalysisStatusResponse,
  BranchSnapshotResponse,
  CreateProjectRequest,
  DirectoryListing,
  ProjectResponse,
  ProjectSettingsRequest,
  RepositoryValidationResponse,
  RuntimeInfo,
  SnapshotDetailResponse,
  SnapshotPageResponse,
} from './api.models';
import { GitHealthApiClient } from './git-health-api-client';

const projectId = '11111111-1111-1111-1111-111111111111';
const analysisId = '22222222-2222-2222-2222-222222222222';
const snapshotId = '33333333-3333-3333-3333-333333333333';

const settings: ProjectSettingsRequest = {
  referenceName: 'refs/heads/main',
  branchNamespace: 'refs/heads/*',
  activeUntilDays: 30,
  inactiveAfterDays: 90,
  excludedPatterns: [],
  protectedPatterns: ['refs/heads/main'],
};

const project: ProjectResponse = {
  id: projectId,
  displayName: 'GitHealth',
  repositoryPath: 'D:/Dev/Repo/App.GitHealth',
  isRepositoryAccessible: true,
  createdAtUtc: '2026-08-29T07:00:00Z',
  updatedAtUtc: '2026-08-29T08:00:00Z',
  referenceName: settings.referenceName,
  branchNamespace: settings.branchNamespace,
  activeUntilDays: settings.activeUntilDays,
  inactiveAfterDays: settings.inactiveAfterDays,
  excludedPatterns: settings.excludedPatterns,
  protectedPatterns: settings.protectedPatterns,
  isFavorite: false,
  groupName: null,
  lastSuccessfulAnalysisId: analysisId,
};

const snapshot: BranchSnapshotResponse = {
  id: snapshotId,
  referenceName: 'refs/heads/feature/dashboard',
  commitId: 'abcdef123456',
  aheadCount: 2,
  behindCount: 1,
  relationship: 'CommonAncestor',
  lastActivityAtUtc: '2026-08-29T08:00:00Z',
  tipAuthor: 'Ada Lovelace',
  topology: 'Diverged',
  activity: 'Active',
  recommendation: 'Keep',
  reason: 'La branche contient des commits propres.',
  isProtected: false,
  isExcluded: false,
};

describe('GitHealthApiClient', () => {
  let client: GitHealthApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    client = TestBed.inject(GitHealthApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets the runtime contract', async () => {
    const expected: RuntimeInfo = {
      mode: 'docker',
      initialRepositoryPath: null,
      repositoriesRoot: '/repositories',
      canBrowseDirectories: false,
      isGitAvailable: true,
      gitExecutablePath: '/usr/bin/git',
      gitDiagnostic: 'git version 2.51.0',
    };
    const result = firstValueFrom(client.getRuntime());

    const request = http.expectOne('/api/runtime');
    expect(request.request.method).toBe('GET');
    request.flush(expected);

    expect(await result).toEqual(expected);
  });

  it('browses root directories and then a selected directory', async () => {
    const expected: DirectoryListing = {
      currentPath: '',
      parentPath: null,
      directories: [{ name: 'Dev', path: 'D:/Dev' }],
      isTruncated: false,
    };
    const rootResult = firstValueFrom(client.browseDirectories(null));
    const rootRequest = http.expectOne('/api/runtime/directories');
    expect(rootRequest.request.params.has('path')).toBe(false);
    rootRequest.flush(expected);
    expect(await rootResult).toEqual(expected);

    const pathResult = firstValueFrom(client.browseDirectories('D:/Dev & sources'));
    const pathRequest = http.expectOne((request) => request.url === '/api/runtime/directories');
    expect(pathRequest.request.params.get('path')).toBe('D:/Dev & sources');
    pathRequest.flush({ ...expected, currentPath: 'D:/Dev & sources' });
    expect((await pathResult).currentPath).toBe('D:/Dev & sources');
  });

  it('gets the project collection and an individual project', async () => {
    const listResult = firstValueFrom(client.listProjects());
    const listRequest = http.expectOne('/api/projects');
    listRequest.flush([project]);
    expect(await listResult).toEqual([project]);

    const itemResult = firstValueFrom(client.getProject(projectId));
    const itemRequest = http.expectOne(`/api/projects/${projectId}`);
    itemRequest.flush(project);
    expect(await itemResult).toEqual(project);
  });

  it('validates a repository path', async () => {
    const path = 'D:/Dev/Repo/App.GitHealth';
    const expected: RepositoryValidationResponse = {
      canonicalPath: path,
      isBare: false,
      suggestedReference: 'refs/heads/main',
      references: ['refs/heads/main'],
    };
    const result = firstValueFrom(client.validateRepository(path));

    const request = http.expectOne('/api/projects/validate');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ path });
    request.flush(expected);

    expect(await result).toEqual(expected);
  });

  it('creates and configures a project', async () => {
    const creation: CreateProjectRequest = {
      displayName: project.displayName,
      repositoryPath: project.repositoryPath,
      settings,
    };
    const createResult = firstValueFrom(client.createProject(creation));
    const createRequest = http.expectOne('/api/projects');
    expect(createRequest.request.method).toBe('POST');
    expect(createRequest.request.body).toEqual(creation);
    createRequest.flush(project);
    expect(await createResult).toEqual(project);

    const updateResult = firstValueFrom(client.updateProjectSettings(projectId, settings));
    const updateRequest = http.expectOne(`/api/projects/${projectId}/settings`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual(settings);
    updateRequest.flush(project);
    expect(await updateResult).toEqual(project);
  });

  it('relocates a project', async () => {
    const relocationResult = firstValueFrom(
      client.relocateProject(projectId, { repositoryPath: 'D:/Dev/Repo/renamed' }),
    );
    const relocationRequest = http.expectOne(`/api/projects/${projectId}/repository`);
    expect(relocationRequest.request.method).toBe('PUT');
    expect(relocationRequest.request.body).toEqual({ repositoryPath: 'D:/Dev/Repo/renamed' });
    relocationRequest.flush({ ...project, repositoryPath: 'D:/Dev/Repo/renamed' });
    expect(await relocationResult).toMatchObject({
      id: projectId,
      repositoryPath: 'D:/Dev/Repo/renamed',
    });
  });

  it('launches an analysis and gets its status', async () => {
    const launch: AnalysisLaunchResponse = {
      analysisId,
      statusUrl: `/api/analyses/${analysisId}`,
      isDuplicate: false,
    };
    const launchResult = firstValueFrom(client.launchAnalysis(projectId));
    const launchRequest = http.expectOne(`/api/projects/${projectId}/analyses`);
    expect(launchRequest.request.method).toBe('POST');
    expect(launchRequest.request.body).toBeNull();
    launchRequest.flush(launch);
    expect(await launchResult).toEqual(launch);

    const status: AnalysisStatusResponse = {
      analysisId,
      projectId,
      status: 'Completed',
      phase: 'Finished',
      startedAtUtc: '2026-08-29T08:00:00Z',
      completedAtUtc: '2026-08-29T08:00:01Z',
      failureCode: null,
      failureMessage: null,
    };
    const statusResult = firstValueFrom(client.getAnalysis(analysisId));
    const statusRequest = http.expectOne(`/api/analyses/${analysisId}`);
    statusRequest.flush(status);
    expect(await statusResult).toEqual(status);
  });

  it('gets filtered snapshots and a snapshot detail', async () => {
    const page: SnapshotPageResponse = {
      analysisId,
      capturedAtUtc: '2026-08-29T08:00:01Z',
      referenceName: 'refs/heads/main',
      items: [snapshot],
      nextCursor: 'next-page',
      policy: {
        activeUntilDays: 30,
        excludedPatterns: [],
        inactiveAfterDays: 90,
        protectedPatterns: [],
      },
    };
    const pageResult = firstValueFrom(
      client.getLatestSnapshots(projectId, {
        search: 'dashboard',
        relationship: 'CommonAncestor',
        sort: 'activity',
        direction: 'desc',
        cursor: 'current-page',
        pageSize: 25,
      }),
    );
    const pageRequest = http.expectOne(
      (request) => request.url === `/api/projects/${projectId}/analyses/latest/branches`,
    );
    expect(pageRequest.request.params.get('search')).toBe('dashboard');
    expect(pageRequest.request.params.get('relationship')).toBe('CommonAncestor');
    expect(pageRequest.request.params.get('sort')).toBe('activity');
    expect(pageRequest.request.params.get('direction')).toBe('desc');
    expect(pageRequest.request.params.get('cursor')).toBe('current-page');
    expect(pageRequest.request.params.get('pageSize')).toBe('25');
    pageRequest.flush(page);
    expect(await pageResult).toEqual(page);

    const detail: SnapshotDetailResponse = {
      analysisId,
      referenceName: 'refs/heads/main',
      referenceCommit: '123456abcdef',
      capturedAtUtc: page.capturedAtUtc,
      snapshot,
      contributors: [{ name: 'Ada Lovelace', email: 'ada@example.test', commitCount: 2 }],
      attributionStatus: 'Available',
      mailmapApplied: true,
      policy: {
        activeUntilDays: 30,
        inactiveAfterDays: 90,
        excludedPatterns: [],
        protectedPatterns: ['refs/heads/main'],
      },
    };
    const detailResult = firstValueFrom(client.getSnapshot(snapshotId));
    const detailRequest = http.expectOne(`/api/branch-snapshots/${snapshotId}`);
    detailRequest.flush(detail);
    expect(await detailResult).toEqual(detail);
  });

  it('turns API failures into ApiError instances', async () => {
    const result = firstValueFrom(client.getProject('missing'));
    const request = http.expectOne('/api/projects/missing');
    request.flush(
      {
        title: 'Ressource introuvable',
        status: 404,
        detail: 'Le projet demandé n’existe pas.',
        code: 'project.not_found',
        traceId: 'trace-404',
      },
      { status: 404, statusText: 'Not Found' },
    );

    const error: unknown = await result.catch((reason: unknown) => reason);
    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 404,
      code: 'project.not_found',
      traceId: 'trace-404',
    });
  });
});
