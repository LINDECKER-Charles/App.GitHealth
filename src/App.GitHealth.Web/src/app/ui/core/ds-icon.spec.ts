import { TestBed } from '@angular/core/testing';
import { DsIcon } from './ds-icon';

describe('DsIcon', () => {
  async function render(): Promise<HTMLElement> {
    await TestBed.configureTestingModule({ imports: [DsIcon] }).compileComponents();
    const fixture = TestBed.createComponent(DsIcon);
    fixture.componentRef.setInput('name', 'git-branch');
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('masque le SVG servi localement et prend la couleur du parent', async () => {
    const host = await render();
    expect(host.classList.contains('etb-icon')).toBe(true);
    expect(host.style.maskImage).toBe('url("/ds/icons/git-branch.svg")');
    expect(host.style.width).toBe('16px');
  });

  it('reste décoratif tant qu’aucun libellé n’est fourni', async () => {
    const host = await render();
    expect(host.getAttribute('aria-hidden')).toBe('true');
    expect(host.getAttribute('role')).toBe('presentation');
  });
});
