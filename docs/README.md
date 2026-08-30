# GitHealth documentation hub

> The Git facts before the decision.

This hub points every reader to the document that answers their intent. The usage guides
start from a concrete action; the technical documents go all the way back to the
evidence, the limitations and the structural choices.

## Using GitHealth

| Need | Document | Expected outcome |
|---|---|---|
| Install and start the application | [README](../README.md#04--get-started-in-minutes) | A first local diagnosis |
| Add, scan and organise repositories | [User guide](USER_GUIDE.md) | A workspace ready to analyse |
| Understand a branch and its verdict | [User guide](USER_GUIDE.md#explaining-a-branch) | A decision tied back to the Git facts |
| Resolve an incident | [Troubleshooting](TROUBLESHOOTING.md) | A targeted diagnosis and a course of action |
| Check a limitation of the RC | [Known limitations](KNOWN_LIMITATIONS.md) | An explicit product expectation |

## Operating and distributing

| Need | Document | Associated evidence |
|---|---|---|
| Publish a native archive | [DevOps](DEVOPS.md) | SHA-256 checksums, SBOM and smoke tests |
| Deploy with Docker Compose | [DevOps](DEVOPS.md#docker-compose) | Read-only mount and hardened container |
| Prepare a release candidate | [Release checklist](RELEASE_CHECKLIST.md) | Qualification matrix |
| Review the first RC | [Report 0.1.0-rc.1](release/0.1.0-rc.1.md) | Versioned acceptance results |

## Understanding the system

| Angle | Document | Central question |
|---|---|---|
| Domain and flows | [Architecture](ARCHITECTURE.md) | How do facts become a verdict? |
| Trust boundary | [Security model](SECURITY_MODEL.md) | What does GitHealth read, and what does it refuse to do? |
| Independent code review | [Security audit](SECURITY_AUDIT.md) | Which risks remain open? |
| Performance | [Benchmarks](BENCHMARKING.md) | How do we detect a measurable regression? |
| Building the MVP | [Implementation plan](IMPLEMENTATION_PLAN.md) | How was the product sliced and qualified? |

## Contributing

- [Contribution guide](../.github/CONTRIBUTING.md) — environment, conventions, tests and PRs;
- [Code of conduct](../.github/CODE_OF_CONDUCT.md) — the collaboration framework;
- [Support](../.github/SUPPORT.md) — pick the right channel and provide a reproducible case;
- [Security policy](../.github/SECURITY.md) — report a vulnerability without exposing it;
- [Changelog](../CHANGELOG.md) — follow the capabilities that shipped;
- [Editorial and visual direction](ART_DIRECTION.md) — extend the identity without diluting it.

## Three reading paths

### I am discovering the product

1. [README](../README.md)
2. [User guide](USER_GUIDE.md)
3. [Known limitations](KNOWN_LIMITATIONS.md)

### I have to evaluate it technically

1. [Architecture](ARCHITECTURE.md)
2. [Security model](SECURITY_MODEL.md)
3. [Benchmarks](BENCHMARKING.md)
4. [Security audit](SECURITY_AUDIT.md)

### I am preparing a release

1. [DevOps](DEVOPS.md)
2. [Release checklist](RELEASE_CHECKLIST.md)
3. [Acceptance report 0.1.0-rc.1](release/0.1.0-rc.1.md)
4. [Changelog](../CHANGELOG.md)

---

Every sensitive claim must be traceable to a test, an architectural control, a
measurement or a documented limitation. If a page no longer allows that journey, it must
be updated at the same time as the behaviour it describes.
