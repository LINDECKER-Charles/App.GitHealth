export type Uuid = string;

export type UtcDateTime = string;

export type RuntimeMode = 'native' | 'docker';

export type UpdateAvailability = 'Unsupported' | 'UpToDate' | 'Unknown' | 'Available';

export type AnalysisRunStatus = 'Running' | 'Completed' | 'Failed' | 'Cancelled';

export type AnalysisPhase =
  'Waiting' | 'Topology' | 'Enrichment' | 'Persistence' | 'Finished' | 'Failed' | 'Cancelled';

/** How far a running analysis has got with one reference. */
export type ReferenceProgressState = 'Listed' | 'Measuring' | 'Measured' | 'Enriching' | 'Read';

export type BranchRelationship =
  'SameCommit' | 'CommonAncestor' | 'BranchIsAncestorOfReference' | 'NoCommonAncestor';

export type BranchTopology = 'Synchronized' | 'Ahead' | 'Merged' | 'Diverged' | 'Unrelated';

export type ActivityStatus = 'Active' | 'Aging' | 'Inactive' | 'Unknown';

export type RecommendationKind = 'Keep' | 'Review' | 'CleanupCandidate' | 'Excluded' | 'Merged';

export type AttributionStatus = 'Available' | 'UnavailableAfterMerge';

export type SnapshotSort = 'name' | 'ahead' | 'behind' | 'activity';

export type SortDirection = 'asc' | 'desc';

export interface RuntimeInfo {
  readonly mode: RuntimeMode;
  readonly initialRepositoryPath: string | null;
  readonly repositoriesRoot: string | null;
  readonly canBrowseDirectories: boolean;
  /** Git answers at startup; false, and no analysis can succeed. */
  readonly isGitAvailable: boolean;
  /** Git executable selected, or `null` when it cannot be found. */
  readonly gitExecutablePath: string | null;
  /** Git version, or the reason it is unavailable. */
  readonly gitDiagnostic: string;
}

