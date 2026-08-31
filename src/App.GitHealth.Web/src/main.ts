import {
  catalogUrl,
  resolveLocale,
  sourceLocale,
  type LocaleCatalog,
} from './app/core/i18n/locale';

/**
 * The application graph is imported dynamically, never statically, and that is load-bearing:
 * `$localize` memoises each tagged template the first time it is evaluated, and modules hold
 * translated constants at module scope. A static import would freeze the source locale before
 * any catalog could be loaded. Keep both imports below lazy.
 */
async function start(): Promise<void> {
  const locale = resolveLocale();
  if (locale !== sourceLocale) {
    const [{ loadTranslations }, catalog] = await Promise.all([
      import('@angular/localize'),
      fetchCatalog(locale),
    ]);
    loadTranslations(catalog.translations);
  }

  const { bootstrap } = await import('./bootstrap');
  await bootstrap();
}

async function fetchCatalog(locale: string): Promise<LocaleCatalog> {
  const response = await fetch(catalogUrl(locale));
  if (!response.ok) {
    throw new Error(`Unable to load the "${locale}" locale catalog (HTTP ${response.status}).`);
  }

  return (await response.json()) as LocaleCatalog;
}

void start().catch((error: unknown) => console.error(error));
