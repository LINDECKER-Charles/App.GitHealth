import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { databaseBackupUrl, appVersion, userGuideUrl } from './core/workspace/app-identity';
import { ProjectsStore } from './core/workspace/projects-store';
import { UpdateStore } from './core/updates/update-store';
import { ThemeService } from './core/workspace/theme';
import { ToastService } from './core/workspace/toast';
import { WorkspaceDialogs } from './core/workspace/workspace-dialogs';
import { AddRepositoryDialog } from './shell/add-repository/add-repository-dialog';
import { BootIntro } from './shell/boot/boot-intro';
import { CommandPalette } from './shell/palette/command-palette';
import { ProjectGroupDialog } from './shell/project-group/project-group-dialog';
import { ProjectRail } from './shell/rail/project-rail';
import { ScanFolderDialog } from './shell/scan-folder/scan-folder-dialog';
import { DsBadge } from './ui/core/ds-badge';
import { DsButton } from './ui/core/ds-button';
import { DsIcon } from './ui/core/ds-icon';
import { DsIconButton } from './ui/core/ds-icon-button';
import { DsKbd } from './ui/core/ds-kbd';
import { DsStatusDot } from './ui/core/ds-status-dot';
import { CalloutTone, DsCallout } from './ui/surfaces/ds-callout';
import { IconName } from './ui/icon-name';

const introStorageKey = 'githealth.intro';
const introSkippedValue = 'skipped';

interface WorkspaceAlert {
  readonly tone: CalloutTone;
  readonly title: string;
  readonly message: string;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown)': 'onKeydown($event)' },
  imports: [
    AddRepositoryDialog,
    BootIntro,
    CommandPalette,
    DsBadge,
    DsButton,
    DsCallout,
    DsIcon,
    DsIconButton,
    DsKbd,
    DsStatusDot,
    ProjectGroupDialog,
    ProjectRail,
    RouterOutlet,
    ScanFolderDialog,
  ],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  private readonly document = inject(DOCUMENT);
  private readonly store = inject(ProjectsStore);

  protected readonly dialogs = inject(WorkspaceDialogs);
  protected readonly theme = inject(ThemeService);
  protected readonly updates = inject(UpdateStore);
  protected readonly toast = inject(ToastService);

  protected readonly version = appVersion;
  protected readonly backupUrl = databaseBackupUrl;
  protected readonly guideUrl = userGuideUrl;
  protected readonly isIntroVisible = signal(false);
  protected readonly themeIcon = computed<IconName>(() => (this.theme.isDark() ? 'sun' : 'moon'));

  protected readonly updateLabel = computed(() =>
    this.updates.isApplying()
      ? $localize`:@@app.update.applying:Updating…`
      : $localize`:@@app.update.action:Update`,
  );

  /**
   * One alert at a time, the most blocking first: without Git no analysis succeeds,
   * whereas a failed update leaves the application usable.
   */
  protected readonly alert = computed<WorkspaceAlert | null>(() => {
    const runtime = this.store.runtime();
    if (runtime !== null && !runtime.isGitAvailable) {
      return {
        tone: 'danger',
        title: $localize`:@@app.alert.gitUnavailable:Git is unavailable`,
        message: runtime.gitDiagnostic,
      };
    }

    const failure = this.updates.error();
    if (failure === null) {
      return null;
    }

    return {
      tone: 'warning',
      title: $localize`:@@app.alert.updateFailed:Update failed`,
      message: failure,
    };
  });

  constructor() {
    this.isIntroVisible.set(this.shouldPlayIntro());
    this.store.load();
    this.updates.load();
  }

  protected dismissIntro(wasSkipped: boolean): void {
    this.isIntroVisible.set(false);
    if (wasSkipped) {
      this.remember(introStorageKey, introSkippedValue);
    }
  }

  protected onKeydown(event: KeyboardEvent): void {
    const opensPalette = (event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k';
    if (!opensPalette && event.key !== 'Escape') {
      return;
    }

    if (opensPalette) {
      event.preventDefault();
    }

    // During the opening sequence, both shortcuts serve first to interrupt it.
    if (this.isIntroVisible()) {
      this.dismissIntro(true);
      return;
    }

    if (opensPalette) {
      this.dialogs.togglePalette();
      return;
    }

    this.dialogs.closeAll();
  }

  /** The intro plays once per session, and never under reduced motion. */
  private shouldPlayIntro(): boolean {
    const view = this.document.defaultView;
    if (view?.matchMedia?.('(prefers-reduced-motion: reduce)').matches === true) {
      return false;
    }

    try {
      return view?.sessionStorage.getItem(introStorageKey) !== introSkippedValue;
    } catch {
      return true;
    }
  }

  private remember(key: string, value: string): void {
    try {
      this.document.defaultView?.sessionStorage.setItem(key, value);
    } catch {
      // Without session storage, the intro plays again on the next load.
    }
  }
}
