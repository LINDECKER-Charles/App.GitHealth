# Getting help

GitHealth is a free software project maintained on limited time. There is no commercial
support and no response-time commitment. This document says where to look for an answer and
how to ask a question so that it can be acted on.

## Start with the documentation

Most questions already have a written answer:

| Question | Document |
| --- | --- |
| How to install, launch and use GitHealth | [User guide](../docs/USER_GUIDE.md) |
| It does not start, the port is taken, Git is missing | [Troubleshooting](../docs/TROUBLESHOOTING.md) |
| Why is this result surprising | [Known limitations](../docs/KNOWN_LIMITATIONS.md) |
| How the measurements are computed | [Architecture](../docs/ARCHITECTURE.md) |
| What the application reads, writes and never sends | [Security](../docs/SECURITY_MODEL.md) |
| Native publishing, Docker, operations | [DevOps](../docs/DEVOPS.md) |
| What changed from one version to the next | [Changelog](../CHANGELOG.md) |

Many puzzling behaviours are not bugs but consequences of Git semantics: a merged branch
whose commit attribution becomes impossible, a remote branch frozen because GitHealth never
runs `fetch`, a contributor appearing twice for lack of a `.mailmap`. The known limitations
explain each of those cases.

## Pick the right channel

| You have | Channel |
| --- | --- |
| A usage question | **GitHub Discussions**, or an issue if discussions are closed |
| A reproducible bug | **Bug report** issue |
| A feature idea | **Feature request** issue |
| Wrong or incomplete documentation | **Documentation** issue |
| A security vulnerability | **Never a public issue** — [SECURITY.md](SECURITY.md) |
| The wish to contribute | [CONTRIBUTING.md](CONTRIBUTING.md) |

A security vulnerability is reported privately, through the repository's **Security
advisories**. That covers in particular any unintended Git write, any escape from the root
mounted under Docker, any cross-origin read and any transmission of data to the outside.

## What makes a request actionable

Without these elements, a question usually stays without a useful answer:

- the **version** of GitHealth — the name of the downloaded archive, the release tag, or
  the tag of the Docker image used;
- the **execution mode**: native Windows executable, native macOS, or Docker Compose;
- the **operating system** and its version, plus the output of `git --version`;
- what you **expected**, what you **got**, and the minimal steps to reproduce it;
- the console error messages, **anonymised**.

The shape of the repository often matters more than its size: standard repository, *bare*,
linked worktree, approximate number of branches, presence of a `.mailmap`.

## What must never be published

GitHealth handles data that identifies people and projects. In a public issue, never attach:

- a company or client repository path;
- internal branch names;
- author names or addresses taken from the history;
- an unredacted CSV export or SQLite backup.

Anonymise before publishing: `D:\Dev\ClientX\billing` becomes `D:\Dev\repository`,
`feature/JIRA-4210-payment-rework` becomes `feature/example`. If the problem is only
reproducible with real data, say so in the issue rather than attaching it — a private
exchange will be offered.

A minimal reproduction repository, built from empty commits, is worth more than any extract
of a real repository.

## Response times

There is no guaranteed response time. Priorities, in order:

1. security vulnerabilities, in particular those affecting the integrity of a repository or
   the confidentiality of author identities;
2. regressions compared to the previously published version;
3. computation bugs — a wrong recommendation or a wrong measurement;
4. everything else.

An issue without an answer is not a rejected issue. Following up after a few weeks is
legitimate.

## Out of scope

Some requests will be closed without being handled, not for lack of interest but because
they fall outside the product: deleting or merging branches, running `fetch`
automatically, cloning a remote repository, integrating with the GitHub, GitLab or Azure
DevOps APIs, or exposing a shared instance on a network. The reasons behind those
boundaries are documented in [ARCHITECTURE.md](../docs/ARCHITECTURE.md).
