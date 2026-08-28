import { Location } from '@angular/common';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { PolicyPreviewResponse, ProjectResponse } from '../../core/api/api.models';
import { ProjectSettings } from './project-settings';

const projectId = '11111111-1111-1111-1111-111111111111';

const project: ProjectResponse = {
  activeUntilDays: 20,
  branchNamespace: 'refs/heads/*',
  createdAtUtc: '2026-08-29T08:00:00Z',
  displayName: 'Dépôt Alpha',
  excludedPatterns: ['refs/heads/archive/*'],
  id: projectId,
  inactiveAfterDays: 75,
  isRepositoryAccessible: false,
  lastSuccessfulAnalysisId: '22222222-2222-2222-2222-222222222222',
  protectedPatterns: ['refs/heads/main', 'refs/heads/release/*'],
  referenceName: 'refs/heads/main',
  repositoryPath: 'D:\\Dev\\alpha',
  updatedAtUtc: '2026-08-29T10:00:00Z',
};

const preview: PolicyPreviewResponse = {
  matches: [
    {
      isExcluded: false,
      isProtected: true,
      reason: 'Protégée par le motif « refs/heads/release/* »',
      referenceName: 'refs/heads/release/2.0',
    },
  ],
};

const api = {
  getProject: vi.fn(() => of(project)),
  previewPolicy: vi.fn(() => of(preview)),
  updatePolicy: vi.fn(() => of(project)),
};

const location = {
  back: vi.fn(),
};

describe('ProjectSettings', () => {
  let fixture: ComponentFixture<ProjectSettings>;

  beforeEach(async () => {
    vi.clearAllMocks();
    api.getProject.mockReturnValue(of(project));
    api.previewPolicy.mockReturnValue(of(preview));
    api.updatePolicy.mockReturnValue(of(project));
    await TestBed.configureTestingModule({
      imports: [ProjectSettings],
      providers: [
        { provide: GitHealthApiClient, useValue: api },
        { provide: Location, useValue: location },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { paramMap: convertToParamMap({ projectId }) },
          },
        },
      ],
    }).compileComponents();
  });

  it('loads thresholds and keeps policies editable for an inaccessible repository', () => {
    createFixture();

    expect(element<HTMLInputElement>('#active-days').value).toBe('20');
    expect(element<HTMLInputElement>('#inactive-days').value).toBe('75');
    expect(element<HTMLTextAreaElement>('#protected-patterns').value).toContain(
      'refs/heads/release/*',
    );
    expect(text()).toContain('Le dépôt est actuellement inaccessible');
    expect(text()).toContain('La politique reste modifiable');
  });

  it('previews normalized patterns and renders exact reasons', () => {
    createFixture();
    setValue('#protected-patterns', ' refs/heads/main \nrefs/heads/release/*\nrefs/heads/main');
    setValue('#excluded-patterns', '\n refs/heads/archive/* \n');

    button('Prévisualiser').click();
    fixture.detectChanges();

    expect(api.previewPolicy).toHaveBeenCalledWith(projectId, {
      activeUntilDays: 20,
      excludedPatterns: ['refs/heads/archive/*'],
      inactiveAfterDays: 75,
      protectedPatterns: ['refs/heads/main', 'refs/heads/release/*'],
    });
    expect(text()).toContain('refs/heads/release/2.0');
    expect(text()).toContain('Protégée · oui');
    expect(text()).toContain('Protégée par le motif');
  });

  it('rejects thresholds in the wrong order before calling the API', () => {
    createFixture();
    setValue('#active-days', '90');
    setValue('#inactive-days', '30');

    button('Prévisualiser').click();
    fixture.detectChanges();

    expect(api.previewPolicy).not.toHaveBeenCalled();
    expect(text()).toContain('Le seuil d’inactivité doit être supérieur');
    expect(text()).toContain('Corrigez les seuils');
  });

  it('saves through the policy endpoint without validating Git access', () => {
    createFixture();
    setValue('#excluded-patterns', 'refs/heads/archive/*\nrefs/heads/obsolete/*');
    submit('form');
    fixture.detectChanges();

    expect(api.updatePolicy).toHaveBeenCalledWith(projectId, {
      activeUntilDays: 20,
      excludedPatterns: ['refs/heads/archive/*', 'refs/heads/obsolete/*'],
      inactiveAfterDays: 75,
      protectedPatterns: ['refs/heads/main', 'refs/heads/release/*'],
    });
    expect(text()).toContain('Politique enregistrée');
    expect(text()).toContain('Les faits Git sont restés inchangés');
  });

  it('shows a load error and retries', () => {
    api.getProject
      .mockReturnValueOnce(throwError(() => new Error('Projet introuvable')))
      .mockReturnValueOnce(of(project));
    createFixture();

    expect(text()).toContain('Projet introuvable');
    button('Réessayer').click();
    fixture.detectChanges();

    expect(api.getProject).toHaveBeenCalledTimes(2);
    expect(text()).toContain('Politiques de Dépôt Alpha');
  });

  function createFixture(): void {
    fixture = TestBed.createComponent(ProjectSettings);
    fixture.detectChanges();
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function element<T extends Element>(selector: string): T {
    return (fixture.nativeElement as HTMLElement).querySelector(selector) as T;
  }

  function button(label: string): HTMLButtonElement {
    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
    const match = buttons.find((candidate) => candidate.textContent?.includes(label));
    if (match === undefined) {
      throw new Error('Bouton introuvable : ' + label);
    }

    return match;
  }

  function setValue(selector: string, value: string): void {
    const control = element<HTMLInputElement | HTMLTextAreaElement>(selector);
    control.value = value;
    control.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
  }

  function submit(selector: string): void {
    element<HTMLFormElement>(selector).dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true }),
    );
  }
});
