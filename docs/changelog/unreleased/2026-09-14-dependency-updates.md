# Dependency refresh, with TypeScript 7 and Node 26 held back

- **Type** — `build`
- **Scope** — `front`, `api`, `infra`, `ci`, `eng`, `docs`
- **Landed** — 2026-09-14
- **Commits** — `6cc70e3`, `208ff1a`, `479a9e4`, `3cb25da`, `18e90ce`, `38ca5fe`, `ba11901`,
  `c132cee`, `75a6fa2`, `ebdd0e7`

## What shipped

The .NET servicing line moves to 10.0.12 in the packages and in the runtime image together.
`Microsoft.AspNetCore.OpenApi`, `Microsoft.EntityFrameworkCore.Design` and
`Microsoft.EntityFrameworkCore.Sqlite` in the API project, and `Microsoft.AspNetCore.Mvc.Testing`
in its tests, all go 10.0.11 → 10.0.12, and the Dockerfile's runtime stage goes
`dotnet/aspnet:10.0.11-noble` → `10.0.12-noble`. `Microsoft.NET.Test.Sdk` goes 18.9.0 → 18.10.0
across the three test projects, and the build stage goes `dotnet/sdk:10.0.400-noble` →
`10.0.401-noble`.

The interface stays inside Angular 22.1.x: `@angular/common`, `compiler`, `core`, `forms`,
`localize`, `platform-browser`, `router` and `compiler-cli` 22.1.4 → 22.1.5, `@angular/build`
and `@angular/cli` 22.1.6 → 22.1.7. `jsdom` goes 28.1.0 → 30.0.1. The end-to-end workspace
takes `@playwright/test` 1.62.1 → 1.63.0, `@types/node` 24.13.3 → 26.5.0 over two bumps, and
`prettier` 3.8.1 → 3.9.6. In CI, `anchore/sbom-action` goes v0.24.1 → v0.24.2.

`eng/build.sh` and `eng/build.cmd` change from mode 644 to 755, so `eng/README.md`'s
instruction to run `./eng/build.sh check` works as written instead of needing an interpreter
named in front of it.

The licence notices then catch up: the image table in `THIRD-PARTY-NOTICES.md` names
`sdk:10.0.401-noble` and `aspnet:10.0.12-noble`, and the pinned-versions table in
`docs/DEVOPS.md` names the 10.0.12 runtime.

## Why

**TypeScript 7 was refused for `src/App.GitHealth.Web`.** Every `@angular/build` on the 22.1
line declares `peerDependencies.typescript` as `>=6.0 <6.1` — checked on 22.1.6, 22.1.7, 22.1.8
and even on `22.2.0-next.7` — so `npm ci` cannot resolve a tree holding 7.x. `angular.json`
drives the unit tests through the `@angular/build:unit-test` builder, which peers `vitest`
`^4.0.8`, so a vitest 5 in the same proposal fails the same way. Dependabot cannot split a
group, so neither half could land; `jsdom` was taken out by hand and the rest closed.
`--legacy-peer-deps` would have made the run green while leaving the Angular compiler pointed
at a TypeScript it does not support, and a tree that resolves is not a tree that compiles what
it claims.

**`tests/App.GitHealth.E2E` has no Angular compiler in it**, and is already on
`typescript ~7.0.2`. The two npm workspaces therefore hold different majors of the same
dependency, deliberately. Recorded here because it otherwise reads as an oversight, and the
next grouped bump will propose closing it.

**The Node image stays at `24.20.0-alpine3.24`.** Node 24 is Active LTS until 2026-10-20; Node
26 only reaches LTS on 2026-10-28. Moving the production build image onto the Current line the
day a version is tagged buys nothing, and the proposed `26.8-alpine3.24` drops the patch
component this repository pins on purpose. The guard in
`tests/Infrastructure/Assert-ComposeConfiguration.ps1`, added in 0.1.0 so that no pull request
could move the Node image alone, is what stopped it. It did its job.

## Consequences

**The interface stays on `typescript ~6.0.2`** until an Angular release widens that peer past
`<6.1`. A grouped Dependabot proposal carrying TypeScript 7 should be closed, not merged and
not forced.

**The two npm workspaces hold different TypeScript majors.** That is the intended state, not
drift.

**The .NET stages have no guard.** The Dockerfile's `node` stage is checked against `.nvmrc`;
nothing checks the `sdk` and `aspnet` stages against `THIRD-PARTY-NOTICES.md` or the DEVOPS
pinned-versions table. A dotnet image bump goes green while both quietly go stale, which is why
those two files needed a hand edit this cycle. It is the same failure mode the 0.1.0 toolchain
entry closed for Node, and it is left open.
