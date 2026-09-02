# A single process serving the API and the interface

- **Type** — `feat`
- **Scope** — `api`, `front`
- **Landed** — 2026-08-28
- **Commits** — `b8e1492`, `62b6b42`, `b7b4258`

## What shipped

The executable foundation: an ASP.NET Core host on .NET 10 (`App.GitHealth.Api`) with its
domain library (`App.GitHealth.Core`) and their test projects, a `/health` endpoint and an
OpenAPI description; an Angular 22 application in standalone mode, strict TypeScript and
SCSS, organised by feature with a development proxy; and the publish step that folds the
Angular bundle into the static files the API serves.

The SDK, Node and npm versions are pinned from this commit onwards (`global.json`,
`.nvmrc`), as are the shared compilation and static-analysis rules
(`Directory.Build.props`).

## Why

Opening the root URL must not require a second server. One process serving both the API
and the interface removes port negotiation, CORS and a supervision problem from the
product before they exist — which is what later makes a desktop window and a container two
packagings of the same artefact rather than two architectures.
