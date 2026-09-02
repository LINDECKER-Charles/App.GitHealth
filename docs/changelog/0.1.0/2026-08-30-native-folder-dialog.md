# Choosing a folder through the system dialog

- **Type** — `feat`
- **Scope** — `front`, `api`
- **Landed** — 2026-08-30
- **Commits** — `a3ac9cb`

## What shipped

In window mode, "Browse" opens the platform's own folder picker and the chosen path comes
back into the field. A message bridge carries the request from the interface to the host,
which opens the dialog on the window's thread. The service detects the shell and falls back
to the HTML folder browser when there is none, so the browser and Docker paths are
unchanged.

## Why

Once the application runs in a window, the HTML folder browser is visibly worse than the
one the system already offers: it cannot show network locations, recent folders or the
places the user has bookmarked. Detecting the shell rather than configuring the mode keeps
one build behaving correctly in all three environments.
