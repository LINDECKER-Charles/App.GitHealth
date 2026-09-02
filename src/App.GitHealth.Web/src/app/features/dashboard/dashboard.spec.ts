import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { BranchSnapshotResponse } from '../../core/api/api.models';
import { LoadedSnapshot } from '../../core/branches/snapshot-loader';
import { ProjectContext } from '../project/project-context';
import { Dashboard } from './dashboard';

const day = 86_400_000;

function branch(
  name: string,
  overrides: Partial<BranchSnapshotResponse> = {},
): BranchSnapshotResponse {
  return {
    id: name,
    referenceName: `refs/heads/${name}`,
    commitId: '1f484960946cabcdef',
    aheadCount: 4,
    behindCount: 3,
    relationship: 'CommonAncestor',
    lastActivityAtUtc: new Date(Date.now() - 3 * day).toISOString(),
    tipAuthor: 'Camille Rousseau',
    topology: 'Diverged',
    activity: 'Active',
    recommendation: 'Review',
    reason: 'Diverged history to review',
    isProtected: false,
    isExcluded: false,
    topContributor: null,
    ...overrides,
  };
}

const snapshot: LoadedSnapshot = {
  analysisId: 'c80c2489-0000-0000-0000-000000000000',
  capturedAtUtc: '2026-08-29T11:21:16Z',
  referenceName: 'refs/heads/main',
  policy: {
    activeUntilDays: 30,
    inactiveAfterDays: 90,
    excludedPatterns: ['refs/heads/archive/*'],
    protectedPatterns: ['refs/heads/main'],
  },
  branches: [
    branch('feature/export-csv'),
    branch('docs/guide', { topology: 'Ahead', behindCount: 0, recommendation: 'Keep' }),
    branch('archive/2023-legacy', { recommendation: 'Excluded', isExcluded: true }),
  ],
  isTruncated: false,
};

describe('Dashboard', () => {
  let context: ProjectContext;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
    context = TestBed.inject(ProjectContext);
    context.latestSnapshot.set(snapshot);
    context.isLoadingLatest.set(false);
  });

  async function render() {
    const fixture = TestBed.createComponent(Dashboard);
    await fixture.whenStable();
    return fixture;
  }

  it('renders one row per branch of the snapshot', async () => {
    const rows = (await render()).nativeElement.querySelectorAll('.dashboard-table tbody tr');
    expect(rows).toHaveLength(3);
  });

  it('counts the recommendations in the tiles', async () => {
    const counts = Array.from(
      (await render()).nativeElement.querySelectorAll('.dashboard-tile-count'),
    ).map((node) => (node as HTMLElement).textContent?.trim());
    expect(counts).toEqual(['3', '1', '0', '1', '0', '1']);
  });

  it('flags an excluded branch with an icon', async () => {
    const compiled = (await render()).nativeElement as HTMLElement;
    expect(compiled.querySelectorAll('.branch-flag')).toHaveLength(1);
  });

  it('publishes the displayed order for the card navigation', async () => {
    await render();
    expect(context.visibleBranchIds()).toHaveLength(3);
  });

  it('empties the table and offers an empty state when no filter matches', async () => {
    const fixture = await render();
    const search = fixture.nativeElement.querySelector('.filter-search input') as HTMLInputElement;
    search.value = 'no-such-branch';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelectorAll('.dashboard-table tbody tr')).toHaveLength(0);
    expect(fixture.nativeElement.querySelector('ds-empty-state')).not.toBeNull();
  });

  it('offers the first analysis when no snapshot exists', async () => {
    context.latestSnapshot.set(null);
    const compiled = (await render()).nativeElement as HTMLElement;
    expect(compiled.querySelector('.dashboard-first-scan')).not.toBeNull();
    expect(compiled.textContent).toContain('This repository has not been measured yet');
  });
});
