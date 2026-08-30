# Audit de sécurité — GitHealth `0.1.0-rc.1`

Date : 29 août 2026
Périmètre : API ASP.NET Core, scanner Git, Angular, SQLite, lanceurs, Docker Compose
et chaîne GitHub Actions de la branche `feat/livraison-mvp`.

## Synthèse

Aucune vulnérabilité critique ou haute n'a été identifiée dans le périmètre audité.
Le produit respecte son modèle local mono-utilisateur : écoute loopback, requêtes de
modification liées à une session anti-forgery, exécution Git sans shell, chemins Docker
confinés et absence de communication applicative sortante.

La release candidate peut être testée localement. Les risques principaux avant une
diffusion large concernent la signature des binaires, l'immuabilité des dépendances de
build et la confidentialité d'une base SQLite non chiffrée.

## Méthode et preuves

- revue manuelle des frontières HTTP, processus, chemins, persistance et exports ;
- recherche de secrets privés et de clients réseau sortants dans les sources ;
- audit NuGet transitif des sept projets : aucune vulnérabilité publiée ;
- `npm audit` des verrous Angular et Playwright : aucune vulnérabilité publiée ;
- 195 tests .NET, dont 14 scénarios de sécurité HTTP, réussis ;
- 43 tests Angular et build de production réussis ;
- scénario Playwright complet réussi sans hôte externe et sans mutation Git ;
- recette sur deux dépôts réels : métriques comparées à Git, exports, redémarrage,
  snapshots restaurés et fingerprints Git identiques ;
- configuration Compose statiquement validée ; smoke Docker dynamique non rejoué
  localement, car le moteur Docker Desktop était indisponible.

## Couverture OWASP

| Domaine | Contrôles observés | État |
|---|---|---|
| A01 Contrôle d'accès | loopback, `Host`, origine et Fetch Metadata | maîtrisé localement |
| A02 Cryptographie | jetons aléatoires ; SQLite non chiffrée | risque résiduel faible |
| A03 Injection | aucun shell, arguments séparés, EF Core, Angular, CSV neutralisé | maîtrisé |
| A04 Conception | frontières locales documentées, timeouts et files bornés | maîtrisé |
| A05 Configuration | CSP, headers, OpenAPI dev-only, Docker non privilégié | maîtrisé |
| A06 Composants | audits NuGet/npm, Dependabot, SBOM | maîtrisé à surveiller |
| A07 Authentification | session anti-forgery ; aucun compte | conforme au modèle local |
| A08 Intégrité | SHA-256, SBOM et provenance ; actions non épinglées par SHA | à renforcer |
| A09 Journalisation | erreurs Problem Details sans sortie Git brute | acceptable en local |
| A10 Requêtes serveur | aucun client HTTP sortant, protocoles Git distants bloqués | maîtrisé |

## Vulnérabilités critiques

Aucune vulnérabilité critique confirmée.

Les tests hostiles couvrent les arguments commençant par un tiret, la traversée de
chemins, les liens symboliques, les worktrees, les `gitdir`/`commondir` externes, les
alternates imbriqués, les environnements Git injectés, les dépassements de sortie, les
timeouts et l'annulation de l'arbre de processus.

## Vulnérabilités potentielles

### P1 — Course TOCTOU sur un chemin local — faible

Un processus exécuté par le même utilisateur peut remplacer un composant du chemin entre
sa validation physique et l'ouverture par Git. La revalidation juste avant l'analyse
réduit la fenêtre, mais ne l'élimine pas sans handles natifs ou sandbox par dépôt.

Impact : GitHealth pourrait lire un autre dépôt accessible au même compte. L'attaquant
possède déjà les droits de lecture correspondants ; aucune élévation de privilège n'a été
démontrée.

Action : conserver le test de revalidation et étudier des handles de répertoire natifs ou
une sandbox pour une version destinée à des dépôts réellement hostiles.

### P2 — Base SQLite non chiffrée — faible

La base contient noms et adresses d'auteur. Les permissions privées Unix et le répertoire
utilisateur limitent l'accès, mais le fichier et ses exports ne sont pas chiffrés au repos.

Impact : un autre processus disposant des droits du compte ou une sauvegarde mal protégée
peut lire ces données professionnelles.

Action : documenter la classification des exports, évaluer le chiffrement par le système
d'exploitation et prévoir une politique de purge adaptée aux organisations.

### P3 — Service local sans authentification utilisateur — faible dans le modèle

Tout processus local pouvant joindre loopback peut lire l'API avec un client non navigateur.
Les navigateurs étrangers sont bloqués par l'origine et Fetch Metadata ; les mutations
exigent en plus la session et le jeton anti-forgery.

Impact : le risque devient élevé si l'application est exposée sur un réseau ou placée
derrière un proxy. Cette configuration est explicitement hors périmètre du MVP.

Action : maintenir le refus des endpoints Kestrel configurés en mode natif et concevoir
une authentification complète avant toute exposition non loopback.

## Mauvaises pratiques

### M1 — Actions GitHub référencées par tag majeur — modérée

Plusieurs étapes utilisent `actions/*@vN` ou `github/codeql-action/*@v4`. Les tags majeurs
facilitent les correctifs, mais restent mutables et agrandissent le risque de chaîne
logicielle au sens OWASP A08.

Action : épingler les actions tierces sur un SHA vérifié et automatiser leurs mises à jour
avec Dependabot.

### M2 — Images Docker référencées par version, sans digest — faible

Les versions Node, SDK et runtime sont explicites, mais les tags de base ne sont pas liés
à un digest immuable.

Action : enregistrer les digests multi-architecture lors de la stabilisation, tout en
conservant un processus automatisé de mise à jour de sécurité.

### M3 — Binaires macOS non signés et non notariés — modérée

La limitation est documentée et n'altère pas le code exécuté en développement, mais elle
empêche l'utilisateur de vérifier l'identité du diffuseur avec les mécanismes natifs.

Action : signer les exécutables et notarier les archives avant une diffusion publique.

## Recommandations

### Avant la version stable

1. Signer Windows et macOS, puis notarier les artefacts macOS.
2. Épingler les actions et images de build par SHA ou digest contrôlé.
3. Exécuter les workflows CodeQL, dependency review, SBOM et provenance sur le tag RC.
4. Rejouer le smoke Docker sur un moteur actif et archiver la preuve CI.
5. Définir la rétention des données d'auteur et la protection attendue des exports.

### Défense continue

1. Traiter les alertes Dependabot et les audits de dépendances avant publication.
2. Conserver les tests d'origine, anti-forgery, chemins hostiles et non-mutation.
3. Examiner chaque nouvelle commande Git pour éviter réseau, hooks et écriture implicite.
4. Refuser toute écoute réseau future sans nouveau modèle de menace et authentification.
5. Refaire cet audit après une intégration de forge, un clone géré ou une mise à jour
   automatique, car ces fonctions changeraient fortement la frontière de confiance.

## Conclusion

Le niveau de sécurité est adapté à une release candidate locale et mono-utilisateur. Les
contrôles empêchent les classes d'attaque les plus pertinentes pour le MVP : CSRF web,
DNS rebinding simple, injection d'arguments Git, fuite réseau accidentelle, déni de
service non borné et sortie de racine Docker. La diffusion publique reste conditionnée
aux recommandations de chaîne de livraison et de signature ci-dessus.
