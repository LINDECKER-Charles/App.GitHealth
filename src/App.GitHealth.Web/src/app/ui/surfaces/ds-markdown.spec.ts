import { TestBed } from '@angular/core/testing';
import { DsMarkdown } from './ds-markdown';

const branch = 'feature/assistant-panel';
const answer = `Merge \`${branch}\` before the release.`;

describe('DsMarkdown', () => {
  async function render(text: string, targets: readonly string[]) {
    await TestBed.configureTestingModule({ imports: [DsMarkdown] }).compileComponents();
    const fixture = TestBed.createComponent(DsMarkdown);
    fixture.componentRef.setInput('text', text);
    fixture.componentRef.setInput('targets', targets);
    await fixture.whenStable();
    return fixture;
  }

  it('turns a code span naming a known branch into a control', async () => {
    const fixture = await render(answer, [branch]);

    const button = fixture.nativeElement.querySelector('button.md-target') as HTMLButtonElement;
    expect(button.type).toBe('button');
    expect(button.textContent?.trim()).toBe(branch);
    expect(button.getAttribute('aria-label')).toContain(branch);
    expect(button.title).toContain(branch);
    expect(fixture.nativeElement.querySelector('code')).toBeNull();
  });

  it('reports the branch the reader picked, spelled exactly as the host asked', async () => {
    const fixture = await render(answer, [branch]);
    const picked: string[] = [];
    fixture.componentInstance.targetSelected.subscribe((name) => picked.push(name));

    (fixture.nativeElement.querySelector('button.md-target') as HTMLButtonElement).click();

    expect(picked).toEqual([branch]);
  });

  /** Code the host knows nothing about is still code: a command must not look clickable. */
  it('leaves an unknown code span as plain code', async () => {
    const fixture = await render('Run `git gc` first.', [branch]);

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
    expect(fixture.nativeElement.querySelector('code')?.textContent).toBe('git gc');
  });

  it('renders nothing clickable when the host offers no target', async () => {
    const fixture = await render(answer, []);

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
    expect(fixture.nativeElement.querySelector('code')?.textContent).toBe(branch);
  });

  /** Branch names differ by case, so a near miss is another branch, not this one. */
  it('refuses a name that only differs by case', async () => {
    const fixture = await render(`Merge \`${branch.toUpperCase()}\` before the release.`, [branch]);

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });

  /** A fenced block is something to read or copy, not a place where controls appear. */
  it('leaves a branch name inside a fenced block untouched', async () => {
    const fixture = await render(`\`\`\`\n${branch}\n\`\`\``, [branch]);

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
    expect(fixture.nativeElement.querySelector('pre code')?.textContent).toBe(branch);
  });
});
