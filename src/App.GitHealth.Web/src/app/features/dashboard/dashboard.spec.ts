import { provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { ProjectResponse, SnapshotPageResponse } from '../../core/api/api.models';
import { Dashboard } from './dashboard';

describe('Dashboard', () => {
  let fixture: ComponentFixture<Dashboard>;
  const project: ProjectResponse = {
    activeUntilDays: 30,
    branchNamespace: 'refs/heads/*',
    createdAtUtc: '2026-08-29T08:00:00Z',
    displayName: 'Dépôt café',
    excludedPatterns: [],
    id: 'project-1',
    inactiveAfterDays: 90,
    isRepositoryAccessible: true,
    lastSuccessfulAnalysisId: 'analysis-1',
    protectedPatterns: [],
    referenceName: 'refs/heads/main',
    repositoryPath: 'D:/Dépôts/café',
    updatedAtUtc: '2026-08-29T09:00:00Z',
  };
  const page: SnapshotPageResponse = {
    analysisId: 'analysis-1',
    capturedAtUtc: '2026-08-29T09:00:00Z',
    items: [
      {
        activity: 'Active',
        aheadCount: 3,
        behindCount: 1,
        commitId: 'abc123',
        id: 'snapshot-1',
        isExcluded: false,
        isProtected: false,
        lastActivityAtUtc: '2026-08-28T09:00:00Z',
        reason: 'La branche contient des commits propres.',
        recommendation: 'Review',
        referenceName: 'refs/heads/feature/été-à-Tokyo',
        relationship: 'CommonAncestor',
        tipAuthor: 'Zoë Martin',
        topology: 'Diverged',
      },
    ],
    nextCursor: null,
    referenceName: 'refs/heads/main',
  };
  const api = {
    getLatestSnapshots: vi.fn(() => of(page)),
    getProject: vi.fn(() => of(project)),
    launchAnalysis: vi.fn(),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Dashboard],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: GitHealthApiClient, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ projectId: 'project-1' }),
              queryParamMap: convertToParamMap({ search: 'été' }),
            },
          },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(Dashboard);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('affiche les faits Git sans dépendre uniquement de la couleur', () => {
    const content = fixture.nativeElement.textContent;
    expect(content).toContain('Dépôt café');
    expect(content).toContain('feature/été-à-Tokyo');
    expect(content).toContain('+3');
    expect(content).toContain('−1');
    expect(content).toContain('Diverged');
    expect(content).toContain('Zoë Martin');
  });

  it('restaure la recherche depuis les paramètres de route', () => {
    const input = fixture.nativeElement.querySelector(
      'input[type="search"]',
    ) as HTMLInputElement | null;
    expect(input?.value).toBe('été');
    expect(api.getLatestSnapshots).toHaveBeenCalledWith(
      'project-1',
      expect.objectContaining({ search: 'été' }),
    );
  });
});
