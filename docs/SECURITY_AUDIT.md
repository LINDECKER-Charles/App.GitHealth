# Security audit — GitHealth `0.1.0-rc.1`

Date: 29 August 2026
Scope: the ASP.NET Core API, the Git scanner, Angular, SQLite, the launchers, Docker
Compose and the GitHub Actions pipeline of the `feat/livraison-mvp` branch.

## Summary

No critical or high vulnerability was identified within the audited scope. The product
honours its local single-user model: loopback listening, mutating requests tied to an
anti-forgery session, shell-free Git execution, confined Docker paths and no outbound
application communication.

The release candidate can be tested locally. The main risks before wide distribution
concern binary signing, the immutability of build dependencies and the confidentiality of
an unencrypted SQLite database.

## Method and evidence

- manual review of the HTTP, process, path, persistence and export boundaries;
- search for private secrets and outbound network clients in the sources;
- transitive NuGet audit of the seven projects: no published vulnerability;
- `npm audit` of the Angular and Playwright lockfiles: no published vulnerability;
- 195 .NET tests passing, including 14 HTTP security scenarios;
- 43 Angular tests and a successful production build;
- full Playwright scenario passing with no external host and no Git mutation;
- acceptance testing on two real repositories: metrics compared against Git, exports,
  restart, restored snapshots and identical Git fingerprints;
- Compose configuration validated statically; the dynamic Docker smoke test was not
  replayed locally, because the Docker Desktop engine was unavailable.

## OWASP coverage

| Area | Observed controls | State |
|---|---|---|
| A01 Access control | loopback, `Host`, origin and Fetch Metadata | controlled locally |
| A02 Cryptography | random tokens; unencrypted SQLite | low residual risk |
| A03 Injection | no shell, separate arguments, EF Core, Angular, neutralised CSV | controlled |
| A04 Design | documented local boundaries, bounded timeouts and queues | controlled |
| A05 Configuration | CSP, headers, dev-only OpenAPI, unprivileged Docker | controlled |
| A06 Components | NuGet/npm audits, Dependabot, SBOM | controlled, to monitor |
| A07 Authentication | anti-forgery session; no accounts | consistent with the local model |
| A08 Integrity | SHA-256, SBOM and provenance; actions not pinned by SHA | to reinforce |
| A09 Logging | Problem Details errors with no raw Git output | acceptable locally |
| A10 Server-side requests | no outbound HTTP client, remote Git protocols blocked | controlled |

## Critical vulnerabilities

No confirmed critical vulnerability.

The hostile tests cover arguments starting with a dash, path traversal, symbolic links,
worktrees, external `gitdir`/`commondir`, nested alternates, injected Git environments,
output overruns, timeouts and process-tree cancellation.

## Potential vulnerabilities

### P1 — TOCTOU race on a local path — low

A process running as the same user can replace a path component between its physical
validation and the moment Git opens it. Re-validating just before the analysis narrows
the window, but does not eliminate it without native handles or a per-repository sandbox.

Impact: GitHealth could read another repository reachable by the same account. The
attacker already holds the corresponding read rights; no privilege escalation was
demonstrated.

Action: keep the re-validation test, and investigate native directory handles or a sandbox
for a version intended for genuinely hostile repositories.

### P2 — Unencrypted SQLite database — low

The database contains author names and addresses. Private Unix permissions and the user
directory limit access, but neither the file nor its exports are encrypted at rest.

Impact: another process holding the account's rights, or a poorly protected backup, can
read that business data.

Action: document the classification of exports, evaluate operating-system encryption and
plan a purge policy suited to organisations.

### P3 — Local service without user authentication — low within the model

Any local process able to reach loopback can read the API with a non-browser client.
Foreign browsers are blocked by origin and Fetch Metadata; mutations additionally require
the session and the anti-forgery token.

Impact: the risk becomes high if the application is exposed on a network or placed behind
a proxy. That configuration is explicitly outside the MVP scope.

Action: keep rejecting Kestrel endpoints configured in native mode, and design full
authentication before any non-loopback exposure.

## Bad practices

### M1 — GitHub actions referenced by major tag — moderate

Several steps use `actions/*@vN` or `github/codeql-action/*@v4`. Major tags make patching
easier, but they stay mutable and widen the supply-chain risk in the sense of OWASP A08.

Action: pin third-party actions to a verified SHA and automate their updates with
Dependabot.

### M2 — Docker images referenced by version, without a digest — low

The Node, SDK and runtime versions are explicit, but the base tags are not bound to an
immutable digest.

Action: record the multi-architecture digests when stabilising, while keeping an automated
security update process.

### M3 — Unsigned and unnotarised macOS binaries — moderate

The limitation is documented and does not alter the code executed in development, but it
prevents the user from verifying the publisher's identity with native mechanisms.

Action: sign the executables and notarise the archives before any public distribution.

## Recommendations

### Before the stable version

1. Sign on Windows and macOS, then notarise the macOS artefacts.
2. Pin build actions and images by SHA or a controlled digest.
3. Run the CodeQL, dependency review, SBOM and provenance workflows on the RC tag.
4. Replay the Docker smoke test on a running engine and archive the CI evidence.
5. Define the retention of author data and the expected protection of exports.

### Continuous defence

1. Handle Dependabot alerts and dependency audits before releasing.
2. Keep the origin, anti-forgery, hostile-path and non-mutation tests.
3. Review every new Git command to rule out network access, hooks and implicit writes.
4. Refuse any future network listener without a new threat model and authentication.
5. Redo this audit after a forge integration, a managed clone or an automatic update, as
   these features would substantially change the trust boundary.

## Conclusion

The security level is appropriate for a local, single-user release candidate. The controls
prevent the attack classes most relevant to the MVP: web CSRF, simple DNS rebinding, Git
argument injection, accidental network leakage, unbounded denial of service and Docker
root escape. Public distribution remains conditional on the supply-chain and signing
recommendations above.
