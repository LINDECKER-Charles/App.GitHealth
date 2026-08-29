# Modèle de sécurité

## Objectif et frontière de confiance

GitHealth aide un utilisateur local à examiner des dépôts potentiellement non fiables.
Il ne doit ni modifier ces dépôts, ni exposer leur contenu ou les identités d'auteur hors
du poste. L'API et l'interface appartiennent au même processus et à la même origine.

Le navigateur, le processus GitHealth et les commandes Git enfant s'exécutent avec les
droits du compte courant. Une application malveillante possédant déjà ces mêmes droits
peut lire les fichiers accessibles au compte et n'est donc pas arrêtée par GitHealth.

## Actifs protégés

- références, objets, index, worktree et reflogs des dépôts analysés ;
- noms et adresses des auteurs présents dans l'historique ;
- base SQLite, politiques et snapshots ;
- capacité de calcul de la machine locale ;
- intégrité des archives distribuées.

## Entrées non fiables

- chemins de dépôt et liens symboliques ;
- noms de références, auteurs, messages et configuration Git du dépôt ;
- sorties et durée des processus Git ;
- requêtes HTTP émises par un autre site ou un processus local ;
- configuration du lanceur, variables d'environnement et montage Docker.

## Contrôles HTTP

Le lanceur natif et Compose écoutent uniquement sur loopback. Toute requête applicative
doit conserver un `Host` loopback. Les routes `/api` refusent une origine étrangère et
un contexte `Sec-Fetch-Site` intersite.

Une navigation HTML, ou le bootstrap `GET /api/session` utilisé par le serveur Angular
de développement, crée une session aléatoire en mémoire et un couple de jetons
anti-forgery. Les cookies de session et d'anti-forgery sont `HttpOnly`, `SameSite=Strict`
et `Secure` sous HTTPS. Angular lit seulement le cookie `XSRF-TOKEN` dédié et le renvoie
dans `X-XSRF-TOKEN` pour les requêtes de modification. Une session sans activité expire
après douze heures.

Toutes les réponses reçoivent une politique CSP limitée à la même origine, ainsi que
`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`
et une `Permissions-Policy` restrictive. OpenAPI n'est publié qu'en développement.
`/health` reste volontairement public sur loopback pour les smoke tests.

Ces protections ne transforment pas GitHealth en service réseau. Il ne faut pas ajouter
une écoute LAN, un proxy inverse ou des origines non loopback sans concevoir une vraie
authentification et un stockage de session distribué.

## Isolation de Git

Les commandes sont lancées directement avec `ProcessStartInfo.ArgumentList`, sans shell
et avec l'entrée standard fermée. Les valeurs issues des dépôts restent des arguments
séparés, y compris lorsqu'elles commencent par un tiret ou contiennent des caractères de
commande.

Chaque processus possède :

- un délai de 30 secondes par défaut, configurable seulement entre 1 et 120 secondes ;
- un budget partagé de sortie de 4 Mio par défaut, borné entre 1 Kio et 16 Mio ;
- une concurrence de quatre commandes par défaut, bornée entre une et huit ;
- une annulation de tout l'arbre de processus en cas de dépassement ou d'arrêt.

GitHealth neutralise les helpers d'identification, les protocoles, la maintenance, le
ramasse-miettes, les variables `GIT_TRACE*` et les principales variables `GIT_*` capables
de rediriger les objets, l'index, le worktree, SSH ou la configuration globale.
`GIT_OPTIONAL_LOCKS=0`,
`GIT_NO_LAZY_FETCH=1` et `GIT_TERMINAL_PROMPT=0` maintiennent le scan non interactif et
en lecture seule.

## Chemins et conteneur

En mode natif, l'utilisateur choisit les dépôts accessibles à son propre compte. En
Docker, le chemin canonique, le worktree, le répertoire Git, son `commondir` et toutes
les object databases, y compris les alternates imbriqués, doivent rester physiquement
sous `/repositories`. Les composants de liens symboliques sont résolus avant ce contrôle.

Compose monte `/repositories` en lecture seule, exécute le processus avec un UID non
privilégié, rend le système de fichiers du conteneur non inscriptible et réserve seulement
`/data` et `/tmp`. L'option `no-new-privileges` est active.

La base, ses fichiers WAL/SHM et son verrou d'instance sont créés avec des permissions
privées lorsque le système le permet. Un répertoire de données créé par GitHealth est
privé ; les permissions d'un répertoire parent préexistant ne sont jamais modifiées. Une
sauvegarde SQLite est toujours demandée explicitement par l'utilisateur.

## Confidentialité et communications sortantes

Le code applicatif ne crée aucun client HTTP sortant, ne contient aucun SDK de
télémétrie et n'intègre aucune ressource web tierce. La CSP limite aussi les connexions
du navigateur à la même origine. Le scénario Playwright échoue s'il observe une requête
HTTP vers un hôte autre que loopback.

Les noms et adresses d'auteur sont conservés dans SQLite et peuvent apparaître dans le
CSV ou la sauvegarde demandés par l'utilisateur. Ces fichiers doivent être protégés comme
des données professionnelles.

## Chaîne de livraison

La CI compile et teste .NET, Angular et le parcours E2E. Un workflow séparé exécute
CodeQL, l'examen des dépendances et les audits NuGet/npm. Dependabot suit les actions,
paquets NuGet, paquets npm et images Docker.

La publication génère une somme SHA-256 et un SBOM SPDX. Pour un dépôt public, ou un dépôt
privé disposant de GitHub Enterprise Cloud et explicitement configuré, elle ajoute des
attestations GitHub de provenance et de SBOM. Ces éléments permettent de contrôler
l'archive, mais ne remplacent pas la signature de code ou la notarisation macOS.

## Risques résiduels

- un logiciel du même utilisateur peut accéder aux dépôts et à SQLite sans passer par
  l'API ;
- une vulnérabilité de Git ou du runtime reste exploitable avant sa mise à jour ;
- les archives macOS de la release candidate ne sont ni signées ni notariées ;
- un export copié hors du poste échappe aux contrôles de GitHealth ;
- les références locales peuvent être obsolètes, car aucun `fetch` n'est automatique.
