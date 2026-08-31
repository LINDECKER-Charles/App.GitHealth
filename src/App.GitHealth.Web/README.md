# App.GitHealth.Web

GitHealth's Angular interface. The front end runs on a development server with a proxy to
the API, and its production build is published into `wwwroot` by the ASP.NET Core project.

## Prerequisites

- Node.js `24.20.0` (the version in the root `.nvmrc` file);
- npm `11.19.0`.

## Development

From this folder:

```shell
npm ci
npm start
```

The Angular server listens on `http://localhost:4200` and forwards `/api`, `/health` and
`/openapi` to the local API on `http://localhost:5115`.

## Checks

```shell
npm run test:ci
npm run build
```

The build is produced in `dist/app-git-health-web/browser`.

## Internationalisation

The app is wired for `@angular/localize`: `@angular/localize/init` is polyfilled by the build
target, `i18n.sourceLocale` is `en-US`, and `npm run i18n:extract` writes the message catalog to
`src/locale/messages.json`. No string is marked yet — the sweep lands once the source strings
are English.

**Never add the `localize` build option and never add an `i18n.locales` key to `angular.json`.**
Either one turns on compile-time inlining, which moves the output to
`dist/app-git-health-web/browser/<locale>/`. That breaks the `FrontendDist` path in
`src/App.GitHealth.Api/App.GitHealth.Api.csproj` and its `index.html` existence guard.

Locales are loaded at runtime instead: the build stays single-locale, and `src/main.ts` resolves
the locale, calls `loadTranslations()` from `@angular/localize`, and only then imports the
application graph. That order is mandatory — `$localize` memoises each tagged template on first
evaluation, so a static import of the graph would freeze the source locale. `src/bootstrap.ts`
exists solely to keep that import dynamic.
