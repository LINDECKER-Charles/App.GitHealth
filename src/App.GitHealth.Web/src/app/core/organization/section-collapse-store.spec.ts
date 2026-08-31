import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { SectionCollapseStore } from './section-collapse-store';

/** Browser storage is not guaranteed under test: we supply our own. */
class FakeStorage {
  private readonly entries = new Map<string, string>();

  getItem(key: string): string | null {
    return this.entries.get(key) ?? null;
  }

  setItem(key: string, value: string): void {
    this.entries.set(key, value);
  }
}

describe('SectionCollapseStore', () => {
  let localStorage: FakeStorage;

  beforeEach(() => (localStorage = new FakeStorage()));

  function create(): SectionCollapseStore {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: DOCUMENT, useValue: { defaultView: { localStorage } } }],
    });
    return TestBed.inject(SectionCollapseStore);
  }

  it('collapses then expands a section', () => {
    const store = create();

    store.toggle('group:Api');
    expect(store.isCollapsed('group:Api')).toBe(true);

    store.toggle('group:Api');
    expect(store.isCollapsed('group:Api')).toBe(false);
  });

  it('finds the collapsed sections again on the next load', () => {
    create().toggle('favorites');

    expect(create().isCollapsed('favorites')).toBe(true);
  });

  it('ignores unreadable stored content', () => {
    localStorage.setItem('githealth.rail.collapsed', 'not JSON');

    expect(create().collapsedKeys().size).toBe(0);
  });
});
