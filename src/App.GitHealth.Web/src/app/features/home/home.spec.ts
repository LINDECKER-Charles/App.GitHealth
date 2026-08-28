import { TestBed } from '@angular/core/testing';
import { Home } from './home';

describe('Home', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Home],
    }).compileComponents();
  });

  it('should explain the read-only purpose', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();

    const heading = fixture.nativeElement.querySelector('h1') as HTMLHeadingElement;

    expect(heading.textContent).toContain('Garder Git intact');
  });

  it('should link to the health endpoint', () => {
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges();

    const healthLink = fixture.nativeElement.querySelector(
      'a[href="/health"]',
    ) as HTMLAnchorElement;

    expect(healthLink.textContent).toContain('Interroger /health');
  });
});
