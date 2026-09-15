# A local API another tool can read GitHealth through, and ask it for a scan

- **Type** — `feat`
- **Scope** — `api`, `front`, `docs`
- **Landed** — 2026-09-15
- **Commits** — `22a2c0e`, `a416354`

## What shipped

**Settings** — a new screen, reached by the gear in the top bar, at `/settings` — carries one
section: **Local API access**. It is closed on a fresh installation and opens on a switch.

Opening it binds a second HTTP listener on `127.0.0.1`, on a port the user chooses (1024 to
65535, `7823` by default). That listener serves `/v1`, and answers nothing without a bearer
token:

| Route | What it does |
| --- | --- |
| `GET /v1` | names the surface, the GitHealth version, and every route below |
| `GET /v1/projects` | the observed repositories |
| `GET /v1/projects/{id}` | one of them |
| `GET /v1/projects/{id}/branches` | branches of its latest capture, filtered and paged like the interface's own list |
| `GET /v1/projects/{id}/analyses` | its capture history |
| `POST /v1/projects/{id}/analyses` | **launches a scan** — every declared baseline, or the one named by `?baseline=` |
| `GET /v1/analyses/{id}` | how a run is going, and how it ended |
| `GET /v1/analyses/{id}/branches` | the branches one capture holds |

The payloads are the interface's own, unchanged, so a caller reads exactly what the screen
reads. The only rewrite is the `statusUrl` a launch answers with: it points into `/v1`,
because `/api` is not reachable from there.

The interface drives all of this through `GET`, `PUT /api/local-api` and
`POST /api/local-api/token`, which stay on the browser surface — behind the session cookie
and the anti-forgery token, like every other mutation. Opening a port is a decision taken in
front of the window, never by the orchestrator on the other side of it.

The token is shown **once**, when it is issued, with a copy button and a ready-to-paste
`curl` line. Afterwards the screen shows six characters of it and when it was issued.
Issuing another one revokes the previous one immediately, with no restart. The settings —
the switch, the port, the token's fingerprint — live in `local-api.json`, next to the
database in the data directory, and survive a restart: what was open when GitHealth was
closed is open again when it starts.

A port that cannot be taken is reported on the screen, with the system's own reason, and
changes nothing else: the setting is kept and the application stays up.

## Why

GitHealth measures branches on a schedule nobody wants to keep by hand. Everything it knows
was reachable only by clicking, so the tools that already run on that machine — an
orchestrator, a nightly job, a dashboard — could neither read a capture nor ask for one.

**The listener is a host of its own, not a route added to the interface's pipeline.** That
pipeline guards `/api` with a loopback host check, an origin check and, for a mutation, a
session cookie plus an anti-forgery token. A command-line caller carries none of the last
three. Relaxing the guard for one route would have relaxed it for the browser too — the same
reasoning that put `POST /agent-bridge/{token}` outside `/api`. Here the surface gets a
second `WebApplication` with its own Kestrel, its own port and a pipeline three steps long:
lend the application's services, weigh the token, route. `LocalApiHost` builds it on demand,
which is also what makes the port changeable without restarting GitHealth — Kestrel binds
its endpoints when a host starts, so a host that can be started is a port that can move.

That second host has a container of its own, as any host does, and nothing useful lives in
it. A middleware therefore replaces each request's `RequestServices` with a scope taken from
the application's provider: the repositories, the analysis queue and the database context
are the application's, and a request answered out of a second copy of them would read a
second, empty database. It is also why every handler resolves its services off
`HttpContext.RequestServices` rather than declaring them as parameters — minimal APIs decide
what is a service at build time, against the container the routes were mapped in.

**The token's fingerprint is kept, never the token.** `GET /api/exports/database` hands the
whole SQLite file over, and a copy of it travels; a live credential had no business being in
there. That is also why the settings are a file next to the database rather than a table
inside it. The file holds a SHA-256 of the token, compared in constant time, and is written
with private permissions in a directory that is already private. The cost is real and
accepted: a token that is lost is replaced, not recovered — the same bargain as a personal
access token anywhere else.

**Opening the access issues a token when there is none, and hands it over in the same
gesture.** The API refuses `isEnabled: true` without one — an open port with no secret in
front of it must not exist — and asking the user for two deliberate clicks to avoid that
would only have taught them to rush the first.

Host filtering, which the slim builder brings along, is switched off on that host on
purpose: it would have answered a foreign `Host` header with a bare 400 read out of
`appsettings.json`, so the rule lives in the surface's own guard instead, spelled the way
the browser surface spells it and answered with the same problem document. A name resolving
to `127.0.0.1` is how a web page would try to reach a listener it has no business reaching,
which is why that check runs before the token is even weighed.

Two shapes were rejected. Serving `/v1` on the interface's own port would have cost nothing
to build and would have made the access impossible to firewall, to move, or to close without
closing the window. Driving Kestrel's endpoints through configuration reload would have kept
one host, at the price of branching the whole pipeline on the port a request arrived on —
one mistake there and the automation routes answer on the browser's port, without a token.

## Consequences

- **A port is open on `127.0.0.1` as long as the access is on, and it reopens by itself at
  every start.** That is the feature, and it is off until someone turns it on. The security
  model's rule is unchanged: loopback only, never a LAN listener.
- **Anything already running as the same user can reach that port.** It still needs the
  token, which it can only get from the settings file — where only a fingerprint is — or
  from wherever the user pasted it. The same software could read the SQLite database
  directly, which is the residual risk this product already accepts.
- **A scan launched through the API is a scan.** It runs Git, it fills the queue and it
  writes a capture, exactly as the button does. A caller that polls it every minute will
  measure the repository every minute.
- **The token cannot be read back.** Losing it means issuing another one and updating
  whatever held it.
