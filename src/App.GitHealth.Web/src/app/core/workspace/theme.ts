import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const storageKey = 'githealth.theme';
const themeAttribute = 'data-theme';

/** Le thème sombre s'active par `data-theme="dark"` sur `<html>`, comme le prévoit le design system. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly current = signal<Theme>('light');

  readonly theme = this.current.asReadonly();
  readonly isDark = computed(() => this.current() === 'dark');

  constructor() {
    this.apply(this.restore());
  }

  toggle(): void {
    this.apply(this.current() === 'dark' ? 'light' : 'dark');
  }

  private apply(theme: Theme): void {
    this.current.set(theme);
    this.document.documentElement.setAttribute(themeAttribute, theme);
    try {
      this.document.defaultView?.localStorage.setItem(storageKey, theme);
    } catch {
      // Un navigateur sans stockage persistant reste utilisable : le thème vaut pour la session.
    }
  }

  private restore(): Theme {
    try {
      return this.document.defaultView?.localStorage.getItem(storageKey) === 'dark'
        ? 'dark'
        : 'light';
    } catch {
      return 'light';
    }
  }
}
