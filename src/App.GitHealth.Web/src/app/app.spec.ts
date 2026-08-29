import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should host routed features', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).not.toBeNull();
  });

  it('should expose the local database backup', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const link = fixture.nativeElement.querySelector('.backup-action') as HTMLAnchorElement;

    expect(link.getAttribute('href')).toBe('/api/exports/database');
    expect(link.hasAttribute('download')).toBe(true);
  });
});
