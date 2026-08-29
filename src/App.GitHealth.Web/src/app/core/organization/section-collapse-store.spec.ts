import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { SectionCollapseStore } from './section-collapse-store';

/** Le stockage du navigateur n'est pas garanti sous test : on fournit le nôtre. */
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

  it('replie puis déplie une section', () => {
    const store = create();

    store.toggle('group:Api');
    expect(store.isCollapsed('group:Api')).toBe(true);

    store.toggle('group:Api');
    expect(store.isCollapsed('group:Api')).toBe(false);
  });

  it('retrouve les sections repliées au chargement suivant', () => {
    create().toggle('favorites');

    expect(create().isCollapsed('favorites')).toBe(true);
  });

  it('ignore un contenu stocké illisible', () => {
    localStorage.setItem('githealth.rail.collapsed', 'pas du JSON');

    expect(create().collapsedKeys().size).toBe(0);
  });
});
