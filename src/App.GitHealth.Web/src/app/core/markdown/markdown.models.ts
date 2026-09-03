/**
 * The shape a Markdown answer is reduced to before it is displayed.
 *
 * It is a tree of values, not a string of HTML, and that is the whole point: the text comes
 * from a language model, so it is never trusted enough to be injected. Angular renders these
 * nodes through ordinary bindings, which escape their content by construction.
 *
 * The grammar covered is what an agent actually writes — headings, emphasis, code, lists,
 * quotes, tables, rules and links. Nested inline formatting is deliberately not: it buys
 * almost nothing on this screen and costs a real parser.
 */

export interface MarkdownTextSpan {
  readonly kind: 'text';
  readonly text: string;
}

export interface MarkdownStrongSpan {
  readonly kind: 'strong';
  readonly text: string;
}

export interface MarkdownEmphasisSpan {
  readonly kind: 'emphasis';
  readonly text: string;
}

export interface MarkdownCodeSpan {
  readonly kind: 'code';
  readonly text: string;
}

export interface MarkdownLinkSpan {
  readonly kind: 'link';
  readonly text: string;
  /** Already restricted to a browsable scheme; anything else stays plain text. */
  readonly href: string;
}

export type MarkdownSpan =
  | MarkdownTextSpan
  | MarkdownStrongSpan
  | MarkdownEmphasisSpan
  | MarkdownCodeSpan
  | MarkdownLinkSpan;

export type MarkdownSpans = readonly MarkdownSpan[];

export interface MarkdownHeadingBlock {
  readonly kind: 'heading';
  /** 1 to 6, as written. */
  readonly level: number;
  readonly spans: MarkdownSpans;
}

export interface MarkdownParagraphBlock {
  readonly kind: 'paragraph';
  readonly spans: MarkdownSpans;
}

export interface MarkdownCodeBlock {
  readonly kind: 'code';
  readonly language: string;
  readonly code: string;
}

export interface MarkdownListBlock {
  readonly kind: 'list';
  readonly ordered: boolean;
  readonly items: readonly MarkdownSpans[];
}

export interface MarkdownQuoteBlock {
  readonly kind: 'quote';
  readonly spans: MarkdownSpans;
}

export interface MarkdownTableBlock {
  readonly kind: 'table';
  readonly header: readonly MarkdownSpans[];
  readonly rows: readonly (readonly MarkdownSpans[])[];
}

export interface MarkdownRuleBlock {
  readonly kind: 'rule';
}

export type MarkdownBlock =
  | MarkdownHeadingBlock
  | MarkdownParagraphBlock
  | MarkdownCodeBlock
  | MarkdownListBlock
  | MarkdownQuoteBlock
  | MarkdownTableBlock
  | MarkdownRuleBlock;

/** One block recognised, and the line the reader stopped on. */
export interface MarkdownBlockMatch {
  readonly block: MarkdownBlock;
  readonly next: number;
}
