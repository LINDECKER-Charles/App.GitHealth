import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Visualisation } from './visualisation';

/** Tient lieu de sous-vue : seul compte ici ce que l'URL désigne et ce qu'elle conserve. */
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
      { path: 'topologie', component: SubViewProbe },
      { path: 'registre', component: SubViewProbe },
      { path: 'ecart', component: SubViewProbe },
    ],
  },
];

describe('Visualisation', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter(routes)] }));
  afterEach(() => TestBed.resetTestingModule());

  function tabs(root: HTMLElement): readonly HTMLButtonElement[] {
    return Array.from(root.querySelectorAll<HTMLButtonElement>('.etb-seg__item'));
  }

  it('accorde le commutateur à la sous-vue que l’URL désigne', async () => {
    const harness = await RouterTestingHarness.create('/visualisation/registre');
    const selected = tabs(harness.fixture.nativeElement).filter(
      (tab) => tab.getAttribute('aria-selected') === 'true',
    );

    expect(selected).toHaveLength(1);
    expect(selected[0].textContent?.trim()).toBe("Registre d'activité");
  });

  it('garde la capture regardée en changeant de sous-vue', async () => {
    const harness = await RouterTestingHarness.create('/visualisation/topologie?capture=a1');
    const target = tabs(harness.fixture.nativeElement).find((tab) =>
      tab.textContent?.includes('Écart'),
    );

    target?.click();
    await harness.fixture.whenStable();

    const url = TestBed.inject(Router).url;
    expect(url).toContain('/visualisation/ecart');
    expect(url).toContain('capture=a1');
  });
});
