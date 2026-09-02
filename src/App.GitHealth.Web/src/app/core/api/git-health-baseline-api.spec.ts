import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import {
  AnalysisHistoryResponse,
  AnalysisLaunchResponse,
  BaselineListResponse,
  ProjectResponse,
  SnapshotPageResponse,
} from './api.models';
import { GitHealthApiClient } from './git-health-api-client';

const projectId = '11111111-1111-1111-1111-111111111111';
const analysisId = '22222222-2222-2222-2222-222222222222';
const analysisUrl = `/api/analyses/${analysisId}`;
const analysesUrl = `/api/projects/${projectId}/analyses`;
const baselinesUrl = `/api/projects/${projectId}/baselines`;
const referenceNames: readonly string[] = ['refs/heads/main', 'refs/heads/dev'];
const secondary = referenceNames[1];

const baselines: BaselineListResponse = {
  items: [
    {
      referenceName: referenceNames[0],
      position: 0,
      isPrimary: true,
      lastSuccessfulAnalysisId: analysisId,
      lastCapturedAtUtc: '2026-08-29T08:00:00Z',
      branchCount: 12,
    },
  ],
  availableReferences: referenceNames,
};

const launch: AnalysisLaunchResponse = {
  analyses: [{ analysisId, referenceName: secondary, statusUrl: analysisUrl, isDuplicate: false }],
  analysisId,
  statusUrl: analysisUrl,
  isDuplicate: false,
};

describe('GitHealthApiClient baselines and deletions', () => {
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

  it('lists the baselines a project declares', async () => {
    const result = firstValueFrom(client.listBaselines(projectId));

    const request = http.expectOne(baselinesUrl);
    expect(request.request.method).toBe('GET');
    request.flush(baselines);

    expect(await result).toEqual(baselines);
  });

  it('replaces the whole baseline list in a single call', async () => {
    const result = firstValueFrom(client.updateBaselines(projectId, referenceNames));

    const request = http.expectOne(baselinesUrl);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ referenceNames });
    request.flush({ id: projectId, referenceNames } as ProjectResponse);

    expect(await result).toMatchObject({ referenceNames });
  });

  it('deletes a project', async () => {
    const result = firstValueFrom(client.deleteProject(projectId));

    const request = http.expectOne(`/api/projects/${projectId}`);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });

    await result;
  });

  it('deletes a single capture', async () => {
    const result = firstValueFrom(client.deleteAnalysis(analysisId));

    const request = http.expectOne(analysisUrl);
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });

    await result;
  });

  it('launches one baseline, or every baseline when none is named', async () => {
    const scoped = firstValueFrom(client.launchAnalysis(projectId, secondary));
    const scopedRequest = http.expectOne((request) => request.url === analysesUrl);
    expect(scopedRequest.request.method).toBe('POST');
    expect(scopedRequest.request.params.get('baseline')).toBe(secondary);
    scopedRequest.flush(launch);
    expect(await scoped).toEqual(launch);

    const every = firstValueFrom(client.launchAnalysis(projectId));
    const everyRequest = http.expectOne((request) => request.url === analysesUrl);
    expect(everyRequest.request.method).toBe('POST');
    expect(everyRequest.request.params.has('baseline')).toBe(false);
    everyRequest.flush(launch);
    expect(await every).toEqual(launch);
  });

  it('narrows the latest snapshots to one baseline', async () => {
    const result = firstValueFrom(client.getLatestSnapshots(projectId, { baseline: secondary }));

    const request = http.expectOne(
      (candidate) => candidate.url === `${analysesUrl}/latest/branches`,
    );
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('baseline')).toBe(secondary);
    request.flush({ items: [] } as unknown as SnapshotPageResponse);

    await result;
  });

  it('narrows the capture history to one baseline', async () => {
    const history: AnalysisHistoryResponse = { items: [], page: 1, pageSize: 20, totalCount: 0 };
    const result = firstValueFrom(client.getAnalysisHistory(projectId, 20, secondary));

    const request = http.expectOne((candidate) => candidate.url === analysesUrl);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('baseline')).toBe(secondary);
    request.flush(history);

    expect(await result).toEqual(history);
  });
});
