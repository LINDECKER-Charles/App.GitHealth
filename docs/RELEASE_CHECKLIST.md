# Checklist de release candidate

Version préparée : `0.1.0-rc.1`.

## Vérifications automatiques

- [x] `dotnet format App.GitHealth.sln --verify-no-changes`
- [x] `dotnet build App.GitHealth.sln --configuration Release`
- [x] `dotnet test App.GitHealth.sln --configuration Release --no-build`
- [x] `npm run format:check --prefix src/App.GitHealth.Web`
- [x] `npm run test:ci --prefix src/App.GitHealth.Web`
- [x] benchmark de 100, 500 et 1 000 branches conforme aux budgets versionnés
- [x] parcours Playwright ajout → analyse → détail → export → redémarrage
- [x] relocalisation : historique, identité du dépôt et exclusion d'un scan concurrent
- [x] reprise au démarrage des analyses interrompues avec un statut terminal
- [x] audit NuGet et npm sans vulnérabilité haute ou critique
- [ ] analyse CodeQL sans alerte bloquante, au push sur dépôt public ou sous licence

## Matrice de distribution

- [x] Windows x64 : publication et smoke test natif
- [ ] macOS Intel : publication et smoke test natif
- [ ] macOS Apple Silicon : publication et smoke test natif
- [ ] Docker : démarrage, utilisateur non privilégié et persistance après recréation
- [x] Docker Compose : configuration statique et confinement validés
- [ ] sommes SHA-256 publiées pour toutes les archives
- [ ] SBOM CycloneDX/SPDX et attestation de provenance associés aux artefacts

## Recette sur dépôts réels

Pour chacun des deux dépôts retenus, consigner le commit du dépôt testé sans publier son
contenu, son nombre de branches et la durée totale :

- [x] comparer un échantillon à `git rev-list --left-right --count` ;
- [x] comparer `git for-each-ref` avant et après ;
- [x] comparer l'index, le diff du worktree et les reflogs avant et après ;
- [x] vérifier une branche fusionnée, une divergente et une inactive ;
- [x] exporter CSV et SQLite ;
- [x] redémarrer et retrouver le dernier snapshot réussi.

Les dépôts d'entreprise ne sont jamais copiés dans le dépôt GitHealth. Le rapport de
recette ne doit contenir ni nom d'auteur, ni adresse, ni chemin local sensible.

Commande reproductible, après avoir remplacé les deux chemins par les dépôts retenus :

```powershell
dotnet publish src/App.GitHealth.Api/App.GitHealth.Api.csproj `
  --configuration Release --output artifacts/acceptance-app

./tests/Infrastructure/Invoke-RealRepositoryAcceptance.ps1 `
  -RepositoryPath @("D:\Repos\volumineux-1", "D:\Repos\volumineux-2") `
  -PublishDirectory artifacts/acceptance-app `
  -ReportPath docs/release/acceptance-0.1.0-rc.1.json
```

Le script compare jusqu'à cinq branches de chaque dépôt avec `git rev-list`, exige des cas
fusionnés, divergents et inactifs, parcourt toute la pagination, exporte CSV et SQLite,
redémarre l'application et vérifie le snapshot restauré. Il compare enfin références,
reflogs, index et diff du worktree, puis anonymise les dépôts dans le rapport versionné.

## Décision de publication

- [x] limites connues relues dans `docs/KNOWN_LIMITATIONS.md`
- [x] audit de sécurité relu dans `SECURITY_AUDIT.md`
- [x] notes de version relues
- [ ] tag annoté `v0.1.0-rc.1` créé depuis un commit vert
- [ ] artefacts téléchargés depuis GitHub Actions et empreintes vérifiées
