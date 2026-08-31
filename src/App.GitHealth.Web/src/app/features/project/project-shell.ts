import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  ActivatedRoute,
  NavigationEnd,
  Params,
  Router,
  RouterLink,
  RouterOutlet,
} from '@angular/router';
import { filter, map } from 'rxjs';
import { displayReference } from '../../core/branches/branch-labels';
import { SnapshotExporter } from '../../core/branches/snapshot-export';
import { analysisPhases, phaseIndex, phaseLabel } from '../../core/workspace/analysis-phases';
import { relativeTime } from '../../core/workspace/relative-time';
import { plural } from '../../core/workspace/plural';
import { DsBadge } from '../../ui/core/ds-badge';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsSpinner } from '../../ui/core/ds-spinner';
import { DsSelect } from '../../ui/forms/ds-select';
import { DsCallout } from '../../ui/surfaces/ds-callout';
import { BranchFiche } from '../branch-fiche/branch-fiche';
import { CaptureStore } from './capture-store';
import { ProjectContext } from './project-context';

type TabId = 'diagnostic' | 'visualisation' | 'history' | 'settings';

/** Premier segment reconnu dans l'URL, sinon le diagnostic. L'ordre fixe la priorité. */
const tabSegments: readonly (readonly [string, TabId])[] = [
  ['/visualisation', 'visualisation'],
  ['/history', 'history'],
  ['/settings', 'settings'],
];

/** Cadre d'un dépôt : identité, actions, onglets et fiche de branche latérale. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    BranchFiche,
    DsBadge,
    DsButton,
    DsCallout,
    DsIcon,
    DsSelect,
    DsSpinner,
    RouterLink,
    RouterOutlet,
  ],
  selector: 'app-project-shell',
  styleUrl: './project-shell.scss',
  templateUrl: './project-shell.html',
})
export class ProjectShell {
  private readonly exporter = inject(SnapshotExporter);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly captures = inject(CaptureStore);
  protected readonly context = inject(ProjectContext);
  protected readonly phases = analysisPhases;
  protected readonly phaseLabel = phaseLabel;

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

  protected readonly activeTab = computed<TabId>(() => tabFromUrl(this.url()));

  protected readonly referenceLabel = computed(() => {
    const project = this.context.project();
    return project?.referenceName === null || project === null
      ? 'référence à choisir'
      : displayReference(project.referenceName);
  });

  /** Le sélecteur dit déjà quelle capture est lue : la ligne dit son âge et son volume. */
  protected readonly meta = computed(() => {
    const snapshot = this.captures.snapshot();
    if (snapshot === null) {
      const project = this.context.project();
      return project === null
        ? 'Lecture du dépôt…'
        : `Aucune analyse enregistrée · ajouté ${relativeTime(project.createdAtUtc)}`;
    }

    return `${plural(snapshot.branches.length, 'branche')} · capturées ${relativeTime(snapshot.capturedAtUtc)}`;
  });

  /** Identité stable : chaque onglet reconstruirait son lien à chaque cycle sinon. */
  protected readonly captureLink = computed<Params>(() => this.captures.captureLink());

  /** Une capture archivée porte les verdicts de son époque : le dire évite de les croire d'aujourd'hui. */
  protected readonly archivedNotice = computed(() => {
    const selected = this.captures.selected();
    return selected === null ? 'verdicts figés' : `${selected.short} · verdicts figés à cette date`;
  });

  protected readonly runLabel = computed(() =>
    this.context.isRunning() ? 'Analyse en cours…' : 'Lancer une analyse',
  );

  protected readonly currentPhaseIndex = computed(() => {
    const phase = this.context.analysis()?.phase;
    return phase === undefined ? 0 : Math.max(0, phaseIndex(phase));
  });

  protected readonly progressStep = computed(
    () => `étape ${this.currentPhaseIndex() + 1} sur ${this.phases.length}`,
  );

  protected readonly currentPhaseLabel = computed(() => {
    const phase = this.context.analysis()?.phase;
    return phase === undefined ? 'En attente' : phaseLabel(phase);
  });

  constructor() {
    effect(() => this.context.open(this.projectId()));
  }

  /** Relancer, c'est vouloir le résultat suivant : rester sur une capture figée le cacherait. */
  protected launchAnalysis(): void {
    this.captures.followLatest();
    this.context.launchAnalysis();
  }

  protected exportSnapshot(): void {
    const snapshot = this.captures.snapshot();
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

function tabFromUrl(url: string): TabId {
  return tabSegments.find(([segment]) => url.includes(segment))?.[1] ?? 'diagnostic';
}
