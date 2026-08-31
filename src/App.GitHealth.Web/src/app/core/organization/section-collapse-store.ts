import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

const storageKey = 'githealth.rail.collapsed';

/**
 * Collapsed sections of the rail. This is window state, not repository data: it stays in
 * the browser, and when storage is unavailable it simply lasts for the session.
 */
@Injectable({ providedIn: 'root' })
export class SectionCollapseStore {
  private readonly document = inject(DOCUMENT);
  private readonly collapsed = signal<ReadonlySet<string>>(this.restore());

  readonly collapsedKeys = this.collapsed.asReadonly();

  isCollapsed(key: string): boolean {
    return this.collapsed().has(key);
  }

  toggle(key: string): void {
    const next = new Set(this.collapsed());
    if (!next.delete(key)) {
      next.add(key);
    }

    this.collapsed.set(next);
    this.persist(next);
  }

  private persist(keys: ReadonlySet<string>): void {
    try {
      this.document.defaultView?.localStorage.setItem(storageKey, JSON.stringify([...keys]));
    } catch {
      // With no persistent storage, the collapse lasts for the current session.
    }
  }

  private restore(): ReadonlySet<string> {
    try {
      const stored = this.document.defaultView?.localStorage.getItem(storageKey);
      const parsed: unknown = stored === null || stored === undefined ? [] : JSON.parse(stored);
      return new Set(Array.isArray(parsed) ? parsed.filter(isText) : []);
    } catch {
      return new Set();
    }
  }
}

function isText(value: unknown): value is string {
  return typeof value === 'string';
}
