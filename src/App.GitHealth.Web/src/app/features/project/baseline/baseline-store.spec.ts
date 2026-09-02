import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NavigationExtras, Params, Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { ProjectResponse } from '../../../core/api/api.models';
import { BaselineStore } from './baseline-store';
import { ProjectContext } from '../project-context';

const primary = 'refs/heads/main';
const secondary = 'refs/heads/dev';

/** Only the URL matters here: a stub records the navigations instead of performing them. */
class RouterStub {
  readonly navigations: NavigationExtras[] = [];
  private readonly params = new BehaviorSubject<Params>({});

  readonly routerState = {
    root: {
      queryParams: this.params.asObservable(),
      snapshot: { queryParams: {} as Params },
    },
  };

  setQueryParams(params: Params): void {
    this.params.next(params);
  }

  navigate(_commands: readonly unknown[], extras: NavigationExtras): Promise<boolean> {
    this.navigations.push(extras);
    return Promise.resolve(true);
  }
}

describe('BaselineStore', () => {
  let store: BaselineStore;
  let context: ProjectContext;
  let router: RouterStub;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useClass: RouterStub },
      ],
    });

    router = TestBed.inject(Router) as unknown as RouterStub;
    context = TestBed.inject(ProjectContext);
    store = TestBed.inject(BaselineStore);
    context.project.set(aProject([primary, secondary]));
  });

  afterEach(() => TestBed.resetTestingModule());

  it('reads the primary baseline as long as the URL asks for none', () => {
    expect(store.requested()).toBeNull();
    expect(store.selected()).toBe(primary);
  });

  it('offers one option per declared baseline, named as the user names them', () => {
    expect(store.hasChoice()).toBe(true);
    expect(store.options()).toEqual([
      { value: primary, label: 'main' },
      { value: secondary, label: 'dev' },
    ]);
  });

  it('states a single baseline instead of offering a choice', () => {
    context.project.set(aProject([primary]));

    expect(store.hasChoice()).toBe(false);
    expect(store.selected()).toBe(primary);
  });

  it('writes the chosen baseline into the URL and drops the capture being read', () => {
    store.select(secondary);

    expect(router.navigations).toEqual([
      {
        queryParams: { baseline: secondary, capture: null },
        queryParamsHandling: 'merge',
      },
    ]);
  });

  it('releases the parameter as soon as the primary baseline is chosen again', () => {
    router.setQueryParams({ baseline: secondary });

    store.select(primary);

    expect(store.requested()).toBe(secondary);
    expect(router.navigations[0].queryParams).toEqual({ baseline: null, capture: null });
  });

  it('stays put when the baseline being read is chosen again', () => {
    router.setQueryParams({ baseline: secondary });

    store.select(secondary);

    expect(router.navigations).toEqual([]);
  });

  it('keeps the baseline being read in the tab links', () => {
    expect(store.baselineLink()).toEqual({});

    router.setQueryParams({ baseline: secondary });

    expect(store.baselineLink()).toEqual({ baseline: secondary });
    expect(store.selected()).toBe(secondary);
  });
});

function aProject(referenceNames: readonly string[]): ProjectResponse {
  return {
    id: 'p1',
    displayName: 'Repository',
    repositoryPath: 'F:/repository',
    isRepositoryAccessible: true,
    createdAtUtc: '2026-08-30T10:00:00.000Z',
    updatedAtUtc: '2026-08-30T10:00:00.000Z',
    referenceName: referenceNames[0],
    referenceNames,
    branchNamespace: 'refs/heads/*',
    activeUntilDays: 30,
    inactiveAfterDays: 90,
    excludedPatterns: [],
    protectedPatterns: [],
    isFavorite: false,
    groupName: null,
    lastSuccessfulAnalysisId: null,
  };
}
