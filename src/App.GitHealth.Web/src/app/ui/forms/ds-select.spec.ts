import { TestBed } from '@angular/core/testing';
import { DsSelect } from './ds-select';

const options = [
  { value: '', label: 'Toute topologie' },
  { value: 'Merged', label: 'Fusionnées' },
  { value: 'Diverged', label: 'Divergentes' },
];

describe('DsSelect', () => {
  async function render(value: string) {
    await TestBed.configureTestingModule({ imports: [DsSelect] }).compileComponents();
    const fixture = TestBed.createComponent(DsSelect);
    fixture.componentRef.setInput('options', options);
    fixture.componentRef.setInput('value', value);
    await fixture.whenStable();
    return fixture;
  }

  it('sélectionne l’option qui porte la valeur courante', async () => {
    const fixture = await render('Merged');
    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    expect(select.value).toBe('Merged');
    expect(select.selectedOptions[0].textContent?.trim()).toBe('Fusionnées');
  });

  it('remonte la valeur choisie par l’utilisateur', async () => {
    const fixture = await render('');
    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    select.value = 'Diverged';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(fixture.componentInstance.value()).toBe('Diverged');
  });
});
