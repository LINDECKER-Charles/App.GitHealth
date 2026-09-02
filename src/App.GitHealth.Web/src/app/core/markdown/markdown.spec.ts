import { parseMarkdown } from './markdown-blocks';
import { parseSpans } from './markdown-inline';
import {
  MarkdownBlock,
  MarkdownCodeBlock,
  MarkdownListBlock,
  MarkdownSpans,
  MarkdownTableBlock,
} from './markdown.models';

describe('parseSpans', () => {
  it('keeps plain text as one span', () => {
    expect(parseSpans('nothing to see')).toEqual([{ kind: 'text', text: 'nothing to see' }]);
  });

  it('reads bold, italic and inline code around their text', () => {
    const spans = parseSpans('keep **main**, drop *stale*, run `git branch`');

    expect(spans).toEqual([
      { kind: 'text', text: 'keep ' },
      { kind: 'strong', text: 'main' },
      { kind: 'text', text: ', drop ' },
      { kind: 'emphasis', text: 'stale' },
      { kind: 'text', text: ', run ' },
      { kind: 'code', text: 'git branch' },
    ]);
  });

  it('reads the underscore spellings too', () => {
    expect(parseSpans('__bold__ and _italic_')).toEqual([
      { kind: 'strong', text: 'bold' },
      { kind: 'text', text: ' and ' },
      { kind: 'emphasis', text: 'italic' },
    ]);
  });

  /** Two asterisks must not read as two emphases with nothing between them. */
  it('prefers bold over emphasis on a double marker', () => {
    expect(parseSpans('**both**')).toEqual([{ kind: 'strong', text: 'both' }]);
  });

  /** What is inside backticks is literal, including markers that would otherwise apply. */
  it('leaves markers inside a code span alone', () => {
    expect(parseSpans('`**not bold**`')).toEqual([{ kind: 'code', text: '**not bold**' }]);
  });

  it('reads a browsable link', () => {
    expect(parseSpans('see [the guide](https://example.test/g)')).toEqual([
      { kind: 'text', text: 'see ' },
      { kind: 'link', text: 'the guide', href: 'https://example.test/g' },
    ]);
  });

  /**
   * The answer comes from a language model. A link it cannot be trusted with stays visible
   * as the text it was written as, rather than becoming something clickable.
   */
  it.each(['javascript:alert(1)', 'file:///etc/passwd', 'data:text/html,x', '/local/path'])(
    'refuses to make %s clickable',
    (href) => {
      const source = `[click](${href})`;

      const spans = parseSpans(source);

      expect(spans.some((span) => span.kind === 'link')).toBe(false);
      expect(flatten(spans)).toBe(source);
    },
  );
});

describe('parseMarkdown', () => {
  it('reads a heading with its level', () => {
    const [block] = parseMarkdown('### Branches to clean up');

    expect(block).toEqual({
      kind: 'heading',
      level: 3,
      spans: [{ kind: 'text', text: 'Branches to clean up' }],
    });
  });

  it('joins the lines of a paragraph and splits on the blank line', () => {
    const blocks = parseMarkdown('one\ntwo\n\nthree');

    expect(blocks).toHaveLength(2);
    expect(text(blocks[0])).toBe('one two');
    expect(text(blocks[1])).toBe('three');
  });

  it('reads a bullet list', () => {
    const [block] = parseMarkdown('- first\n- second\n- third');

    const list = block as MarkdownListBlock;
    expect(list.kind).toBe('list');
    expect(list.ordered).toBe(false);
    expect(list.items.map(flatten)).toEqual(['first', 'second', 'third']);
  });

  it('reads a numbered list as ordered', () => {
    const [block] = parseMarkdown('1. first\n2. second');

    const list = block as MarkdownListBlock;
    expect(list.ordered).toBe(true);
    expect(list.items).toHaveLength(2);
  });

  it('keeps a fenced block verbatim with its language', () => {
    const [block] = parseMarkdown('```bash\ngit branch -d old\n\ngit gc\n```');

    expect(block).toEqual<MarkdownCodeBlock>({
      kind: 'code',
      language: 'bash',
      code: 'git branch -d old\n\ngit gc',
    });
  });

  /** A run stopped mid-sentence still has to render, fence closed or not. */
  it('closes an unterminated fence at the end of the text', () => {
    const [block] = parseMarkdown('```\nstill writing');

    expect((block as MarkdownCodeBlock).code).toBe('still writing');
  });

  it('reads a table with its header', () => {
    const [block] = parseMarkdown('| branch | ahead |\n|---|---|\n| main | 0 |\n| feat/x | 12 |');

    const table = block as MarkdownTableBlock;
    expect(table.kind).toBe('table');
    expect(table.header.map(flatten)).toEqual(['branch', 'ahead']);
    expect(table.rows.map((row) => row.map(flatten))).toEqual([
      ['main', '0'],
      ['feat/x', '12'],
    ]);
  });

  /** Pipes in a sentence are not a table; without the divider row it stays a paragraph. */
  it('does not mistake a line of pipes for a table', () => {
    const [block] = parseMarkdown('| this is | just text |');

    expect(block.kind).toBe('paragraph');
  });

  it('reads a quote across its lines', () => {
    const [block] = parseMarkdown('> first\n> second');

    expect(block.kind).toBe('quote');
    expect(text(block)).toBe('first second');
  });

  it('reads a horizontal rule rather than a one-item list', () => {
    expect(parseMarkdown('---')[0]).toEqual({ kind: 'rule' });
    expect(parseMarkdown('***')[0]).toEqual({ kind: 'rule' });
  });

  it('stops a paragraph where the next construct starts', () => {
    const blocks = parseMarkdown('intro line\n- a bullet');

    expect(blocks.map((block) => block.kind)).toEqual(['paragraph', 'list']);
  });

  it('reads an empty answer as no blocks at all', () => {
    expect(parseMarkdown('')).toEqual([]);
    expect(parseMarkdown('   \n\n  ')).toEqual([]);
  });

  it('reads a whole answer in order', () => {
    const blocks = parseMarkdown(
      '## Verdict\n\nTwo branches can go.\n\n- `feat/a` — merged\n- `feat/b` — stale\n\n> Check with the author first.',
    );

    expect(blocks.map((block) => block.kind)).toEqual(['heading', 'paragraph', 'list', 'quote']);
  });
});

function text(block: MarkdownBlock): string {
  return 'spans' in block ? flatten(block.spans) : '';
}

function flatten(spans: MarkdownSpans): string {
  return spans.map((span) => span.text).join('');
}
