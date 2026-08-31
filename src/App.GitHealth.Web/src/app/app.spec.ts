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
  gitDiagnostic: 'Git cannot be found. Locations tried: the PATH.',
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

  it('mounts the shell: top bar, rail and routed area', async () => {
    const compiled = (await render()).nativeElement as HTMLElement;
    expect(compiled.querySelector('.topbar')).not.toBeNull();
    expect(compiled.querySelector('app-project-rail')).not.toBeNull();
    expect(compiled.querySelector('router-outlet')).not.toBeNull();
  });

  it('exposes the local database backup', async () => {
    const compiled = (await render()).nativeElement as HTMLElement;
    const link = compiled.querySelector('.backup-action') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe(databaseBackupUrl);
    expect(link.hasAttribute('download')).toBe(true);
  });

  it('opens the palette when the search field is clicked', async () => {
    const fixture = await render();
    const dialogs = TestBed.inject(WorkspaceDialogs);
    expect(dialogs.isPaletteOpen()).toBe(false);

    (fixture.nativeElement.querySelector('.topbar-search') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(dialogs.isPaletteOpen()).toBe(true);
    expect(fixture.nativeElement.querySelector('app-command-palette')).not.toBeNull();
  });

  it('opens and closes the palette from the keyboard', async () => {
    const fixture = await render();
    const dialogs = TestBed.inject(WorkspaceDialogs);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', metaKey: true }));
    await fixture.whenStable();
    expect(dialogs.isPaletteOpen()).toBe(true);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    await fixture.whenStable();
    expect(dialogs.isPaletteOpen()).toBe(false);
  });

  it('announces that Git is unavailable before the first scan', async () => {
    const fixture = await render();
    expect(fixture.nativeElement.querySelector('.workspace-alert')).toBeNull();

    TestBed.inject(ProjectsStore).runtime.set(runtimeWithoutGit);
    await fixture.whenStable();

    const alert = fixture.nativeElement.querySelector('.workspace-alert') as HTMLElement;
    expect(alert.textContent).toContain(runtimeWithoutGit.gitDiagnostic);
  });

  it('offers the update only when it is published', async () => {
    const fixture = await render();
    expect(fixture.nativeElement.querySelector('.update-action')).toBeNull();

    TestBed.inject(UpdateStore).status.set({
      availability: 'Available',
      currentVersion: '0.1.0',
      availableVersion: '0.1.1',
    });
    await fixture.whenStable();

    const action = fixture.nativeElement.querySelector('.update-action') as HTMLButtonElement;
    expect(action.textContent?.trim()).toBe('Update');
  });

  it('does not replay the intro once it has been skipped in the session', async () => {
    const fixture = await render();
    expect(fixture.nativeElement.querySelector('app-boot-intro')).toBeNull();
  });
});
