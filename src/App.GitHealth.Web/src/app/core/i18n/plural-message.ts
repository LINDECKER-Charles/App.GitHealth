import { sourceLocale } from './locale';

/**
 * One message per plural category the active locale needs. English only ever selects `one` and
 * `other`; a locale with more categories supplies the ones it uses and falls back to `other`.
 */
export type PluralMessages = Partial<Record<Intl.LDMLPluralRule, string>> & {
  readonly other: string;
};

const pluralRules = new Intl.PluralRules(sourceLocale);

/**
 * Picks the message matching the count's plural category. Callers pass already-translated
 * `$localize` messages, so each variant keeps its own stable message id rather than hiding
 * inside an ICU sub-message, which cannot carry one.
 */
export function pluralMessage(count: number, messages: PluralMessages): string {
  return messages[pluralRules.select(count)] ?? messages.other;
}
