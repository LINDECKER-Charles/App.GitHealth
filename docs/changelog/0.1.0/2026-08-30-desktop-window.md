# GitHealth opens as a desktop application

- **Type** — `feat`, `docs`
- **Scope** — `api`, `docs`
- **Landed** — 2026-08-30
- **Commits** — `440465b`, `2aa9ee0`, `6b73878`, `efdd495`, `1067628`, `bd942cf`, `19802c6`, `df9e8df`

## What shipped

A double-click opens a native window. Photino embeds the system rendering engine in the
host process — WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux — so Kestrel and
the window share one process, with no child-process supervision and no port negotiation.
The window opens maximised: at a fixed size, the workspace's minimum width is not
guaranteed on a scaled display.

The launcher now distinguishes three intents: a window by default, `--no-window` to open
the system browser, and `--no-browser` meaning "no interface at all" — it implies
`--no-window` and serves headless runs. If no rendering engine is usable, the application
warns on `stderr` and falls back to the browser instead of stopping.

The executable carries a multi-resolution `.ico` derived from the design system's 512×512
source, embedded in the binary and handed to the installer for its shortcuts, and moves to
the windowed subsystem so a double-click no longer opens a console next to the window —
closing that console used to kill the application. Started from a terminal it reattaches to
the calling console, so `--help` stays readable, except when output is already redirected,
which leaves the smoke tests intact.

`DESKTOP_PLAN.md` records the decisions and the three gaps found while implementing them;
`ARCHITECTURE.md`, `DEVOPS.md`, `KNOWN_LIMITATIONS.md` and `THIRD-PARTY-NOTICES.md` follow —
Photino's native library is redistributed next to the executable and Apache-2.0 requires
its licence to travel with it.

## Why

The product is used by one person on their own machine, which is a desktop application's
shape, not a server's. Embedding the webview in the host process rather than spawning a
browser removes the two failure modes a local server has: a port already taken, and a
window closed while the process keeps running.

Failing back to the browser rather than exiting matters most on Linux, where WebKitGTK may
simply not be installed — the product stays usable instead of refusing to start.

## Consequences

`--no-browser` changes meaning: it now implies `--no-window` and guarantees no interface at
all. The native smoke test and the end-to-end runs pass that flag, since a window would
leave them waiting for a close event that never comes.
