import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { ApiError } from './api-error';
import {
  AnalysisLaunchResponse,
  AnalysisStatusResponse,
  CreateProjectRequest,
  DirectoryListing,
  ProjectResponse,
  ProjectSettingsRequest,
  RepositoryValidationResponse,
  RuntimeInfo,
  SnapshotDetailResponse,
  SnapshotPageResponse,
  SnapshotQuery,
} from './api.models';

const apiRoot = '/api';
const projectsUrl = `${apiRoot}/projects`;

@Injectable({ providedIn: 'root' })
export class GitHealthApiClient {
  private readonly http = inject(HttpClient);

  getRuntime(): Observable<RuntimeInfo> {
    return this.request(this.http.get<RuntimeInfo>(`${apiRoot}/runtime`));
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

  launchAnalysis(projectId: string): Observable<AnalysisLaunchResponse> {
    const url = `${projectUrl(projectId)}/analyses`;
    return this.request(this.http.post<AnalysisLaunchResponse>(url, null));
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

  private request<T>(source: Observable<T>): Observable<T> {
    return source.pipe(catchError((error: unknown) => throwError(() => ApiError.from(error))));
  }
}

function projectUrl(projectId: string): string {
  return `${projectsUrl}/${encodeURIComponent(projectId)}`;
}

function snapshotParams(query: SnapshotQuery): HttpParams {
  let params = new HttpParams();
  params = setParam(params, 'search', query.search);
  params = setParam(params, 'relationship', query.relationship);
  params = setParam(params, 'sort', query.sort);
  params = setParam(params, 'direction', query.direction);
  params = setParam(params, 'cursor', query.cursor);
  return setParam(params, 'pageSize', query.pageSize);
}

function setParam(
  params: HttpParams,
  name: string,
  value: string | number | null | undefined,
): HttpParams {
  return value === undefined || value === null ? params : params.set(name, value.toString());
}
