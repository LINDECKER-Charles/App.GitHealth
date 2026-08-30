# Release candidate checklist

Version being prepared: `0.1.0-rc.1`.

## Automated checks

- [x] `dotnet format App.GitHealth.sln --verify-no-changes`
- [x] `dotnet build App.GitHealth.sln --configuration Release`
- [x] `dotnet test App.GitHealth.sln --configuration Release --no-build`
- [x] `npm run format:check --prefix src/App.GitHealth.Web`
- [x] `npm run test:ci --prefix src/App.GitHealth.Web`
- [x] 100, 500 and 1,000 branch benchmark within the versioned budgets
- [x] Playwright journey: add → analyse → detail → export → restart
- [x] relocation: history, repository identity and exclusion of a concurrent scan
- [x] interrupted analyses resumed at startup with a terminal status
- [x] NuGet and npm audits with no high or critical vulnerability
- [ ] CodeQL analysis with no blocking alert, on push to a public or licensed repository

## Distribution matrix

- [x] Windows x64: publication and native smoke test
- [ ] macOS Intel: publication and native smoke test
- [ ] macOS Apple Silicon: publication and native smoke test
- [ ] Docker: startup, unprivileged user and persistence after recreation
- [x] Docker Compose: static configuration and confinement validated
- [ ] SHA-256 checksums published for every archive
- [ ] CycloneDX/SPDX SBOM and provenance attestation attached to the artefacts

## Acceptance testing on real repositories

For each of the two selected repositories, record the commit of the repository under test
without publishing its content, its branch count and the total duration:

- [x] compare a sample against `git rev-list --left-right --count`;
- [x] compare `git for-each-ref` before and after;
- [x] compare the index, the worktree diff and the reflogs before and after;
- [x] check a merged branch, a diverged one and an inactive one;
- [x] export CSV and SQLite;
- [x] restart and find the last successful snapshot again.

Company repositories are never copied into the GitHealth repository. The acceptance report
must contain no author name, no address and no sensitive local path.

Reproducible command, once both paths have been replaced with the selected repositories:

```powershell
dotnet publish src/App.GitHealth.Api/App.GitHealth.Api.csproj `
  --configuration Release --output artifacts/acceptance-app

./tests/Infrastructure/Invoke-RealRepositoryAcceptance.ps1 `
  -RepositoryPath @("D:\Repos\large-1", "D:\Repos\large-2") `
  -PublishDirectory artifacts/acceptance-app `
  -ReportPath docs/release/acceptance-0.1.0-rc.1.json
```

The script compares up to five branches of each repository with `git rev-list`, requires
merged, diverged and inactive cases, walks the whole pagination, exports CSV and SQLite,
restarts the application and checks the restored snapshot. It finally compares references,
reflogs, index and worktree diff, then anonymises the repositories in the versioned
report.

## Release decision

- [x] known limitations reviewed in `docs/KNOWN_LIMITATIONS.md`
- [x] security audit reviewed in `SECURITY_AUDIT.md`
- [x] release notes reviewed
- [ ] annotated tag `v0.1.0-rc.1` created from a green commit and pushed
- [ ] GitHub release drafted on that tag, "pre-release" box ticked
- [ ] release published — publishing triggers `release.yml`, which attaches the archives,
      checksums, SBOMs, installers and manifests once the matrix is green
- [ ] artefacts downloaded from the release and their checksums verified
