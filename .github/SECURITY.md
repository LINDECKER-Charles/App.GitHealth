# Security policy

## Supported versions

Only the latest published `0.x` version receives security fixes. This policy will be
revised at the first stable version, `1.0.0`.

## Reporting a vulnerability

Do not open a public issue containing a proof of exploitation, a local path or author data.
Use the GitHub repository's **Security advisories** feature to create a private report. If
it is not available, contact the maintainer through the organisation's usual confidential
channel.

Include, where possible:

- the version and execution mode concerned;
- the preconditions and the minimal reproduction steps;
- the expected impact, without attaching a company repository;
- a suggested fix or regression test.

An acknowledgement is aimed for within three business days. The fix is prioritised
according to exploitability and impact on the repositories, the author identities and the
local database. Coordinated disclosure is agreed before anything is made public.

## Trust boundary

GitHealth is a local, single-user application. Deliberate network exposure, modifying the
binary, an already compromised system, or software running with the same rights as the user
fall outside the MVP threat model. Defects that nevertheless allow a Git write, a Docker
root escape, a cross-origin read or a silent exfiltration remain in scope.
