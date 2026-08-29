import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { DsButton } from './ds-button';

@Component({
  imports: [DsButton],
  template: `
    <button
      dsButton
      [variant]="variant()"
      [size]="size()"
      [iconLeft]="iconLeft()"
      [loading]="loading()"
      [disabled]="disabled()"
    >
      Lancer une analyse
    </button>
  `,
})
class HostComponent {
  readonly variant = signal<'primary' | 'secondary' | 'ghost' | 'danger'>('primary');
  readonly size = signal<'sm' | 'md' | 'lg'>('sm');
  readonly iconLeft = signal<'refresh-cw' | null>('refresh-cw');
  readonly loading = signal(false);
  readonly disabled = signal(false);
}

describe('DsButton', () => {
  async function render() {
    await TestBed.configureTestingModule({ imports: [HostComponent] }).compileComponents();
    const fixture = TestBed.createComponent(HostComponent);
    await fixture.whenStable();
    return fixture;
  }

  it('habille le bouton natif sans élément enveloppant', async () => {
    const fixture = await render();
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.classList.contains('etb-btn')).toBe(true);
    expect(button.classList.contains('etb-btn--primary')).toBe(true);
    expect(button.classList.contains('etb-btn--sm')).toBe(true);
    expect(button.textContent?.trim()).toBe('Lancer une analyse');
  });

  it('affiche l’icône de gauche, remplacée par le spinner en chargement', async () => {
    const fixture = await render();
    expect(fixture.nativeElement.querySelector('ds-icon')).not.toBeNull();

    fixture.componentInstance.loading.set(true);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('ds-icon')).toBeNull();
    expect(fixture.nativeElement.querySelector('ds-spinner')).not.toBeNull();
  });

  it('désactive le bouton pendant le chargement', async () => {
    const fixture = await render();
    fixture.componentInstance.loading.set(true);
    await fixture.whenStable();
    expect((fixture.nativeElement.querySelector('button') as HTMLButtonElement).disabled).toBe(
      true,
    );
  });

  it('change de variante et de taille avec ses entrées', async () => {
    const fixture = await render();
    fixture.componentInstance.variant.set('ghost');
    fixture.componentInstance.size.set('lg');
    await fixture.whenStable();
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    expect(button.classList.contains('etb-btn--ghost')).toBe(true);
    expect(button.classList.contains('etb-btn--lg')).toBe(true);
    expect(button.classList.contains('etb-btn--primary')).toBe(false);
  });
});
