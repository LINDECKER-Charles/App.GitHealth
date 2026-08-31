/** Locale every source string is authored in; mirrors `i18n.sourceLocale` in angular.json. */
export const sourceLocale = 'en-US';

/** Shape emitted by `ng extract-i18n --format=json`, consumable by `loadTranslations()`. */
export interface LocaleCatalog {
  readonly locale: string;
  readonly translations: Record<string, string>;
}

/** Locales shipping a runtime catalog. Only the source locale exists today. */
const supportedLocales: readonly string[] = [sourceLocale];

const storageKey = 'githealth.locale';

/**
 * Locale to boot with. Called before the Angular graph is imported, so it runs outside
 * dependency injection and reads the storage key directly.
 */
export function resolveLocale(): string {
  const stored = readStoredLocale();
  return stored !== null && supportedLocales.includes(stored) ? stored : sourceLocale;
}

/** Path of the catalog served for a locale, relative to the app's `<base href>`. */
export function catalogUrl(locale: string): string {
  return `locale/messages.${locale}.json`;
}

function readStoredLocale(): string | null {
  try {
    return globalThis.localStorage?.getItem(storageKey) ?? null;
  } catch {
    // A browser without persistent storage stays usable: the source locale applies.
    return null;
  }
}
