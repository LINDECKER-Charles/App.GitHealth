import { MarkdownSpan, MarkdownSpans } from './markdown.models';

/**
 * One pass, left to right, over the constructs an agent actually uses. Code spans come
 * first in the alternation so their content stays literal, and `**` before `*` so a bold
 * run is never read as two emphases.
 */
const inlinePattern =
  /`([^`]+)`|\*\*([^*]+)\*\*|__([^_]+)__|\*([^*]+)\*|_([^_]+)_|\[([^\]\n]+)\]\(([^)\s]+)\)/g;

/** Schemes a rendered link may carry. Anything else stays text, visible but inert. */
const browsableScheme = /^(https?:\/\/|mailto:)/i;

/** Turns one line of Markdown into the spans that make it up. */
export function parseSpans(line: string): MarkdownSpans {
  const spans: MarkdownSpan[] = [];
  let cursor = 0;
  inlinePattern.lastIndex = 0;
  for (let match = inlinePattern.exec(line); match !== null; match = inlinePattern.exec(line)) {
    pushText(spans, line.slice(cursor, match.index));
    spans.push(...toSpans(match));
    cursor = match.index + match[0].length;
  }

  pushText(spans, line.slice(cursor));
  return spans.length > 0 ? spans : [{ kind: 'text', text: line }];
}

/**
 * A match yields one span, except a link whose target cannot be browsed: that one is
 * rendered as the text it was written as, rather than silently dropped or made clickable.
 */
function toSpans(match: RegExpExecArray): readonly MarkdownSpan[] {
  const code = group(match, 1);
  if (code !== undefined) {
    return [{ kind: 'code', text: code }];
  }

  const bold = group(match, 2) ?? group(match, 3);
  if (bold !== undefined) {
    return [{ kind: 'strong', text: bold }];
  }

  const italic = group(match, 4) ?? group(match, 5);
  if (italic !== undefined) {
    return [{ kind: 'emphasis', text: italic }];
  }

  return linkSpans(group(match, 6) ?? '', group(match, 7) ?? '');
}

/** A group that did not take part in the match is absent, which the array type hides. */
function group(match: RegExpExecArray, index: number): string | undefined {
  return match[index] as string | undefined;
}

function linkSpans(text: string, href: string): readonly MarkdownSpan[] {
  return browsableScheme.test(href)
    ? [{ kind: 'link', text, href }]
    : [{ kind: 'text', text: `[${text}](${href})` }];
}

function pushText(spans: MarkdownSpan[], text: string): void {
  if (text.length > 0) {
    spans.push({ kind: 'text', text });
  }
}
