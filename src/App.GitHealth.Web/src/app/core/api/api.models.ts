export type Uuid = string;

export type UtcDateTime = string;

export type RuntimeMode = 'native' | 'docker';

export type AnalysisRunStatus = 'Running' | 'Completed' | 'Failed' | 'Cancelled';

export type AnalysisPhase =
  'Waiting' | 'Topology' | 'Enrichment' | 'Persistence' | 'Finished' | 'Failed' | 'Cancelled';

export type BranchRelationship =
  'SameCommit' | 'CommonAncestor' | 'BranchIsAncestorOfReference' | 'NoCommonAncestor';

export type BranchTopology = 'Synchronized' | 'Ahead' | 'Merged' | 'Diverged' | 'Unrelated';

export type ActivityStatus = 'Active' | 'Aging' | 'Inactive' | 'Unknown';

export type RecommendationKind = 'Keep' | 'Review' | 'CleanupCandidate' | 'Excluded';

export type AttributionStatus = 'Available' | 'UnavailableAfterMerge';

export type SnapshotSort = 'name' | 'ahead' | 'behind' | 'activity';

export type SortDirection = 'asc' | 'desc';

export interface RuntimeInfo {
  readonly mode: RuntimeMode;
  readonly initialRepositoryPath: string | null;
  readonly repositoriesRoot: string | null;
  readonly canBrowseDirectories: boolean;
}

export interface DirectoryEntry {
  readonly name: string;
  readonly path: string;
}

export interface DirectoryListing {
  readonly currentPath: string;
  readonly parentPath: string | null;
  readonly directories: readonly DirectoryEntry[];
  readonly isTruncated: boolean;
}

export interface RepositoryValidationResponse {
  readonly canonicalPath: string;
  readonly isBare: boolean;
  readonly suggestedReference: string | null;
  readonly references: readonly string[];
}

export interface ProjectSettingsRequest {
  readonly referenceName: string | null;
  readonly branchNamespace: string;
  readonly activeUntilDays: number;
  readonly inactiveAfterDays: number;
  readonly excludedPatterns: readonly string[];
  readonly protectedPatterns: readonly string[];
}

export interface PolicyUpdateRequest {
  readonly activeUntilDays: number;
  readonly inactiveAfterDays: number;
  readonly excludedPatterns: readonly string[];
  readonly protectedPatterns: readonly string[];
}

export type PolicySnapshot = PolicyUpdateRequest;

export interface PolicyPreviewMatch {
  readonly referenceName: string;
  readonly isExcluded: boolean;
  readonly isProtected: boolean;
  readonly reason: string;
}

export interface PolicyPreviewResponse {
  readonly matches: readonly PolicyPreviewMatch[];
}

export interface CreateProjectRequest {
  readonly displayName: string;
  readonly repositoryPath: string;
  readonly settings?: ProjectSettingsRequest;
}

export interface RelocateProjectRequest {
  readonly repositoryPath: string;
}

export interface ProjectResponse {
  readonly id: Uuid;
  readonly displayName: string;
  readonly repositoryPath: string;
  readonly isRepositoryAccessible: boolean;
  readonly createdAtUtc: UtcDateTime;
  readonly updatedAtUtc: UtcDateTime;
  readonly referenceName: string | null;
  readonly branchNamespace: string;
  readonly activeUntilDays: number;
  readonly inactiveAfterDays: number;
  readonly excludedPatterns: readonly string[];
  readonly protectedPatterns: readonly string[];
  readonly lastSuccessfulAnalysisId: Uuid | null;
}

export interface AnalysisLaunchResponse {
  readonly analysisId: Uuid;
  readonly statusUrl: string;
  readonly isDuplicate: boolean;
}

export interface AnalysisStatusResponse {
  readonly analysisId: Uuid;
  readonly projectId: Uuid;
  readonly status: AnalysisRunStatus;
  readonly phase: AnalysisPhase;
  readonly startedAtUtc: UtcDateTime;
  readonly completedAtUtc: UtcDateTime | null;
  readonly failureCode: string | null;
  readonly failureMessage: string | null;
}

export interface AnalysisHistoryItem {
  readonly analysisId: Uuid;
  readonly status: AnalysisRunStatus;
  readonly startedAtUtc: UtcDateTime;
  readonly completedAtUtc: UtcDateTime | null;
  readonly referenceName: string;
  readonly branchNamespace: string;
  readonly activeUntilDays: number;
  readonly inactiveAfterDays: number;
  readonly excludedPatterns: readonly string[];
  readonly protectedPatterns: readonly string[];
  readonly failureCode: string | null;
  readonly failureMessage: string | null;
}

export interface AnalysisHistoryResponse {
  readonly items: readonly AnalysisHistoryItem[];
}

export interface SnapshotQuery {
  readonly search?: string | null;
  readonly relationship?: string | null;
  readonly sort?: string | null;
  readonly direction?: string | null;
  readonly cursor?: string | null;
  readonly pageSize?: number;
  readonly topology?: BranchTopology;
  readonly activity?: ActivityStatus;
  readonly recommendation?: RecommendationKind;
  readonly isProtected?: boolean;
  readonly isExcluded?: boolean;
}

export interface BranchSnapshotResponse {
  readonly id: Uuid;
  readonly referenceName: string;
  readonly commitId: string;
  readonly aheadCount: number;
  readonly behindCount: number;
  readonly relationship: BranchRelationship;
  readonly lastActivityAtUtc: UtcDateTime | null;
  readonly tipAuthor: string | null;
  readonly topology: BranchTopology;
  readonly activity: ActivityStatus;
  readonly recommendation: RecommendationKind;
  readonly reason: string;
  readonly isProtected: boolean;
  readonly isExcluded: boolean;
}

export interface SnapshotPageResponse {
  readonly analysisId: Uuid;
  readonly capturedAtUtc: UtcDateTime;
  readonly referenceName: string;
  readonly items: readonly BranchSnapshotResponse[];
  readonly nextCursor: string | null;
  readonly policy: PolicySnapshot;
}

export interface ContributorResponse {
  readonly name: string;
  readonly email: string;
  readonly commitCount: number;
}

export interface SnapshotDetailResponse {
  readonly analysisId: Uuid;
  readonly referenceName: string;
  readonly referenceCommit: string;
  readonly capturedAtUtc: UtcDateTime;
  readonly snapshot: BranchSnapshotResponse;
  readonly contributors: readonly ContributorResponse[];
  readonly attributionStatus: AttributionStatus;
  readonly mailmapApplied: boolean;
  readonly policy: PolicySnapshot;
}

export type RepositoryValidation = RepositoryValidationResponse;

export type Project = ProjectResponse;

export type AnalysisStatus = AnalysisStatusResponse;

export type BranchSnapshot = BranchSnapshotResponse;

export type SnapshotPage = SnapshotPageResponse;
