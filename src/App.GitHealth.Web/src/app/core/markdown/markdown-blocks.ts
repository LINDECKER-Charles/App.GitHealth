import { parseSpans } from './markdown-inline';
import { MarkdownBlock, MarkdownBlockMatch, MarkdownSpans } from './markdown.models';

const fencePattern = /^\s*```\s*(\S*)\s*$/;
const headingPattern = /^(#{1,6})\s+(.*)$/;
const rulePattern = /^\s*([-*_])(?:\s*\1){2,}\s*$/;
const unorderedPattern = /^\s*[-*+]\s+(.*)$/;
const orderedPattern = /^\s*\d+[.)]\s+(.*)$/;
const quotePattern = /^\s*>\s?(.*)$/;
const tableRowPattern = /^\s*\|(.*)\|\s*$/;
const tableDividerPattern = /^\s*\|[\s:|-]+\|\s*$/;

type BlockReader = (lines: readonly string[], start: number) => MarkdownBlockMatch | null;

/**
 * Reads a Markdown answer into blocks. Each reader owns one construct and says where it
 * stopped, so the loop stays flat and a new construct is one function rather than one more
 * branch in a growing conditional. Order matters: a rule would otherwise read as a list.
 */
const readers: readonly BlockReader[] = [
  readFence,
  readHeading,
  readRule,
  readTable,
  readQuote,
  readList,
];

export function parseMarkdown(text: string): readonly MarkdownBlock[] {
  const lines = text.replace(/\r\n?/g, '\n').split('\n');
  const blocks: MarkdownBlock[] = [];
  let index = 0;
  while (index < lines.length) {
    if (lines[index].trim().length === 0) {
      index += 1;
      continue;
    }

    const match = readBlock(lines, index);
    blocks.push(match.block);
    index = match.next;
  }

  return blocks;
}

function readBlock(lines: readonly string[], start: number): MarkdownBlockMatch {
  for (const reader of readers) {
    const match = reader(lines, start);
    if (match !== null) {
      return match;
    }
  }

  return readParagraph(lines, start);
}

/** An unterminated fence still yields a code block: a truncated answer must stay readable. */
function readFence(lines: readonly string[], start: number): MarkdownBlockMatch | null {
  const opening = fencePattern.exec(lines[start]);
  if (opening === null) {
    return null;
  }

  let index = start + 1;
  while (index < lines.length && !fencePattern.test(lines[index])) {
    index += 1;
  }

  return {
    block: {
      kind: 'code',
      language: opening[1].length > 0 ? opening[1] : 'text',
      code: lines.slice(start + 1, index).join('\n'),
    },
    next: Math.min(index + 1, lines.length),
  };
}

function readHeading(lines: readonly string[], start: number): MarkdownBlockMatch | null {
  const match = headingPattern.exec(lines[start]);
  return match === null
    ? null
    : {
        block: { kind: 'heading', level: match[1].length, spans: parseSpans(match[2].trim()) },
        next: start + 1,
      };
}

function readRule(lines: readonly string[], start: number): MarkdownBlockMatch | null {
  return rulePattern.test(lines[start]) ? { block: { kind: 'rule' }, next: start + 1 } : null;
}

/** A table needs its divider row: without it the pipes are just characters in a sentence. */
function readTable(lines: readonly string[], start: number): MarkdownBlockMatch | null {
  const hasDivider = start + 1 < lines.length && tableDividerPattern.test(lines[start + 1]);
  if (!tableRowPattern.test(lines[start]) || !hasDivider) {
    return null;
  }

  let index = start + 2;
  while (index < lines.length && tableRowPattern.test(lines[index])) {
    index += 1;
  }

  return {
    block: {
      kind: 'table',
      header: readCells(lines[start]),
      rows: lines.slice(start + 2, index).map(readCells),
    },
    next: index,
  };
}

function readCells(line: string): readonly MarkdownSpans[] {
  const inner = tableRowPattern.exec(line);
  return (inner === null ? line : inner[1]).split('|').map((cell) => parseSpans(cell.trim()));
}

function readQuote(lines: readonly string[], start: number): MarkdownBlockMatch | null {
  if (!quotePattern.test(lines[start])) {
    return null;
  }

  const collected = collect(lines, start, quotePattern);
  return {
    block: { kind: 'quote', spans: parseSpans(collected.values.join(' ')) },
    next: collected.next,
  };
}

function readList(lines: readonly string[], start: number): MarkdownBlockMatch | null {
  const ordered = orderedPattern.test(lines[start]);
  if (!ordered && !unorderedPattern.test(lines[start])) {
    return null;
  }

  const collected = collect(lines, start, ordered ? orderedPattern : unorderedPattern);
  return {
    block: { kind: 'list', ordered, items: collected.values.map(parseSpans) },
    next: collected.next,
  };
}

/**
 * Everything else. Consecutive lines join into one paragraph, as Markdown reads them, and
 * stop as soon as another construct begins rather than swallowing it.
 */
function readParagraph(lines: readonly string[], start: number): MarkdownBlockMatch {
  let index = start + 1;
  while (index < lines.length && isPlainLine(lines, index)) {
    index += 1;
  }

  return {
    block: { kind: 'paragraph', spans: parseSpans(lines.slice(start, index).join(' ').trim()) },
    next: index,
  };
}

function isPlainLine(lines: readonly string[], index: number): boolean {
  return lines[index].trim().length > 0 && readers.every((reader) => reader(lines, index) === null);
}

/** Consecutive lines matching one pattern, reduced to their captured content. */
function collect(
  lines: readonly string[],
  start: number,
  pattern: RegExp,
): { readonly values: readonly string[]; readonly next: number } {
  const values: string[] = [];
  let index = start;
  while (index < lines.length) {
    const match = pattern.exec(lines[index]);
    if (match === null) {
      break;
    }

    values.push(match[1].trim());
    index += 1;
  }

  return { values, next: index };
}
