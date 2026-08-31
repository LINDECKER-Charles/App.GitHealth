import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Visualisation } from './visualisation';

/** Stands in for a sub-view: all that counts here is what the URL names and what it keeps. */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-sub-view-probe',
  template: '',
})
class SubViewProbe {}

const routes = [
  {
    path: 'visualisation',
    component: Visualisation,
    children: [
      { path: 'topology', component: SubViewProbe },
      { path: 'register', component: SubViewProbe },
      { path: 'drift', component: SubViewProbe },
    ],
  },
];

describe('Visualisation', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter(routes)] }));
  afterEach(() => TestBed.resetTestingModule());

  function tabs(root: HTMLElement): readonly HTMLButtonElement[] {
    return Array.from(root.querySelectorAll<HTMLButtonElement>('.etb-seg__item'));
  }

  it('matches the switch to the sub-view the URL names', async () => {
    const harness = await RouterTestingHarness.create('/visualisation/register');
    const selected = tabs(harness.fixture.nativeElement).filter(
      (tab) => tab.getAttribute('aria-selected') === 'true',
    );

    expect(selected).toHaveLength(1);
    expect(selected[0].textContent?.trim()).toBe('Activity register');
  });

  it('keeps the capture being viewed when the sub-view changes', async () => {
    const harness = await RouterTestingHarness.create('/visualisation/topology?capture=a1');
    const target = tabs(harness.fixture.nativeElement).find((tab) =>
      tab.textContent?.includes('Drift'),
    );

    expect(target).toBeDefined();
    target?.click();
    await harness.fixture.whenStable();

    const url = TestBed.inject(Router).url;
    expect(url).toContain('/visualisation/drift');
    expect(url).toContain('capture=a1');
  });
});
