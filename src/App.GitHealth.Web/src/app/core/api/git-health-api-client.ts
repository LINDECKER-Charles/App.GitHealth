import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { ApiError } from './api-error';
import {
  AnalysisHistoryResponse,
  AnalysisLaunchResponse,
  AnalysisStatusResponse,
  AssistantAgentList,
  AssistantBriefing,
  AssistantRun,
  AssistantRunRequest,
  BaselineListResponse,
  CreateProjectRequest,
  DirectoryListing,
  PolicyPreviewResponse,
  PolicyUpdateRequest,
  ProjectOrganizationRequest,
  ProjectResponse,
  ProjectSettingsRequest,
  RelocateProjectRequest,
  RepositoryDiscoveryRequest,
  RepositoryDiscoveryResponse,
  RepositoryValidationResponse,
  RuntimeInfo,
  SnapshotDetailResponse,
  SnapshotPageResponse,
  SnapshotQuery,
  UpdateStatus,
} from './api.models';

const apiRoot = '/api';
const projectsUrl = `${apiRoot}/projects`;

@Injectable({ providedIn: 'root' })
export class GitHealthApiClient {
  private readonly http = inject(HttpClient);

  getRuntime(): Observable<RuntimeInfo> {
    return this.request(this.http.get<RuntimeInfo>(`${apiRoot}/runtime`));
  }

  getUpdateStatus(): Observable<UpdateStatus> {
    return this.request(this.http.get<UpdateStatus>(`${apiRoot}/updates`));
  }

  /**
   * Triggers the update. An empty response means the host is restarting the
   * application; a status means nothing was applicable.
   */
  applyUpdate(): Observable<UpdateStatus | null> {
    return this.request(this.http.post<UpdateStatus | null>(`${apiRoot}/updates/apply`, null));
  }

  browseDirectories(path: string | null): Observable<DirectoryListing> {
    const params = setParam(new HttpParams(), 'path', path);
    return this.request(
      this.http.get<DirectoryListing>(`${apiRoot}/runtime/directories`, { params }),
    );
  }

  listProjects(): Observable<readonly ProjectResponse[]> {
    return this.request(this.http.get<readonly ProjectResponse[]>(projectsUrl));
  }

  getProject(projectId: string): Observable<ProjectResponse> {
    return this.request(this.http.get<ProjectResponse>(projectUrl(projectId)));
  }

  validateRepository(path: string): Observable<RepositoryValidationResponse> {
    return this.request(
      this.http.post<RepositoryValidationResponse>(`${projectsUrl}/validate`, { path }),
    );
  }

  discoverRepositories(
    request: RepositoryDiscoveryRequest,
  ): Observable<RepositoryDiscoveryResponse> {
    return this.request(
      this.http.post<RepositoryDiscoveryResponse>(`${apiRoot}/repositories/discover`, request),
    );
  }

  createProject(request: CreateProjectRequest): Observable<ProjectResponse> {
    return this.request(this.http.post<ProjectResponse>(projectsUrl, request));
  }

  updateProjectSettings(
    projectId: string,
    settings: ProjectSettingsRequest,
  ): Observable<ProjectResponse> {
    return this.request(
      this.http.put<ProjectResponse>(`${projectUrl(projectId)}/settings`, settings),
    );
  }

  updateProjectOrganization(
    projectId: string,
    request: ProjectOrganizationRequest,
  ): Observable<ProjectResponse> {
    return this.request(
      this.http.put<ProjectResponse>(`${projectUrl(projectId)}/organization`, request),
    );
  }

  relocateProject(projectId: string, request: RelocateProjectRequest): Observable<ProjectResponse> {
    return this.request(
      this.http.put<ProjectResponse>(`${projectUrl(projectId)}/repository`, request),
    );
  }

  /** Without a baseline, every baseline the project declares is measured. */
  launchAnalysis(projectId: string, baseline?: string | null): Observable<AnalysisLaunchResponse> {
    const url = `${projectUrl(projectId)}/analyses`;
    const params = setParam(new HttpParams(), 'baseline', baseline);
    return this.request(this.http.post<AnalysisLaunchResponse>(url, null, { params }));
  }

  listBaselines(projectId: string): Observable<BaselineListResponse> {
    return this.request(this.http.get<BaselineListResponse>(`${projectUrl(projectId)}/baselines`));
  }

  updateBaselines(
    projectId: string,
    referenceNames: readonly string[],
  ): Observable<ProjectResponse> {
    return this.request(
      this.http.put<ProjectResponse>(`${projectUrl(projectId)}/baselines`, { referenceNames }),
    );
  }

  deleteProject(projectId: string): Observable<void> {
    return this.request(this.http.delete<void>(projectUrl(projectId)));
  }

  deleteAnalysis(analysisId: string): Observable<void> {
    const id = encodeURIComponent(analysisId);
    return this.request(this.http.delete<void>(`${apiRoot}/analyses/${id}`));
  }

  getAnalysis(analysisId: string): Observable<AnalysisStatusResponse> {
    const id = encodeURIComponent(analysisId);
    return this.request(this.http.get<AnalysisStatusResponse>(`${apiRoot}/analyses/${id}`));
  }

