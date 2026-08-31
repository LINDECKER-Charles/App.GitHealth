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

  it('masks the locally served SVG and takes the parent colour', async () => {
    const host = await render();
    expect(host.classList.contains('etb-icon')).toBe(true);
    expect(host.style.maskImage).toBe('url("/ds/icons/git-branch.svg")');
    expect(host.style.width).toBe('16px');
  });

  it('stays decorative while no label is supplied', async () => {
    const host = await render();
    expect(host.getAttribute('aria-hidden')).toBe('true');
    expect(host.getAttribute('role')).toBe('presentation');
  });
});
