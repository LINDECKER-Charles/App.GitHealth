# Third-party notices

GitHealth is published under the [MIT license](LICENSE). It relies on third-party
components that remain subject to their own license and their own copyright. This document
lists the ones that are **redistributed** with the application, then the ones that are only
used to build or test it.

This page is a readable summary. The generated inventories are authoritative:

- `artifacts/publish/wwwroot/3rdpartylicenses.txt` — full license texts of the npm packages
  included in the Angular bundle, produced by the `production` configuration;
- `dotnet list App.GitHealth.sln package --include-transitive` — the exact NuGet dependency
  tree, transitive versions included;
- `npm ls --prefix src/App.GitHealth.Web` — the exact npm dependency tree.

## Redistributed with the application

### Assets embedded in the interface

| Component | License | Location |
| --- | --- | --- |
| IBM Plex Sans, IBM Plex Mono | SIL OFL 1.1 | `src/App.GitHealth.Web/public/ds/fonts/` |
| Lucide Icons | ISC | `src/App.GitHealth.Web/public/ds/icons/` |

- IBM Plex — © 2017 IBM Corp., reserved font name "Plex".
- Lucide Icons — © Lucide Icons and Contributors.

The license texts sit next to the files concerned — `fonts/NOTICE.txt` and
`icons/LICENSE-lucide.txt` — and are served with the application. Both files must follow
the fonts and the glyphs in any copy or redistribution: this is a requirement of both the
SIL OFL 1.1 and the ISC license.

Fonts and icons are served **locally**. GitHealth contacts no CDN, no foundry and no remote
service to load them.

### Runtime libraries

| Component | License |
| --- | --- |
| Angular (`@angular/*`) | MIT |
| RxJS | Apache-2.0 |
| tslib | 0BSD |
| ASP.NET Core and the .NET runtime | MIT |
| Entity Framework Core, `Microsoft.Data.Sqlite` | MIT |
| SQLitePCLRaw | Apache-2.0 |
| SQLite engine | public domain |
| `Microsoft.AspNetCore.OpenApi`, `Microsoft.OpenApi` | MIT |
| Photino.NET, Photino.Native | Apache-2.0 |
| Velopack | MIT |

The native Windows, macOS and Linux distributions embed the .NET runtime: the Microsoft
copyright notices ship with them in the published archive.

Photino provides the desktop shell. Its native library — `Photino.Native.dll` on Windows,
`.dylib` on macOS, `.so` on Linux — is redistributed next to the executable; Apache-2.0
requires its license text and its copyright notice to follow any copy or redistribution.
Photino embeds no rendering engine: it calls the system's own — WebView2, WKWebView or
WebKitGTK — installed and updated by the platform, outside the project's distribution
scope.

Velopack produces the installers and applies the updates. Its library and its `Setup` and
`Update` utilities are redistributed inside the published installers, not in the portable
archives.

## Required at runtime, not redistributed

| Component | License | Role |
| --- | --- | --- |
| Git | GPL-2.0 | invoked as an external process |

GitHealth **does not bundle Git** and does not derive from it: it runs the `git` binary
already installed on the machine, without a shell, and reads its output. No published
archive contains any Git code, which leaves GPL-2.0 outside the project's distribution
scope. Git must be installed separately — version 2.38 or newer recommended.

## Container images

The Docker image is built from base images published by their respective vendors and
subject to their own terms:

| Image | Role |
| --- | --- |
| `node:24.20.0-alpine3.24` | building the Angular bundle |
| `mcr.microsoft.com/dotnet/sdk:10.0.400-noble` | .NET compilation |
| `mcr.microsoft.com/dotnet/aspnet:10.0.11-noble` | runtime |

The runtime image installs `ca-certificates`, `curl` and `git` from the Ubuntu
repositories; those packages remain covered by their original licenses.

## Build and test tooling

These components are not redistributed with the application; they serve to build, verify
and measure it.

| Component | License |
| --- | --- |
| TypeScript | Apache-2.0 |
| Angular CLI, `@angular/build` | MIT |
| Vitest | MIT |
| jsdom | MIT |
| Prettier | MIT |
| Playwright | Apache-2.0 |
| xUnit.net | Apache-2.0 |
| `Microsoft.NET.Test.Sdk` | MIT |
| Coverlet | MIT |
| `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.Design` | MIT |
| GitHub actions used by the workflows | MIT |

## Adding a dependency

Every new dependency or third-party asset must:

1. carry a license compatible with MIT — permissive, without extended copyleft;
2. be added to the matching table in this document;
3. if it is redistributed, have its license text copied next to the files concerned, as for
   IBM Plex and Lucide.

A dependency under an unknown license, under strong copyleft (GPL, AGPL), or whose origin
cannot be verified, cannot be integrated. The terms are restated in
[CONTRIBUTING.md](.github/CONTRIBUTING.md).

## Reporting an attribution error

A missing or inaccurate attribution is reported through an ordinary public issue — it is
not a security vulnerability. State the component, its actual license and its upstream
source; the fix is handled as a priority.