  getLatestSnapshots(
    projectId: string,
    query: SnapshotQuery = {},
  ): Observable<SnapshotPageResponse> {
    const url = `${projectUrl(projectId)}/analyses/latest/branches`;
    return this.request(
      this.http.get<SnapshotPageResponse>(url, { params: snapshotParams(query) }),
    );
  }

  getSnapshot(snapshotId: string): Observable<SnapshotDetailResponse> {
    const id = encodeURIComponent(snapshotId);
    const url = `${apiRoot}/branch-snapshots/${id}`;
    return this.request(this.http.get<SnapshotDetailResponse>(url));
  }

  updatePolicy(projectId: string, policy: PolicyUpdateRequest): Observable<ProjectResponse> {
    return this.request(this.http.put<ProjectResponse>(`${projectUrl(projectId)}/policy`, policy));
  }

  previewPolicy(projectId: string, policy: PolicyUpdateRequest): Observable<PolicyPreviewResponse> {
    return this.request(
      this.http.post<PolicyPreviewResponse>(`${projectUrl(projectId)}/policy/preview`, policy),
    );
  }

  /** A baseline narrows the history to its own captures, which is what the picker shows. */
  getAnalysisHistory(
    projectId: string,
    pageSize?: number,
    baseline?: string | null,
  ): Observable<AnalysisHistoryResponse> {
    let params = setParam(new HttpParams(), 'pageSize', pageSize);
    params = setParam(params, 'baseline', baseline);
    return this.request(
      this.http.get<AnalysisHistoryResponse>(`${projectUrl(projectId)}/analyses`, { params }),
    );
  }

  getAnalysisSnapshots(
    analysisId: string,
    query: SnapshotQuery = {},
  ): Observable<SnapshotPageResponse> {
    const id = encodeURIComponent(analysisId);
    return this.request(
      this.http.get<SnapshotPageResponse>(`${apiRoot}/analyses/${id}/branches`, {
        params: snapshotParams(query),
      }),
    );
  }

  branchCsvUrl(projectId: string, query: SnapshotQuery = {}): string {
    const params = snapshotParams(query).toString();
    const url = `${projectUrl(projectId)}/analyses/latest/branches.csv`;
    return params.length === 0 ? url : `${url}?${params}`;
  }

  /** `refresh` probes the machine again, for a CLI installed since the app was opened. */
  listAssistantAgents(refresh = false): Observable<AssistantAgentList> {
    const params = setParam(new HttpParams(), 'refresh', refresh || null);
    return this.request(
      this.http.get<AssistantAgentList>(`${apiRoot}/assistant/agents`, { params }),
    );
  }

  /** The exact text a run would send, read before the run is allowed to start. */
  getAssistantBriefing(projectId: string, baseline?: string | null): Observable<AssistantBriefing> {
    const params = setParam(new HttpParams(), 'baseline', baseline);
    const url = `${projectUrl(projectId)}/assistant/briefing`;
    return this.request(this.http.get<AssistantBriefing>(url, { params }));
  }

  startAssistantRun(projectId: string, request: AssistantRunRequest): Observable<AssistantRun> {
    const url = `${projectUrl(projectId)}/assistant/runs`;
    return this.request(this.http.post<AssistantRun>(url, request));
  }

  /** `from` is the trace offset already received, so a poll carries only what is new. */
  getAssistantRun(runId: string, from: number): Observable<AssistantRun> {
    const id = encodeURIComponent(runId);
    const params = setParam(new HttpParams(), 'from', from);
    return this.request(this.http.get<AssistantRun>(`${apiRoot}/assistant/runs/${id}`, { params }));
  }

  cancelAssistantRun(runId: string): Observable<AssistantRun> {
    const id = encodeURIComponent(runId);
    return this.request(
      this.http.post<AssistantRun>(`${apiRoot}/assistant/runs/${id}/cancel`, null),
    );
  }

  private request<T>(source: Observable<T>): Observable<T> {
    return source.pipe(catchError((error: unknown) => throwError(() => ApiError.from(error))));
  }
}

function projectUrl(projectId: string): string {
  return `${projectsUrl}/${encodeURIComponent(projectId)}`;
}

function snapshotParams(query: SnapshotQuery): HttpParams {
  let params = new HttpParams();
  params = setParam(params, 'baseline', query.baseline);
  params = setParam(params, 'search', query.search);
  params = setParam(params, 'relationship', query.relationship);
  params = setParam(params, 'sort', query.sort);
  params = setParam(params, 'direction', query.direction);
  params = setParam(params, 'cursor', query.cursor);
  params = setParam(params, 'pageSize', query.pageSize);
  params = setParam(params, 'topology', query.topology);
  params = setParam(params, 'activity', query.activity);
  params = setParam(params, 'recommendation', query.recommendation);
  params = setParam(params, 'isProtected', query.isProtected);
  return setParam(params, 'isExcluded', query.isExcluded);
}

function setParam(
  params: HttpParams,
  name: string,
  value: string | number | boolean | null | undefined,
): HttpParams {
  return value === undefined || value === null ? params : params.set(name, value.toString());
}
