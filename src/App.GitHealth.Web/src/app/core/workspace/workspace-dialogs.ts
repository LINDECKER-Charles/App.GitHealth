import { Injectable, signal } from '@angular/core';

/** Ouverture des surfaces globales : la palette et l'ajout de dépôt sont joignables de partout. */
@Injectable({ providedIn: 'root' })
export class WorkspaceDialogs {
  readonly isPaletteOpen = signal(false);
  readonly isAddRepositoryOpen = signal(false);

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
    this.isPaletteOpen.set(false);
    this.isAddRepositoryOpen.set(true);
  }

  closeAddRepository(): void {
    this.isAddRepositoryOpen.set(false);
  }

  closeAll(): void {
    this.isPaletteOpen.set(false);
    this.isAddRepositoryOpen.set(false);
  }
}
