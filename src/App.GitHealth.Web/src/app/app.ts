import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { databaseBackupUrl, appVersion, userGuideUrl } from './core/workspace/app-identity';
import { ProjectsStore } from './core/workspace/projects-store';
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
import { DsIcon } from './ui/core/ds-icon';
import { DsIconButton } from './ui/core/ds-icon-button';
import { DsKbd } from './ui/core/ds-kbd';
import { DsStatusDot } from './ui/core/ds-status-dot';
import { DsCallout } from './ui/surfaces/ds-callout';
import { IconName } from './ui/icon-name';

const introStorageKey = 'githealth.intro';
const introSkippedValue = 'skipped';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown)': 'onKeydown($event)' },
  imports: [
    AddRepositoryDialog,
    BootIntro,
    CommandPalette,
    DsBadge,
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
  protected readonly toast = inject(ToastService);

  protected readonly version = appVersion;
  protected readonly backupUrl = databaseBackupUrl;
  protected readonly guideUrl = userGuideUrl;
  protected readonly isIntroVisible = signal(false);
  protected readonly themeIcon = computed<IconName>(() => (this.theme.isDark() ? 'sun' : 'moon'));

  /** Sans Git, aucune analyse n'aboutit : la cause est annoncée avant le premier scan. */
  protected readonly gitFailure = computed(() => {
    const runtime = this.store.runtime();
    return runtime !== null && !runtime.isGitAvailable ? runtime.gitDiagnostic : null;
  });

  constructor() {
    this.isIntroVisible.set(this.shouldPlayIntro());
    this.store.load();
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

    // Pendant la séquence d'ouverture, les deux raccourcis servent d'abord à la couper.
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

  /** L'introduction est jouée une fois par session, et jamais en mouvement réduit. */
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
      // Sans stockage de session, l'introduction rejouera au prochain chargement.
    }
  }
}
