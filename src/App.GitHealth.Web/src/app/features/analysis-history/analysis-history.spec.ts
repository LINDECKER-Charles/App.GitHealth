import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { Observable, Subject, of, throwError } from 'rxjs';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { AnalysisHistoryResponse } from '../../core/api/api.models';
import { AnalysisHistory } from './analysis-history';

const history: AnalysisHistoryResponse = {
  items: [
    {
      activeUntilDays: 30,
      analysisId: 'analysis-success',
      branchNamespace: 'refs/heads/*',
      completedAtUtc: '2026-08-29T09:01:00Z',
      excludedPatterns: ['refs/heads/archive/*'],
      failureCode: null,
      failureMessage: null,
      inactiveAfterDays: 90,
      protectedPatterns: ['refs/heads/release/*'],
      referenceName: 'refs/heads/main',
      startedAtUtc: '2026-08-29T09:00:00Z',
      status: 'Completed',
    },
    {
      activeUntilDays: 20,
      analysisId: 'analysis-failed',
      branchNamespace: 'refs/remotes/origin/*',
      completedAtUtc: '2026-08-28T09:00:10Z',
      excludedPatterns: [],
      failureCode: 'git.unavailable',
      failureMessage: 'Git ne répond plus.',
      inactiveAfterDays: 60,
      protectedPatterns: [],
      referenceName: 'refs/remotes/origin/main',
      startedAtUtc: '2026-08-28T09:00:00Z',
      status: 'Failed',
    },
    {
      activeUntilDays: 30,
      analysisId: 'analysis-cancelled',
      branchNamespace: 'refs/heads/*',
      completedAtUtc: '2026-08-27T09:00:10Z',
      excludedPatterns: [],
      failureCode: 'analysis.cancelled',
      failureMessage: 'Arrêt demandé par l’utilisateur.',
      inactiveAfterDays: 90,
      protectedPatterns: [],
      referenceName: 'refs/heads/main',
      startedAtUtc: '2026-08-27T09:00:00Z',
      status: 'Cancelled',
    },
  ],
};

interface ApiStub {
  readonly getAnalysisHistory: ReturnType<typeof vi.fn>;
}

describe('AnalysisHistory', () => {
  let api: ApiStub;

  beforeEach(() => {
    api = { getAnalysisHistory: vi.fn(() => of(history)) };
  });

  it('affiche les réussites, incidents et politiques capturées', async () => {
    const fixture = await render(api);
    const element = fixture.nativeElement as HTMLElement;
    const text = element.textContent ?? '';

    expect(text).toContain('Réussie');
    expect(text).toContain('Échec');
    expect(text).toContain('Annulée');
    expect(text).toContain('Git ne répond plus.');
    expect(text).toContain('refs/heads/archive/*');
    expect(text).toContain('refs/heads/release/*');
    expect(text).toContain('Active jusqu’à 30 jours');

    const historicalLinks = element.querySelectorAll<HTMLAnchorElement>('a.history-link');
    expect(historicalLinks).toHaveLength(1);
    expect(historicalLinks[0]?.getAttribute('href')).toBe(
      '/projects/project-42/analyses/analysis-success',
    );
  });

  it('guide vers le diagnostic quand aucun historique n’existe', async () => {
    api.getAnalysisHistory.mockReturnValue(of({ items: [] }));

    const fixture = await render(api);
    const element = fixture.nativeElement as HTMLElement;
    const link = element.querySelector<HTMLAnchorElement>('.empty-state a');

    expect(element.textContent).toContain('Aucune analyse enregistrée.');
    expect(link?.getAttribute('href')).toBe('/projects/project-42');
  });

  it('annonce le chargement sans masquer la structure de page', async () => {
    const pending = new Subject<AnalysisHistoryResponse>();
    api.getAnalysisHistory.mockReturnValue(pending as Observable<AnalysisHistoryResponse>);

    const fixture = await render(api);
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('[role="status"]')?.textContent).toContain('Lecture du journal');
    expect(element.querySelector('h1')?.textContent).toContain('Historique des analyses');
  });

  it('explique une erreur et permet de réessayer au clavier', async () => {
    const failure = new HttpErrorResponse({
      status: 503,
      statusText: 'Service Unavailable',
      error: {
        detail: 'La base de données est occupée.',
        code: 'database.busy',
        status: 503,
      },
    });
    api.getAnalysisHistory
      .mockReturnValueOnce(throwError(() => failure))
      .mockReturnValueOnce(of(history));
    const fixture = await render(api);
    const element = fixture.nativeElement as HTMLElement;
    const retry = element.querySelector<HTMLButtonElement>('.error-state button');

    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      'La base de données est occupée.',
    );
    expect(retry?.type).toBe('button');

    retry?.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(api.getAnalysisHistory).toHaveBeenCalledTimes(2);
    expect(element.textContent).toContain('analysis-success'.slice(0, 8));
  });
});

async function render(api: ApiStub): Promise<ComponentFixture<AnalysisHistory>> {
  await TestBed.configureTestingModule({
    imports: [AnalysisHistory],
    providers: [
      provideRouter([]),
      { provide: GitHealthApiClient, useValue: api },
      {
        provide: ActivatedRoute,
        useValue: {
          snapshot: { paramMap: convertToParamMap({ projectId: 'project-42' }) },
        },
      },
    ],
  }).compileComponents();
  const fixture = TestBed.createComponent(AnalysisHistory);
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture;
}
