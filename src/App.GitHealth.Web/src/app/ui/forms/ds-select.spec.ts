import { TestBed } from '@angular/core/testing';
import { DsSelect } from './ds-select';

const options = [
  { value: '', label: 'Any topology' },
  { value: 'Merged', label: 'Merged' },
  { value: 'Diverged', label: 'Diverged' },
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

  it('selects the option carrying the current value', async () => {
    const fixture = await render('Merged');
    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    expect(select.value).toBe('Merged');
    expect(select.selectedOptions[0].textContent?.trim()).toBe('Merged');
  });

  it('reports back the value the user chose', async () => {
    const fixture = await render('');
    const select = fixture.nativeElement.querySelector('select') as HTMLSelectElement;
    select.value = 'Diverged';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(fixture.componentInstance.value()).toBe('Diverged');
  });
});
