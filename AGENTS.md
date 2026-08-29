# App.GitHealth

Projet .NET / C#. Conventions B-Hive posées par `/b-hive-init` ; les conventions
transverses (format des commits, nommage des branches) vivent dans le
`AGENTS.md` global et sont posées par `/b-hive-dev-convention`.

## commit

Convention de commits (maintenue par /commit, initialisée par /b-hive-init).
- Style : Conventional Commits — langue : fr
- Scopes (chemin → scope) :
  - `src/App.GitHealth.Api/**` → `api`
  - `src/App.GitHealth.Core/**` → `core`
  - `src/App.GitHealth.Web/**` → `front`
  - `.github/**` → `ci`
  - `docs/**` → `docs`
  - `docker*`, `compose*`, `deploy*`, `k8s*`, `.gitlab-ci*` → `infra`
  - config transverse à la racine (`*.sln`, `Directory.*.props`, `.editorconfig`,
    `global.json`) → `chore` (sans scope)

Cette carte est prospective : le dépôt ne contient encore aucun code. Complète-la
au fil de l'apparition des projets — un projet `src/App.GitHealth.<Zone>` donne
le scope `<zone>` en minuscules.

Règles transverses : les tests (`tests/**`, `*Tests.cs`, `*.Tests/**`) voyagent
avec le code testé ; les entrées de changelog sont jointes au commit feature/fix
qu'elles documentent.

## Conventions de code

Ces conventions s'appliquent à tout le code du projet. Les **limites chiffrées**
sont des plafonds à respecter ; les **principes** sont des défauts à suivre, sauf
raison explicite et justifiée de s'en écarter.

### Principes directeurs

- **DRY (Don't Repeat Yourself)** — Pas de duplication de logique ni de
  connaissance métier : une règle vit à un seul endroit. (Nuance : n'abstrais pas
  avant la 3ᵉ répétition — une duplication ponctuelle vaut mieux qu'une mauvaise
  abstraction.)
- **KISS (Keep It Simple)** — Choisis la solution la plus simple qui résout
  réellement le problème. Pas de complexité ni de « cleverness » gratuites.
- **SOLID** :
  - **S — Responsabilité unique** : une classe ou un module n'a qu'une seule
    raison de changer.
  - **O — Ouvert/fermé** : ouvert à l'extension, fermé à la modification.
  - **L — Substitution de Liskov** : un sous-type doit pouvoir remplacer son type
    parent sans casser le comportement attendu.
  - **I — Ségrégation des interfaces** : préfère plusieurs interfaces ciblées à
    une interface fourre-tout.
  - **D — Inversion des dépendances** : dépends d'abstractions, pas
    d'implémentations concrètes.

### Limites de taille et de complexité (vérifiables)

| Règle | Limite |
|---|---|
| Taille d'un fichier | ≤ 300 lignes (alerte), 400 maximum |
| Fichiers par dossier | ≤ 10 (au-delà, découpe en sous-dossiers par domaine) |
| Taille d'une fonction / méthode | ≤ 30 lignes |
| Nombre de paramètres | ≤ 3 (au-delà, regroupe dans un objet / une struct) |
| Profondeur d'imbrication | ≤ 3 niveaux |
| Complexité cyclomatique | ≤ 10 par fonction |
| Longueur de ligne | ≤ 100 caractères |

- **Un seul élément public par fichier** (une classe, un composant ou un module
  par fichier), nommé comme le fichier.
- **Pas de nombres ni de chaînes magiques** — extrais-les dans des constantes
  nommées qui expliquent leur intention.

### Nommage

- **Noms explicites qui révèlent l'intention** — le nom dit *quoi* et *pourquoi*,
  pas *comment*. Un nom long et clair vaut mieux qu'un nom court et obscur.
- **Conventions cohérentes** sur tout le projet — casse idiomatique C#, sans
  jamais la mélanger : `PascalCase` pour les types, méthodes, propriétés et
  constantes ; `camelCase` pour les variables locales et les paramètres ;
  interfaces préfixées `I` (`IRepositoryScanner`) ; champs privés en `_camelCase`.
- **Pas d'abréviations cryptiques** — `userCount`, pas `usrCnt`. Seules les
  abréviations universelles sont tolérées (`id`, `url`, `http`).
- **Booléens préfixés** par `Is`, `Has`, `Should`, `Can`… (ex. `IsActive`,
  `HasAccess`, `ShouldRetry`).

### Fonctions

- **Une fonction = une seule chose** — si tu dois écrire « et » pour décrire ce
  qu'elle fait, découpe-la.
- **Privilégie les fonctions pures** — évite les effets de bord quand c'est
  possible, et rends-les explicites quand ils sont nécessaires.
- **Guard clauses / return early** — traite et sors tôt sur les cas limites au
  lieu d'imbriquer des `if/else`.
- **Évite les flag parameters** — un booléen qui change le comportement cache
  deux fonctions déguisées en une seule ; sépare-les.
- **CQS (Command Query Separation)** — une fonction *modifie* l'état OU
  *retourne* une valeur, jamais les deux.

## Tests

- **Toute feature s'accompagne de tests**, livrés avec elle (même branche, même
  PR).
- **Uniquement le nécessaire pour tester la feature** : le comportement nominal
  et les cas limites qu'elle introduit. Pas de course au pourcentage de
  couverture, pas de tests redondants ; on ne teste ni le framework ni les
  bibliothèques tierces.
- Un bon test échoue quand le **comportement** de la feature casse — pas quand
  son implémentation change.
