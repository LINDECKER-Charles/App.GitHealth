import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { displayReference } from '../../core/branches/branch-labels';
import { SnapshotExporter } from '../../core/branches/snapshot-export';
import { pluralMessage } from '../../core/i18n/plural-message';
import { analysisPhases, phaseIndex, phaseLabel } from '../../core/workspace/analysis-phases';
import { relativeTime } from '../../core/workspace/relative-time';
import { DsBadge } from '../../ui/core/ds-badge';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsSpinner } from '../../ui/core/ds-spinner';
import { DsCallout } from '../../ui/surfaces/ds-callout';
import { BranchCard } from '../branch-card/branch-card';
import { ProjectContext } from './project-context';

type TabId = 'diagnostic' | 'history' | 'settings';

const readingRepositoryLabel = $localize`:@@project.state.reading:Reading the repository…`;

/** Frame of a repository: identity, actions, tabs and the side branch card. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BranchCard, DsBadge, DsButton, DsCallout, DsIcon, DsSpinner, RouterLink, RouterOutlet],
  selector: 'app-project-shell',
  styleUrl: './project-shell.scss',
  templateUrl: './project-shell.html',
})
export class ProjectShell {
  private readonly exporter = inject(SnapshotExporter);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly context = inject(ProjectContext);
  protected readonly phases = analysisPhases;
  protected readonly phaseLabel = phaseLabel;
  protected readonly readingRepository = readingRepositoryLabel;

  private readonly params = toSignal(this.route.paramMap, { requireSync: true });
  private readonly queryParams = toSignal(this.route.queryParamMap, { requireSync: true });
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  protected readonly projectId = computed(() => this.params().get('projectId') ?? '');
  protected readonly branchId = computed(() => this.queryParams().get('branch'));

  protected readonly activeTab = computed<TabId>(() => {
    const url = this.url();
    if (url.includes('/settings')) {
      return 'settings';
    }

    return url.includes('/history') ? 'history' : 'diagnostic';
  });

  protected readonly referenceLabel = computed(() => {
    const project = this.context.project();
    return project?.referenceName === null || project === null
      ? $localize`:@@project.reference.unset:baseline still to choose`
      : displayReference(project.referenceName);
  });

  protected readonly meta = computed(() => {
    const snapshot = this.context.snapshot();
    if (snapshot === null) {
      const project = this.context.project();
      if (project === null) {
        return readingRepositoryLabel;
      }

      const added = relativeTime(project.createdAtUtc);
      return $localize`:@@project.meta.noAnalysis:No analysis saved · added ${added}`;
    }

    const at = relativeTime(snapshot.capturedAtUtc);
    const branches = branchCountLabel(snapshot.branches.length);
    const id = snapshot.analysisId.slice(0, 8);
    return $localize`:@@project.meta.capture:Capture ${at} · ${branches} · analysis ${id}`;
  });

  protected readonly runLabel = computed(() =>
    this.context.isRunning()
      ? $localize`:@@project.action.running:Analysis running…`
      : $localize`:@@project.action.run:Run an analysis`,
  );

  protected readonly currentPhaseIndex = computed(() => {
    const phase = this.context.analysis()?.phase;
    return phase === undefined ? 0 : Math.max(0, phaseIndex(phase));
  });

  protected readonly progressStep = computed(() => {
    const step = this.currentPhaseIndex() + 1;
    const total = this.phases.length;
    return $localize`:@@project.progress.step:step ${step} of ${total}`;
  });

  protected readonly currentPhaseLabel = computed(() => {
    const phase = this.context.analysis()?.phase;
    return phase === undefined ? $localize`:@@project.progress.waiting:Waiting` : phaseLabel(phase);
  });

  constructor() {
    effect(() => this.context.open(this.projectId()));
  }

  protected exportSnapshot(): void {
    const snapshot = this.context.snapshot();
    const project = this.context.project();
    if (snapshot !== null && project !== null) {
      this.exporter.export(project.displayName, snapshot.branches);
    }
  }

  protected closeBranch(): void {
    void this.router.navigate([], {
      queryParams: { branch: null },
      queryParamsHandling: 'merge',
      relativeTo: this.route,
      replaceUrl: true,
    });
  }

  protected openBranch(snapshotId: string): void {
    void this.router.navigate([], {
      queryParams: { branch: snapshotId },
      queryParamsHandling: 'merge',
      relativeTo: this.route,
    });
  }
}

function branchCountLabel(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@project.branches.one:${count}:count: branch`,
    other: $localize`:@@project.branches.many:${count}:count: branches`,
  });
}
