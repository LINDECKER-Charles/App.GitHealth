/** Jeton coloré d'une ligne de commande, prêt à être rendu sans `innerHTML`. */
export interface ShellToken {
  readonly text: string;
  readonly className: string;
}

const grammar: readonly (readonly [string, RegExp])[] = [
  ['etb-tok-com', /#[^\n]*/],
  ['etb-tok-str', /'[^']*'|"[^"]*"/],
  ['etb-tok-kw', /\b(?:curl|npm|npx|pnpm|git|docker|make|cd|export|sudo|jq|openssl)\b/],
  ['etb-tok-fn', /--?[A-Za-z][\w-]*/],
  ['etb-tok-num', /\b\d+\b/],
];

const pattern = new RegExp(grammar.map(([, rule]) => `(${rule.source})`).join('|'), 'g');

/** Reprend la grammaire `bash` du design system : mot-clé, chaîne, option, nombre, commentaire. */
export function tokenizeShell(code: string): readonly ShellToken[] {
  const tokens: ShellToken[] = [];
  let cursor = 0;
  pattern.lastIndex = 0;

  for (let match = pattern.exec(code); match !== null; match = pattern.exec(code)) {
    if (match.index > cursor) {
      tokens.push({ text: code.slice(cursor, match.index), className: '' });
    }

    tokens.push({ text: match[0], className: classOf(match) });
    cursor = match.index + match[0].length;
  }

  if (cursor < code.length) {
    tokens.push({ text: code.slice(cursor), className: '' });
  }

  return tokens;
}

function classOf(match: RegExpExecArray): string {
  const index = grammar.findIndex((_, position) => match[position + 1] !== undefined);
  return index < 0 ? '' : grammar[index][0];
}
