# Pull request

## Pourquoi

<!-- Le problème résolu, pas la liste des fichiers touchés. Lien vers l'issue si elle
     existe : `Closes #123`. -->

## Ce qui change pour la personne qui utilise GitHealth

<!-- Ce qui devient visible, différent ou impossible. « Aucun changement observable »
     est une réponse valable pour un refactoring. -->

## Comment le vérifier

<!-- Le chemin exact à suivre pour constater le résultat : commande, écran, fichier
     de test qui échoue sans le correctif. -->

## Type de changement

- [ ] `feat` — nouvelle fonctionnalité
- [ ] `fix` — correction de bug
- [ ] `docs` — documentation uniquement
- [ ] `refactor` — remaniement sans changement de comportement
- [ ] `perf` — performance
- [ ] `test` — tests uniquement
- [ ] `build` / `ci` / `chore` — outillage et maintenance
- [ ] Rupture de compatibilité (`BREAKING CHANGE:` présent dans un commit)

## Vérifications

- [ ] Les commits suivent Conventional Commits en français, avec le scope du dépôt.
- [ ] La branche suit `type/description-courte` et ne traite qu'un seul sujet.
- [ ] La fonctionnalité ou le correctif est couvert par des tests, livrés ici.
- [ ] `dotnet format --verify-no-changes` et `prettier --check` passent.
- [ ] `dotnet build` en Release ne produit aucun avertissement.
- [ ] `dotnet test` et `npm run test:ci` passent localement.
- [ ] Le changement respecte les limites de taille, de nommage et de découpe du projet.
- [ ] `CHANGELOG.md` est à jour sous `[Non publié]`, si le changement est observable.
- [ ] La documentation concernée suit le code.

## Frontières du produit

- [ ] Aucune écriture Git : ni référence, ni index, ni worktree, ni reflog modifiés.
- [ ] Aucun accès réseau ajouté — pas de `fetch`, pas de CDN, pas de télémétrie.
- [ ] Aucune identité d'auteur, aucun chemin de dépôt transmis hors du processus local.
- [ ] Aucune ressource tierce ajoutée sans licence compatible MIT et sans entrée dans
      `THIRD-PARTY-NOTICES.md`.

<!-- Si l'une de ces cases ne peut pas être cochée, expliquez pourquoi ci-dessous. -->

## Complément

<!-- Captures d'écran pour un changement visuel, mesures pour un changement de
     performance, points d'attention pour la revue. -->
