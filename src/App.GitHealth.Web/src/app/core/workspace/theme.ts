import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const storageKey = 'githealth.theme';
const themeAttribute = 'data-theme';

/** The dark theme switches on via `data-theme="dark"` on `<html>`, as the design system says. */
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
      // A browser with no persistent storage stays usable: the theme lasts for the session.
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
