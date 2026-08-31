import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import {
  AnalysisHistoryResponse,
  PolicyPreviewResponse,
  PolicyUpdateRequest,
  ProjectResponse,
  SnapshotPageResponse,
} from './api.models';
import { GitHealthApiClient } from './git-health-api-client';

const projectId = '11111111-1111-1111-1111-111111111111';
const analysisId = '22222222-2222-2222-2222-222222222222';
const policy: PolicyUpdateRequest = {
  activeUntilDays: 20,
  inactiveAfterDays: 60,
  excludedPatterns: ['refs/heads/archive/*'],
  protectedPatterns: ['refs/heads/release/*'],
};

describe('GitHealthApiClient policy and history', () => {
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

  it('updates and previews a project policy', async () => {
    const update = firstValueFrom(client.updatePolicy(projectId, policy));
    const updateRequest = http.expectOne(`/api/projects/${projectId}/policy`);
    expect(updateRequest.request.method).toBe('PUT');
    expect(updateRequest.request.body).toEqual(policy);
    updateRequest.flush({ id: projectId } as ProjectResponse);
    await update;

    const response: PolicyPreviewResponse = {
      matches: [
        {
          referenceName: 'refs/heads/release/1.0',
          isExcluded: false,
          isProtected: true,
          reason: 'The branch matches the protected pattern.',
        },
      ],
    };
    const preview = firstValueFrom(client.previewPolicy(projectId, policy));
    const previewRequest = http.expectOne(`/api/projects/${projectId}/policy/preview`);
    expect(previewRequest.request.method).toBe('POST');
    previewRequest.flush(response);
    expect(await preview).toEqual(response);
  });

  it('loads history and a historical snapshot page', async () => {
    const history: AnalysisHistoryResponse = { items: [], page: 1, pageSize: 100, totalCount: 0 };
    const historyResult = firstValueFrom(client.getAnalysisHistory(projectId, 100));
    const historyRequest = http.expectOne(
      (request) => request.url === `/api/projects/${projectId}/analyses`,
    );
    expect(historyRequest.request.params.get('pageSize')).toBe('100');
    historyRequest.flush(history);
    expect(await historyResult).toEqual(history);

    const page = { items: [] } as unknown as SnapshotPageResponse;
    const pageResult = firstValueFrom(
      client.getAnalysisSnapshots(analysisId, { recommendation: 'Review', pageSize: 50 }),
    );
    const pageRequest = http.expectOne(
      (request) => request.url === `/api/analyses/${analysisId}/branches`,
    );
    expect(pageRequest.request.params.get('recommendation')).toBe('Review');
    expect(pageRequest.request.params.get('pageSize')).toBe('50');
    pageRequest.flush(page);
    expect(await pageResult).toEqual(page);
  });

  it('builds the CSV URL from the active filters', () => {
    const url = client.branchCsvUrl(projectId, {
      activity: 'Inactive',
      recommendation: 'CleanupCandidate',
      search: 'café',
    });

    expect(url).toContain(`/api/projects/${projectId}/analyses/latest/branches.csv?`);
    expect(url).toContain('activity=Inactive');
    expect(url).toContain('recommendation=CleanupCandidate');
    expect(url).toContain('search=caf%C3%A9');
  });
});
