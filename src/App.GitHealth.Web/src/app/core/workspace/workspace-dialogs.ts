import { Injectable, computed, signal } from '@angular/core';

/** Global surfaces: the palette and the add-repository dialog are reachable from anywhere. */
@Injectable({ providedIn: 'root' })
export class WorkspaceDialogs {
  readonly isPaletteOpen = signal(false);
  readonly isAddRepositoryOpen = signal(false);
  readonly isScanFolderOpen = signal(false);

  /** Repository whose group is being chosen, or `null` when the dialog is closed. */
  readonly projectGroupId = signal<string | null>(null);
  readonly isProjectGroupOpen = computed(() => this.projectGroupId() !== null);

  togglePalette(): void {
    this.isPaletteOpen.update((open) => !open);
  }

  openPalette(): void {
    this.isPaletteOpen.set(true);
  }

  closePalette(): void {
    this.isPaletteOpen.set(false);
  }

  openAddRepository(): void {
    this.closeAll();
    this.isAddRepositoryOpen.set(true);
  }

  closeAddRepository(): void {
    this.isAddRepositoryOpen.set(false);
  }

  openScanFolder(): void {
    this.closeAll();
    this.isScanFolderOpen.set(true);
  }

  closeScanFolder(): void {
    this.isScanFolderOpen.set(false);
  }

  openProjectGroup(projectId: string): void {
    this.closeAll();
    this.projectGroupId.set(projectId);
  }

  closeProjectGroup(): void {
    this.projectGroupId.set(null);
  }

  closeAll(): void {
    this.isPaletteOpen.set(false);
    this.isAddRepositoryOpen.set(false);
    this.isScanFolderOpen.set(false);
    this.projectGroupId.set(null);
  }
}
