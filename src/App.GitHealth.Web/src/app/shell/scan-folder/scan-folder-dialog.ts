import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { DiscoveredRepository, RepositoryDiscoveryResponse } from '../../core/api/api.models';
import { displayReference } from '../../core/branches/branch-labels';
import { scanJobDetail, scanStateTones } from '../../core/scan/folder-scan-labels';
import { FolderScanStore } from '../../core/scan/folder-scan-store';
import { FolderScanTarget } from '../../core/scan/folder-scan.models';
import { plural } from '../../core/workspace/plural';
import { ProjectsStore } from '../../core/workspace/projects-store';
import { DsButton } from '../../ui/core/ds-button';
import { DsIcon } from '../../ui/core/ds-icon';
import { DsIconButton } from '../../ui/core/ds-icon-button';
import { DsStatusDot } from '../../ui/core/ds-status-dot';
import { DsCheckbox } from '../../ui/forms/ds-checkbox';
import { DsInput } from '../../ui/forms/ds-input';
import { DsSelect, SelectOption } from '../../ui/forms/ds-select';
import { DirectoryBrowser } from '../add-repository/directory-browser';

const defaultDepth = '3';

const depthOptions: readonly SelectOption[] = [
  { value: '1', label: '1 niveau' },
  { value: '2', label: '2 niveaux' },
  { value: '3', label: '3 niveaux' },
  { value: '4', label: '4 niveaux' },
  { value: '5', label: '5 niveaux' },
];

/** Détecte les dépôts d'un dossier, puis lance une analyse sur la sélection retenue. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DirectoryBrowser,
    DsButton,
    DsCheckbox,
    DsIcon,
    DsIconButton,
    DsInput,
    DsSelect,
    DsStatusDot,
  ],
  selector: 'app-scan-folder-dialog',
  styleUrl: './scan-folder-dialog.scss',
  templateUrl: './scan-folder-dialog.html',
})
export class ScanFolderDialog {
  private readonly api = inject(GitHealthApiClient);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly projects = inject(ProjectsStore);
  protected readonly scan = inject(FolderScanStore);
  readonly close = output<void>();

  protected readonly path = signal('');
  protected readonly depth = signal(defaultDepth);
  protected readonly isBrowsing = signal(false);
  protected readonly isDiscovering = signal(false);
  protected readonly discovery = signal<RepositoryDiscoveryResponse | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly selection = signal<ReadonlySet<string>>(new Set());

  protected readonly depthOptions = depthOptions;
  protected readonly displayReference = displayReference;
  protected readonly jobDetail = scanJobDetail;
  protected readonly stateTones = scanStateTones;

  protected readonly repositories = computed<readonly DiscoveredRepository[]>(
    () => this.discovery()?.repositories ?? [],
  );

  protected readonly hasDiscovered = computed(() => this.discovery() !== null);
  protected readonly selectedCount = computed(() => this.selection().size);

  protected readonly isAllSelected = computed(() => {
    const selectable = selectablePaths(this.repositories());
    return selectable.length > 0 && this.selectedCount() === selectable.length;
  });

  protected readonly canScan = computed(
    () => this.selectedCount() > 0 && !this.scan.isRunning() && !this.isDiscovering(),
  );

  protected readonly resultsLabel = computed(() => {
    const total = this.repositories().length;
    return `${plural(total, 'dépôt')} détecté${total > 1 ? 's' : ''}`;
  });

  protected readonly scanLabel = computed(
    () => `Analyser ${plural(this.selectedCount(), 'dépôt')}`,
  );

  protected readonly summaryLabel = computed(() => {
    const summary = this.scan.summary();
    const parts = [`${summary.done}/${summary.total} analysés`];
    if (summary.active > 0) {
      parts.push(`${summary.active} en cours`);
    }

    if (summary.failed > 0) {
      parts.push(plural(summary.failed, 'échec'));
    }

    return parts.join(' · ');
  });

  protected discover(): void {
    const path = this.path().trim();
    if (path.length === 0 || this.isDiscovering()) {
      return;
    }

    this.isDiscovering.set(true);
    this.error.set(null);
    this.discovery.set(null);
    this.selection.set(new Set());
    this.api
      .discoverRepositories({ path, depth: Number(this.depth()) })
      .pipe(
        finalize(() => this.isDiscovering.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (response) => this.applyDiscovery(response),
        error: (error: unknown) =>
          this.error.set(apiErrorMessage(error, 'Ce dossier ne peut pas être exploré.')),
      });
  }

  protected useDirectory(path: string): void {
    this.isBrowsing.set(false);
    this.path.set(path);
    this.discover();
  }

  protected isSelected(repository: DiscoveredRepository): boolean {
    return this.selection().has(repository.canonicalPath);
  }

  protected toggle(repository: DiscoveredRepository, isChecked: boolean): void {
    if (repository.suggestedReference === null) {
      return;
    }

    this.selection.update((current) => {
      const next = new Set(current);
      if (isChecked) {
        next.add(repository.canonicalPath);
      } else {
        next.delete(repository.canonicalPath);
      }

      return next;
    });
  }

  protected toggleAll(): void {
    this.selection.set(
      this.isAllSelected() ? new Set() : new Set(selectablePaths(this.repositories())),
    );
  }

  protected scanSelection(): void {
    if (this.canScan()) {
      this.scan.start(this.selectedTargets());
    }
  }

  /** Revenir à la sélection efface le suivi ; les analyses déjà lancées, elles, se terminent. */
  protected backToSelection(): void {
    this.scan.reset();
  }

  private applyDiscovery(response: RepositoryDiscoveryResponse): void {
    this.discovery.set(response);
    this.selection.set(new Set(selectablePaths(response.repositories)));
  }

  private selectedTargets(): readonly FolderScanTarget[] {
    const selection = this.selection();
    return this.repositories()
      .filter((repository) => selection.has(repository.canonicalPath))
      .map((repository) => ({
        canonicalPath: repository.canonicalPath,
        name: repository.suggestedName,
        referenceName: repository.suggestedReference,
        projectId: repository.trackedProjectId,
      }));
  }
}

/** Un dépôt sans référence lisible ne peut être ni comparé ni enregistré. */
function selectablePaths(repositories: readonly DiscoveredRepository[]): readonly string[] {
  return repositories
    .filter((repository) => repository.suggestedReference !== null)
    .map((repository) => repository.canonicalPath);
}
