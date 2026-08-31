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
import { DesktopBridge } from '../../core/desktop/desktop-bridge';
import { pluralMessage } from '../../core/i18n/plural-message';
import { scanJobDetail, scanStateTones } from '../../core/scan/folder-scan-labels';
import { FolderScanStore } from '../../core/scan/folder-scan-store';
import { FolderScanTarget } from '../../core/scan/folder-scan.models';
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
  { value: '1', label: $localize`:@@scanFolder.depth.level1:1 level` },
  { value: '2', label: $localize`:@@scanFolder.depth.level2:2 levels` },
  { value: '3', label: $localize`:@@scanFolder.depth.level3:3 levels` },
  { value: '4', label: $localize`:@@scanFolder.depth.level4:4 levels` },
  { value: '5', label: $localize`:@@scanFolder.depth.level5:5 levels` },
];

/** Detects the repositories in a folder, then runs an analysis on the kept selection. */
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
  private readonly desktop = inject(DesktopBridge);
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

  protected readonly resultsLabel = computed(() => detectedMessage(this.repositories().length));

  protected readonly scanLabel = computed(() => analyseMessage(this.selectedCount()));

  protected readonly summaryLabel = computed(() => {
    const summary = this.scan.summary();
    const parts = [analysedMessage(summary.done, summary.total)];
    if (summary.active > 0) {
      parts.push(runningMessage(summary.active));
    }

    if (summary.failed > 0) {
      parts.push(failureMessage(summary.failed));
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
          this.error.set(
            apiErrorMessage(
              error,
              $localize`:@@scanFolder.error.discover:This folder cannot be explored.`,
            ),
          ),
      });
  }

  /** System dialog when the desktop shell offers one, folder browser otherwise. */
  protected browse(): void {
    if (!this.desktop.isAvailable) {
      this.isBrowsing.set(true);
      return;
    }

    void this.desktop.pickFolder().then((path) => {
      if (path !== null) {
        this.useDirectory(path);
      }
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

  /** Going back to the selection clears the tracking; analyses already started still finish. */
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

/** A repository with no readable baseline can be neither compared nor saved. */
function selectablePaths(repositories: readonly DiscoveredRepository[]): readonly string[] {
  return repositories
    .filter((repository) => repository.suggestedReference !== null)
    .map((repository) => repository.canonicalPath);
}

/** Each count carries its whole sentence: word order around a number is not universal. */
function detectedMessage(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@scanFolder.results.detectedOne:${count}:count: repository detected`,
    other: $localize`:@@scanFolder.results.detectedMany:${count}:count: repositories detected`,
  });
}

function analyseMessage(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@scanFolder.action.analyseOne:Analyse ${count}:count: repository`,
    other: $localize`:@@scanFolder.action.analyseMany:Analyse ${count}:count: repositories`,
  });
}

function analysedMessage(done: number, total: number): string {
  return $localize`:@@scanFolder.summary.analysed:${done}:done:/${total}:total: analysed`;
}

function runningMessage(count: number): string {
  return $localize`:@@scanFolder.summary.running:${count}:count: running`;
}

function failureMessage(count: number): string {
  return pluralMessage(count, {
    one: $localize`:@@scanFolder.summary.failureOne:${count}:count: failure`,
    other: $localize`:@@scanFolder.summary.failureMany:${count}:count: failures`,
  });
}
