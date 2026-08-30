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
