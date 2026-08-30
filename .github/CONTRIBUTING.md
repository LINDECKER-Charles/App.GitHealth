# Contribuer à GitHealth

Merci de l'intérêt porté au projet. Ce document décrit comment signaler un problème,
préparer son environnement, écrire du code conforme aux conventions du dépôt et proposer
une pull request.

La participation au projet implique le respect du [code de conduite](CODE_OF_CONDUCT.md).

## Licence des contributions

GitHealth est distribué sous [licence MIT](../LICENSE). En proposant une contribution, vous
acceptez qu'elle soit publiée sous cette même licence et vous confirmez avoir le droit de
la soumettre. Il n'y a pas de CLA à signer.

N'intégrez pas de code, de police, d'icône ou de texte dont la licence est inconnue ou
incompatible avec MIT. Tout ajout de dépendance ou de ressource tierce doit être déclaré
dans [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

## Avant d'ouvrir une issue

Commencez par vérifier :

1. le [guide utilisateur](../docs/USER_GUIDE.md), qui décrit le comportement attendu ;
2. le [dépannage](../docs/TROUBLESHOOTING.md) ;
3. les [limites connues](../docs/KNOWN_LIMITATIONS.md) — plusieurs comportements
   surprenants sont des conséquences assumées de la sémantique Git ;
4. les issues existantes, ouvertes comme fermées.

Puis choisissez le bon canal :

| Situation | Canal |
| --- | --- |
| Bug reproductible | issue **Rapport de bug** |
| Idée ou besoin nouveau | issue **Proposition de fonctionnalité** |
| Documentation fausse ou incomplète | issue **Documentation** |
| Question d'usage | voir [SUPPORT.md](SUPPORT.md) |
| **Faille de sécurité** | **jamais une issue publique** — voir [SECURITY.md](SECURITY.md) |

Ne joignez jamais à une issue publique un chemin de dépôt d'entreprise, un nom de
branche interne, une adresse d'auteur ou un extrait de base SQLite. Anonymisez avant de
publier.

## Périmètre du projet

GitHealth **observe** un dépôt, il ne le modifie jamais. Les contributions suivantes
seront refusées, quelle que soit leur qualité :

- supprimer, fusionner, faire un checkout ou pousser une branche ;
- lancer automatiquement `git fetch` ou `git remote prune` ;
- cloner un dépôt distant ou gérer des identifiants ;
- transmettre des chemins, des noms de branches ou des identités d'auteur à un service
  externe ;
- transformer le produit en application multi-utilisateur exposée sur un réseau.

Le périmètre complet et ses raisons sont décrits dans [ARCHITECTURE.md](../docs/ARCHITECTURE.md).
Une évolution qui touche à ces frontières se discute dans une issue **avant** d'écrire du
code.

## Préparer l'environnement

Les versions sont verrouillées par `global.json` et `.nvmrc` ; les respecter évite des
échecs de CI difficiles à diagnostiquer.

| Outil | Version | Source |
| --- | --- | --- |
| SDK .NET | 10.0.400 | `global.json` |
| Node.js | 24.20.0 LTS | `.nvmrc` |
| npm | 11.19.0 | `packageManager` |
| Git | 2.38 ou plus récent | prérequis d'exécution |
| PowerShell 7 | pour les scripts `eng/` et `tests/Infrastructure/` | facultatif |
| Docker | pour la vérification Compose | facultatif |

Restauration des dépendances depuis la racine du dépôt :

```shell
dotnet restore App.GitHealth.sln
npm ci --prefix src/App.GitHealth.Web
npm ci --prefix tests/App.GitHealth.E2E
```

## Boucle de développement

Deux terminaux suffisent pour travailler sur l'interface avec l'API en direct.

```shell
# terminal 1 — API sur http://localhost:5115
dotnet run --project src/App.GitHealth.Api
```

```shell
# terminal 2 — interface Angular sur http://localhost:4200
npm start --prefix src/App.GitHealth.Web
```

Le serveur de développement Angular relaie `/api`, `/health` et `/openapi` vers l'API via
`proxy.conf.json`. En développement, l'origine `http://localhost:4200` est explicitement
autorisée par `LocalSecurity:AllowedOrigins` ; en production, l'interface et l'API
partagent la même origine.

Pour reproduire l'application telle qu'elle est livrée — bundle Angular servi depuis
`wwwroot` — publier puis lancer le résultat :

```shell
dotnet publish src/App.GitHealth.Api/App.GitHealth.Api.csproj \
  --configuration Release --output artifacts/publish
./artifacts/publish/githealth --repo "$HOME/Dev/MonDepot"
```

Plusieurs comportements ne se manifestent que dans ce mode intégré, en particulier ceux
liés à la politique de sécurité du contenu et aux adresses profondes. Une correction qui
touche au service des fichiers statiques, à la CSP ou au routage doit être vérifiée ainsi.

## Conventions de code

Les conventions complètes vivent dans [AGENTS.md](../AGENTS.md). L'essentiel :

- **DRY, KISS, SOLID** comme défauts ; s'en écarter demande une raison explicite ;
- **un seul élément public par fichier**, nommé comme le fichier ;
- **pas de nombre ni de chaîne magique** — une constante nommée dit l'intention ;
- **guard clauses** plutôt que `if/else` imbriqués ;
- **CQS** — une fonction modifie l'état ou retourne une valeur, jamais les deux ;
- nommage C# idiomatique : `PascalCase` pour les types et membres, `camelCase` pour les
  locales et paramètres, `I` devant les interfaces, `_camelCase` pour les champs privés ;
- booléens préfixés par `Is`, `Has`, `Should`, `Can`.

Limites vérifiables, à respecter :

| Règle | Limite |
| --- | --- |
| Taille d'un fichier | ≤ 300 lignes (alerte), 400 maximum |
| Fichiers par dossier | ≤ 10 |
| Taille d'une fonction | ≤ 30 lignes |
| Nombre de paramètres | ≤ 3 |
| Profondeur d'imbrication | ≤ 3 niveaux |
| Complexité cyclomatique | ≤ 10 par fonction |
| Longueur de ligne | ≤ 100 caractères |

Le dépôt compile avec `TreatWarningsAsErrors` et `EnforceCodeStyleInBuild` : un
avertissement est un échec de build, pas un détail à traiter plus tard.

Le formatage n'est pas négociable et n'a pas à être discuté en revue — il est appliqué
par outil :

```shell
dotnet format App.GitHealth.sln
(cd src/App.GitHealth.Web && npx prettier --write .)
(cd tests/App.GitHealth.E2E && npx prettier --write .)
```

## Tests

**Toute fonctionnalité s'accompagne de ses tests**, dans la même branche et la même pull
request. Le principe est de couvrir le comportement nominal et les cas limites que la
fonctionnalité introduit — ni plus, ni moins. On ne teste ni le framework, ni les
bibliothèques tierces, et on ne court pas après un pourcentage de couverture.

Un bon test échoue quand le **comportement** casse, pas quand l'implémentation change.

| Suite | Emplacement | Rôle |
| --- | --- | --- |
| Domaine | `tests/App.GitHealth.Core.Tests` | règles de classement, politiques, calculs |
| API | `tests/App.GitHealth.Api.Tests` | points d'entrée HTTP, file d'analyse, sécurité |
| Git | `tests/App.GitHealth.Git.IntegrationTests` | lecture réelle de dépôts fabriqués |
| Bout en bout | `tests/App.GitHealth.E2E` | parcours utilisateur sous Playwright |

Exécution :

```shell
dotnet test App.GitHealth.sln
npm run test:ci --prefix src/App.GitHealth.Web
```

Les tests bout en bout ont besoin d'une publication et de Chromium :

```shell
(cd tests/App.GitHealth.E2E && npx playwright install --with-deps chromium)
GITHEALTH_E2E_PUBLISH="$PWD/artifacts/publish" \
  npm run test:ci --prefix tests/App.GitHealth.E2E
```

Les tests d'intégration Git fabriquent leurs propres dépôts dans des dossiers temporaires
et isolent l'environnement Git. Ne les faites jamais pointer vers un dépôt réel de la
machine.

## Vérifier avant de pousser

Cette séquence reproduit ce que fait la CI. La passer localement évite l'aller-retour :

```shell
dotnet format App.GitHealth.sln --verify-no-changes
npm run format:check --prefix src/App.GitHealth.Web
npm run typecheck --prefix tests/App.GitHealth.E2E
npm run format:check --prefix tests/App.GitHealth.E2E
dotnet build App.GitHealth.sln --configuration Release
dotnet test App.GitHealth.sln --configuration Release --no-build
npm run test:ci --prefix src/App.GitHealth.Web
```

Si la contribution touche à Docker ou à Compose :

```shell
pwsh ./tests/Infrastructure/Assert-ComposeConfiguration.ps1
docker buildx build --check .
```

Le détail des workflows se trouve dans [docs/DEVOPS.md](../docs/DEVOPS.md).

## Branches

Une branche part de `main` et y revient par pull request. Une branche = un sujet.

```
type/description-courte
```

- **type** : `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `style`, `build`, `ci`,
  `chore` — toujours la forme courte, `feat/` et jamais `feature/` ;
- **description** : `kebab-case`, sans accent, deux à cinq mots qui disent l'objet de la
  branche.

Exemples : `feat/scan-dossier-parallele`, `fix/csp-base-uri`,
`docs/guide-utilisateur-politiques`.

## Commits

Le dépôt suit **Conventional Commits**, rédigés **en français** :

```
type(scope): description
```

- description à l'infinitif, minuscule initiale, sans point final ;
- sujet de **72 caractères maximum** ;
- corps facultatif, réservé au *pourquoi* ;
- une rupture de compatibilité se signale par un pied `BREAKING CHANGE:` ;
- **un commit = un changement cohérent** : ne mélangez jamais deux sujets.

Le scope est obligatoire dès que la carte ci-dessous couvre les fichiers modifiés :

| Chemin | Scope |
| --- | --- |
| `src/App.GitHealth.Api/**` | `api` |
| `src/App.GitHealth.Core/**` | `core` |
| `src/App.GitHealth.Web/**` | `front` |
| `.github/**` | `ci` |
| `docs/**` | `docs` |
| `docker*`, `compose*`, `deploy*`, `k8s*` | `infra` |
| config transverse à la racine | aucun scope, type `chore` |

Les tests voyagent avec le code qu'ils testent — ils prennent son scope, pas un scope
`test` séparé. L'entrée de changelog est jointe au commit de la fonctionnalité ou du
correctif qu'elle documente.

Exemple :

```
feat(front): ajouter la palette de commandes au clavier
fix(api): rejeter la relocalisation pendant une analyse en cours
docs(docs): documenter l'échelle réduite des branches fusionnées
```

## Changelog

Toute évolution visible par l'utilisateur — fonctionnalité, correctif, changement de
comportement, limite nouvelle — s'ajoute à la section `[Non publié]` de
[CHANGELOG.md](../CHANGELOG.md), sous `Ajouté`, `Modifié`, `Corrigé`, `Sécurité` ou
`Limites`.

L'entrée décrit ce que la personne qui utilise GitHealth constate, pas la mécanique
interne. Un refactoring sans effet observable ne produit pas d'entrée.

## Ouvrir une pull request

1. Créez la branche depuis `main`, en respectant le nommage ci-dessus.
2. Écrivez le code, ses tests et son entrée de changelog.
3. Passez la séquence de vérification locale.
4. Ouvrez la pull request vers `main` et remplissez le gabarit.
5. Une pull request en cours de travail s'ouvre en **brouillon**.

Une bonne pull request explique **pourquoi** le changement existe, ce qu'il change pour
la personne qui utilise l'outil, et comment le vérifier. Un lien vers l'issue d'origine
avec `Closes #123` clôt automatiquement celle-ci à la fusion.

Gardez les pull requests petites et centrées sur un sujet. Un renommage massif mélangé à
un correctif rend la revue impossible : séparez-les en deux.

## Revue et fusion

La CI doit être verte avant toute revue. Les points regardés en priorité :

- le comportement observé correspond-il à ce qui est annoncé ;
- les tests échouent-ils réellement sans le correctif ;
- les frontières du produit sont-elles respectées — aucune écriture Git, aucun accès
  réseau, aucune fuite d'identité d'auteur ;
- les conventions de taille, de nommage et de découpe sont-elles tenues ;
- la documentation et le changelog suivent-ils le code.

Les retours de revue portent sur le code, jamais sur la personne. Une question en revue
est une question, pas un reproche.
