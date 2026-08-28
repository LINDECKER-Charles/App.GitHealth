import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import {
  DirectoryListing,
  Project,
  RepositoryValidation,
  RuntimeInfo,
} from '../../core/api/api.models';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { Home } from './home';

const nativeRuntime: RuntimeInfo = {
  canBrowseDirectories: true,
  mode: 'native',
  repositoriesRoot: null,
};

const validation: RepositoryValidation = {
  canonicalPath: 'D:\\Dev\\alpha',
  isBare: false,
  references: ['refs/heads/main', 'refs/remotes/origin/main'],
  suggestedReference: 'refs/heads/main',
};

const project: Project = {
  activeUntilDays: 30,
  branchNamespace: 'refs/heads/*',
  createdAtUtc: '2026-08-29T08:00:00Z',
  displayName: 'Alpha',
  excludedPatterns: [],
  id: '11111111-1111-1111-1111-111111111111',
  inactiveAfterDays: 90,
  isRepositoryAccessible: true,
  lastSuccessfulAnalysisId: null,
  protectedPatterns: [],
  referenceName: 'refs/heads/main',
  repositoryPath: 'D:\\Dev\\alpha',
  updatedAtUtc: '2026-08-29T09:30:00Z',
};

const api = {
  browseDirectories: vi.fn(() => of(directoryListing('D:\\Dev'))),
  createProject: vi.fn(() => of(project)),
  getRuntime: vi.fn(() => of(nativeRuntime)),
  listProjects: vi.fn(() => of<readonly Project[]>([])),
  validateRepository: vi.fn(() => of(validation)),
};

describe('Home', () => {
  let fixture: ComponentFixture<Home>;
  let router: Router;

  beforeEach(async () => {
    vi.clearAllMocks();
    api.browseDirectories.mockReturnValue(of(directoryListing('D:\\Dev')));
    api.createProject.mockReturnValue(of(project));
    api.getRuntime.mockReturnValue(of(nativeRuntime));
    api.listProjects.mockReturnValue(of([]));
    api.validateRepository.mockReturnValue(of(validation));

    await TestBed.configureTestingModule({
      imports: [Home],
      providers: [provideRouter([]), { provide: GitHealthApiClient, useValue: api }],
    }).compileComponents();
    router = TestBed.inject(Router);
  });

  it('lists recent projects and explains an inaccessible repository', () => {
    api.listProjects.mockReturnValue(
      of([{ ...project, isRepositoryAccessible: false, lastSuccessfulAnalysisId: 'analysis-1' }]),
    );

    createFixture();

    expect(text()).toContain('Alpha');
    expect(text()).toContain('Dépôt inaccessible');
    expect(text()).toContain('Le dernier résultat reste consultable');
    const link = element<HTMLAnchorElement>('.project-card');
    expect(link.getAttribute('href')).toBe(`/projects/${project.id}`);
  });

  it('guides the user when no project is registered', () => {
    createFixture();

    expect(text()).toContain('Aucun dépôt observé pour l’instant');
    expect(text()).toContain('Son historique restera intact');
  });

  it('shows a load error and retries the request', () => {
    api.listProjects
      .mockReturnValueOnce(throwError(() => new Error('Base locale indisponible')))
      .mockReturnValueOnce(of([]));
    createFixture();

    expect(text()).toContain('Base locale indisponible');
    button('Réessayer').click();
    fixture.detectChanges();

    expect(api.listProjects).toHaveBeenCalledTimes(2);
    expect(text()).toContain('Aucun dépôt observé pour l’instant');
  });

  it('validates a path, creates the configured project and opens it', () => {
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    createFixture();
    setInput('#repository-path', '  D:\\Dev\\alpha  ');

    submit('.add-project form');
    fixture.detectChanges();

    expect(api.validateRepository).toHaveBeenCalledWith('D:\\Dev\\alpha');
    expect(element<HTMLInputElement>('#project-name').value).toBe('alpha');
    setInput('#project-name', 'Projet Alpha');
    submit('.add-project form');
    fixture.detectChanges();

    expect(api.createProject).toHaveBeenCalledWith({
      displayName: 'Projet Alpha',
      repositoryPath: 'D:\\Dev\\alpha',
      settings: {
        activeUntilDays: 30,
        branchNamespace: 'refs/heads/*',
        excludedPatterns: [],
        inactiveAfterDays: 90,
        protectedPatterns: [],
        referenceName: 'refs/heads/main',
      },
    });
    expect(navigate).toHaveBeenCalledWith(['/projects', project.id]);
  });

  it('browses native directories and copies the selected path', () => {
    const root = directoryListing('D:\\Dev', [{ name: 'alpha', path: 'D:\\Dev\\alpha' }]);
    const repository = directoryListing('D:\\Dev\\alpha');
    api.browseDirectories.mockReturnValueOnce(of(root)).mockReturnValueOnce(of(repository));
    createFixture();

    button('Parcourir').click();
    fixture.detectChanges();
    button('alpha').click();
    fixture.detectChanges();
    button('Utiliser ce chemin').click();
    fixture.detectChanges();

    expect(api.browseDirectories).toHaveBeenNthCalledWith(1, null);
    expect(api.browseDirectories).toHaveBeenNthCalledWith(2, 'D:\\Dev\\alpha');
    expect(element<HTMLInputElement>('#repository-path').value).toBe('D:\\Dev\\alpha');
  });

  function createFixture(): void {
    fixture = TestBed.createComponent(Home);
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
      throw new Error(`Bouton introuvable : ${label}`);
    }

    return match;
  }

  function setInput(selector: string, value: string): void {
    const input = element<HTMLInputElement>(selector);
    input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    fixture.detectChanges();
  }

  function submit(selector: string): void {
    element<HTMLFormElement>(selector).dispatchEvent(
      new Event('submit', { bubbles: true, cancelable: true }),
    );
  }
});

function directoryListing(
  currentPath: string,
  directories: DirectoryListing['directories'] = [],
): DirectoryListing {
  return {
    currentPath,
    directories,
    isTruncated: false,
    parentPath: null,
  };
}
