import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { RuntimeInfo } from './core/api/api.models';
import { UpdateStore } from './core/updates/update-store';
import { ProjectsStore } from './core/workspace/projects-store';
import { WorkspaceDialogs } from './core/workspace/workspace-dialogs';
import { databaseBackupUrl } from './core/workspace/app-identity';

const runtimeWithoutGit: RuntimeInfo = {
  mode: 'native',
  initialRepositoryPath: null,
  repositoriesRoot: null,
  canBrowseDirectories: true,
  isGitAvailable: false,
  gitExecutablePath: null,
  gitDiagnostic: 'Git est introuvable. Emplacements testés : le PATH.',
};

describe('App', () => {
  beforeEach(async () => {
    window.sessionStorage.setItem('githealth.intro', 'skipped');
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();
  });

  afterEach(() => window.sessionStorage.clear());

  async function render() {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    return fixture;
  }

  it('monte la coquille : barre supérieure, rail et zone routée', async () => {
    const compiled = (await render()).nativeElement as HTMLElement;
    expect(compiled.querySelector('.topbar')).not.toBeNull();
    expect(compiled.querySelector('app-project-rail')).not.toBeNull();
    expect(compiled.querySelector('router-outlet')).not.toBeNull();
  });

  it('expose la sauvegarde locale de la base', async () => {
    const compiled = (await render()).nativeElement as HTMLElement;
    const link = compiled.querySelector('.backup-action') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe(databaseBackupUrl);
    expect(link.hasAttribute('download')).toBe(true);
  });

  it('ouvre la palette au clic sur le champ de recherche', async () => {
    const fixture = await render();
    const dialogs = TestBed.inject(WorkspaceDialogs);
    expect(dialogs.isPaletteOpen()).toBe(false);

    (fixture.nativeElement.querySelector('.topbar-search') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(dialogs.isPaletteOpen()).toBe(true);
    expect(fixture.nativeElement.querySelector('app-command-palette')).not.toBeNull();
  });

  it('ouvre et ferme la palette au clavier', async () => {
    const fixture = await render();
    const dialogs = TestBed.inject(WorkspaceDialogs);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', metaKey: true }));
    await fixture.whenStable();
    expect(dialogs.isPaletteOpen()).toBe(true);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await fixture.whenStable();
    expect(dialogs.isPaletteOpen()).toBe(false);
  });

  it('annonce l’indisponibilité de Git avant le premier scan', async () => {
    const fixture = await render();
    expect(fixture.nativeElement.querySelector('.workspace-alert')).toBeNull();

    TestBed.inject(ProjectsStore).runtime.set(runtimeWithoutGit);
    await fixture.whenStable();

    const alert = fixture.nativeElement.querySelector('.workspace-alert') as HTMLElement;
    expect(alert.textContent).toContain(runtimeWithoutGit.gitDiagnostic);
  });

  it('propose la mise à jour seulement quand elle est publiée', async () => {
    const fixture = await render();
    expect(fixture.nativeElement.querySelector('.update-action')).toBeNull();

    TestBed.inject(UpdateStore).status.set({
      availability: 'Available',
      currentVersion: '0.1.0-rc.1',
      availableVersion: '0.1.0-rc.2',
    });
    await fixture.whenStable();

    const action = fixture.nativeElement.querySelector('.update-action') as HTMLButtonElement;
    expect(action.textContent).toContain('Mettre à jour');
  });

  it('ne rejoue pas l’introduction une fois passée dans la session', async () => {
    const fixture = await render();
    expect(fixture.nativeElement.querySelector('app-boot-intro')).toBeNull();
  });
});