export interface UpdateStatus {
  readonly availability: UpdateAvailability;
  /** Installed version, or `null` outside a managed installation. */
  readonly currentVersion: string | null;
  /** More recent published version, filled in only when one exists. */
  readonly availableVersion: string | null;
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

export interface RepositoryDiscoveryRequest {
  readonly path: string;
  readonly depth?: number;
}

export interface DiscoveredRepository {
  readonly canonicalPath: string;
  readonly suggestedName: string;
  readonly suggestedReference: string | null;
  readonly referenceCount: number;
  readonly isBare: boolean;
  /** Project already saved on this repository, or `null` when it remains to be added. */
  readonly trackedProjectId: Uuid | null;
}

export interface RepositoryDiscoveryResponse {
  readonly rootPath: string;
  readonly repositories: readonly DiscoveredRepository[];
  readonly isTruncated: boolean;
}

export interface ProjectSettingsRequest {
  readonly referenceName: string | null;
  /** Full ordered baseline list; when absent, `referenceName` is the only baseline. */
  readonly referenceNames?: readonly string[];
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

export interface ProjectOrganizationRequest {
  readonly isFavorite: boolean;
  /** Owning group; `null` moves the repository out of any group. */
  readonly groupName: string | null;
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
  /** Primary baseline; always the first entry of `referenceNames`. */
  readonly referenceName: string | null;
  /** Every branch this repository is compared against, in display order. */
  readonly referenceNames: readonly string[];
  readonly branchNamespace: string;
  readonly activeUntilDays: number;
  readonly inactiveAfterDays: number;
  readonly excludedPatterns: readonly string[];
  readonly protectedPatterns: readonly string[];
  readonly isFavorite: boolean;
  readonly groupName: string | null;
  readonly lastSuccessfulAnalysisId: Uuid | null;
}

export interface AnalysisLaunchItem {
  readonly analysisId: Uuid;
  readonly referenceName: string;
  readonly statusUrl: string;
  readonly isDuplicate: boolean;
}

export interface AnalysisLaunchResponse {
  /** One run per baseline measured by this launch. */
  readonly analyses: readonly AnalysisLaunchItem[];
  /** Run of the primary baseline. */
  readonly analysisId: Uuid;
  readonly statusUrl: string;
  readonly isDuplicate: boolean;
}

export interface BaselineResponse {
  readonly referenceName: string;
  readonly position: number;
  readonly isPrimary: boolean;
  readonly lastSuccessfulAnalysisId: Uuid | null;
  readonly lastCapturedAtUtc: UtcDateTime | null;
  readonly branchCount: number;
}

export interface BaselineListResponse {
  readonly items: readonly BaselineResponse[];
  /** References the repository offers now; empty when it cannot be read. */
  readonly availableReferences: readonly string[];
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
  /** What the run is doing right now; absent once it is no longer being followed. */
  readonly progress: AnalysisProgressResponse | null;
}

export interface AnalysisProgressResponse {
  /** Every reference of the run, in read order, each with what is known of it so far. */
  readonly references: readonly AnalysisReferenceProgress[];
  /** Tail of the Git commands run, oldest first. */
  readonly commands: readonly AnalysisCommandTrace[];
  /** Commands run since the start, including those no longer in the tail. */
  readonly commandCount: number;
}

export interface AnalysisReferenceProgress {
  readonly referenceName: string;
  readonly commitId: string;
  readonly state: ReferenceProgressState;
  readonly lastActivityAtUtc: UtcDateTime | null;
  readonly tipAuthor: string | null;
  readonly mergeBaseCommit: string | null;
  readonly aheadCount: number | null;
  readonly behindCount: number | null;
  readonly topology: BranchTopology | null;
  readonly topContributor: string | null;
  readonly contributorCount: number | null;
}

export interface AnalysisCommandTrace {
  /** Rank of the command in the run: what lets the console append without repeating. */
  readonly sequence: number;
  readonly commandLine: string;
  readonly durationMs: number;
  readonly exitCode: number;
  readonly output: string | null;
}

export interface AnalysisHistoryItem {
  readonly analysisId: Uuid;
  readonly status: AnalysisRunStatus;
  readonly startedAtUtc: UtcDateTime;
  readonly completedAtUtc: UtcDateTime | null;
  readonly capturedAtUtc: UtcDateTime | null;
  readonly referenceName: string;
  readonly referenceCommit: string | null;
  readonly branchNamespace: string;
  readonly activeUntilDays: number;
  readonly inactiveAfterDays: number;
  readonly excludedPatterns: readonly string[];
  readonly protectedPatterns: readonly string[];
  readonly gitVersion: string | null;
  readonly branchCount: number;
  readonly failureCode: string | null;
  readonly failureMessage: string | null;
}

export interface AnalysisHistoryResponse {
  readonly items: readonly AnalysisHistoryItem[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

export interface SnapshotQuery {
  /** Baseline whose latest capture is read; absent means the primary one. */
  readonly baseline?: string | null;
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
  /** Author of most of the commits this branch adds; null once it is merged. */
  readonly topContributor: ContributorResponse | null;
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

export type AssistantRunStatus = 'Running' | 'Completed' | 'Failed' | 'Cancelled';

/** One command-line agent GitHealth knows how to drive, and whether it can be driven here. */
export interface AssistantAgent {
  readonly id: string;
  readonly name: string;
  readonly isAvailable: boolean;
  /** What the CLI answered to its version flag, or `null` when it never answered. */
  readonly version: string | null;
  readonly executablePath: string | null;
  readonly installationUrl: string;
  /** Where the search looked and what to set, when the agent was not found. */
  readonly unavailableReason: string | null;
  /** Effort levels this agent accepts, cheapest first. */
  readonly efforts: readonly string[];
  readonly defaultEffort: string;
}

export interface AssistantAgentList {
  /** False turns the feature off for the installation, whatever is on the machine. */
  readonly isEnabled: boolean;
  readonly agents: readonly AssistantAgent[];
}

/** The capture as it would be handed to an agent, shown before anything is sent. */
export interface AssistantBriefing {
  readonly baseline: string;
  readonly capturedAtUtc: UtcDateTime;
  readonly branchCount: number;
  readonly omittedBranchCount: number;
  readonly text: string;
}

export interface AssistantRunRequest {
  readonly agentId: string;
  readonly question: string;
  readonly baseline?: string | null;
  /** One of the agent's declared levels; anything else is refused by the API. */
  readonly effort: string;
  /** Thread to continue. Absent opens a new one, whose id comes back on the run. */
  readonly conversationId?: Uuid | null;
}

/** What the agent is busy with. The interface phrases it; the API only names it. */
export type AssistantStepKind = 'Waiting' | 'Thinking' | 'Tool' | 'Writing';

/** One thing the agent did, sent while it runs and never kept once it has. */
export interface AssistantRunStep {
  readonly kind: AssistantStepKind;
  /** The capture tool that was called. Empty for every other kind. */
  readonly label: string;
  /** What the call asked for, or what the agent said of its own reasoning. */
  readonly detail: string | null;
  readonly atUtc: UtcDateTime;
}

export interface AssistantRun {
  readonly runId: Uuid;
  readonly projectId: Uuid;
  readonly agentId: string;
  readonly agentName: string;
  /** The level actually used, which may be the agent's default. */
  readonly effort: string;
  readonly question: string;
  /** The command as it was run, with this run's bridge token blanked. */
  readonly commandLine: string;
  /** Thread this run belongs to, whether it opened it or continued one. */
  readonly conversationId: Uuid;
  /** Rows of the capture the agent may read, which bounds any count it gives. */
  readonly branchCount: number;
  readonly status: AssistantRunStatus;
  readonly startedAtUtc: UtcDateTime;
  readonly completedAtUtc: UtcDateTime | null;
  /** What the agent has been doing, oldest first, whole on every poll. */
  readonly steps: readonly AssistantRunStep[];
  /** What the agent wrote since the offset asked for, not the whole log. */
  readonly trace: string;
  /** Offset to send on the next poll. */
  readonly traceOffset: number;
  readonly answer: string | null;
  readonly failureCode: string | null;
  readonly failureMessage: string | null;
  /** The agent wrote past the budget and was stopped short. */
  readonly isTruncated: boolean;
}

/** What the panel and the policy screen both need before showing anything. */
export interface AssistantStatus {
  /** Null while sending this repository's captures has never been allowed. */
  readonly consentGrantedAtUtc: UtcDateTime | null;
  readonly conversationCount: number;
}

export interface AssistantConsentRequest {
  readonly granted: boolean;
}

/** One line of the conversation list, across every baseline of a repository. */
export interface AssistantConversationSummary {
  readonly id: Uuid;
  readonly analysisId: Uuid;
  readonly baseline: string;
  readonly agentId: string;
  readonly agentName: string;
  readonly title: string;
  readonly answerCount: number;
  readonly startedAtUtc: UtcDateTime;
  readonly updatedAtUtc: UtcDateTime;
}

export interface AssistantConversationList {
  readonly conversations: readonly AssistantConversationSummary[];
}

export type AssistantMessageRole = 'user' | 'agent';

/** One stored turn. A question and an answer share a shape, so a thread reads in order. */
export interface AssistantMessage {
  readonly id: Uuid;
  readonly position: number;
  readonly role: AssistantMessageRole;
  readonly text: string;
  readonly writtenAtUtc: UtcDateTime;
  /** How an agent turn ended. Null on a question, which cannot fail. */
  readonly status: AssistantRunStatus | null;
  readonly effort: string | null;
  readonly commandLine: string | null;
  readonly failureCode: string | null;
  readonly failureMessage: string | null;
  readonly durationMs: number | null;
  readonly isTruncated: boolean;
}

export interface AssistantConversation {
  readonly id: Uuid;
  readonly analysisId: Uuid;
  readonly baseline: string;
  readonly agentId: string;
  readonly agentName: string;
  readonly title: string;
  /** Rows the agent could read when the thread was written. */
  readonly branchCount: number;
  readonly startedAtUtc: UtcDateTime;
  readonly updatedAtUtc: UtcDateTime;
  readonly messages: readonly AssistantMessage[];
}

export interface AssistantPurgeResult {
  readonly deleted: number;
}
