import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { GitHealthApiClient } from '../../core/api/git-health-api-client';
import { ProjectResponse } from '../../core/api/api.models';
import { RepositoryRelocation } from './repository-relocation';

const project: ProjectResponse = {
  activeUntilDays: 30,
  branchNamespace: 'refs/heads/*',
  createdAtUtc: '2026-08-29T08:00:00Z',
  displayName: 'Dépôt Alpha',
  excludedPatterns: [],
  id: '11111111-1111-1111-1111-111111111111',
  inactiveAfterDays: 90,
  isRepositoryAccessible: false,
  lastSuccessfulAnalysisId: '22222222-2222-2222-2222-222222222222',
  protectedPatterns: ['refs/heads/main'],
  referenceName: 'refs/heads/main',
  repositoryPath: 'D:\\Dev\\ancien',
  updatedAtUtc: '2026-08-29T10:00:00Z',
};

const relocatedProject = {
  ...project,
  isRepositoryAccessible: true,
  repositoryPath: 'D:\\Dev\\nouveau',
};

const api = {
  relocateProject: vi.fn(() => of(relocatedProject)),
};

describe('RepositoryRelocation', () => {
  let fixture: ComponentFixture<RepositoryRelocation>;

  beforeEach(async () => {
    vi.clearAllMocks();
    api.relocateProject.mockReturnValue(of(relocatedProject));
    await TestBed.configureTestingModule({
      imports: [RepositoryRelocation],
      providers: [{ provide: GitHealthApiClient, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(RepositoryRelocation);
    fixture.componentRef.setInput('project', project);
    fixture.detectChanges();
  });

  it('relocates the same project and announces history preservation', () => {
    const emitted = vi.fn();
    fixture.componentInstance.relocated.subscribe(emitted);
    setPath(relocatedProject.repositoryPath);

    submit();
    fixture.detectChanges();

    expect(api.relocateProject).toHaveBeenCalledWith(project.id, {
      repositoryPath: relocatedProject.repositoryPath,
    });
    expect(emitted).toHaveBeenCalledWith(relocatedProject);
    expect(text()).toContain('Les analyses précédentes sont conservées');
  });

  it('rejects the unchanged path without calling the API', () => {
    setPath(project.repositoryPath);
    submit();
    fixture.detectChanges();

    expect(api.relocateProject).not.toHaveBeenCalled();
    expect(text()).toContain('Indiquez le nouveau chemin');
  });

  it('renders a precise API failure', () => {
    api.relocateProject.mockReturnValue(throwError(() => new Error('Référence absente')));
    setPath(relocatedProject.repositoryPath);
    submit();
    fixture.detectChanges();

    expect(text()).toContain('Référence absente');
  });

  function setPath(value: string): void {
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }

  function submit(): void {
    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }
});
