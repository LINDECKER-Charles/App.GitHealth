# Exploitation du socle GitHealth

## Versions verrouillées

| Outil                |     Version |
| -------------------- | ----------: |
| SDK .NET             |    10.0.400 |
| Runtime ASP.NET Core |     10.0.11 |
| Node.js              | 24.20.0 LTS |
| npm                  |     11.19.0 |

## Publication native

Le script de publication produit trois distributions autonomes :

| Système | Architecture | RID | Point d'entrée |
|---|---|---|---|
| Windows | x64 | `win-x64` | `githealth.exe` |
| macOS | Intel | `osx-x64` | `githealth` |
| macOS | Apple Silicon | `osx-arm64` | `githealth` |

Depuis la racine du dépôt, PowerShell publie les trois cibles :

```powershell
./eng/Publish-Native.ps1
```

Une seule cible et un répertoire de sortie peuvent aussi être précisés :

```powershell
./eng/Publish-Native.ps1 `
  -RuntimeIdentifier win-x64 `
  -OutputRoot artifacts/publish
```

Chaque publication est autonome, non élaguée et accompagnée du bundle Angular.
Le script vérifie l'exécutable et `wwwroot/index.html`, puis crée :

- `artifacts/publish/githealth-win-x64.zip` ;
- `artifacts/publish/githealth-osx-x64.tar.gz` ;
- `artifacts/publish/githealth-osx-arm64.tar.gz`.

Les archives ne sont pas des exécutables monofichiers : les extraire entièrement
et conserver leurs fichiers ensemble. Le lanceur fixe sa racine de contenu au
dossier de l'exécutable ; il peut donc être appelé depuis n'importe quel répertoire
courant. Les artefacts macOS du MVP ne sont ni signés ni notariés.

Exemples de lancement :

```powershell
D:\Applications\GitHealth\githealth.exe `
  --repo D:\Dev\MonDepot `
  --data-dir D:\Donnees\GitHealth
```

```shell
/Applications/GitHealth/githealth \
  --repo "$HOME/Dev/MonDepot" \
  --data-dir "$HOME/Library/Application Support/GitHealth"
```

Les chemins relatifs fournis en option sont, eux, résolus depuis le répertoire
courant. Employer des chemins absolus évite donc toute ambiguïté.

### Options du lanceur

| Option | Valeur par défaut | Effet |
|---|---|---|
| `--repo <chemin>` | vide | préremplit le dépôt proposé sur l'accueil |
| `--port <1-65535>` | port disponible | impose un port précis sur l'interface loopback |
| `--data-dir <chemin>` | répertoire système | déplace la base et son verrou d'instance |
| `--no-browser` | navigateur ouvert | n'ouvre pas le navigateur au démarrage |
| `--help`, `-h` | — | affiche l'aide puis quitte |

Les formes `--repo=...`, `--port=...` et `--data-dir=...` sont également
acceptées. Sans `--port`, le système attribue un port disponible. Dans tous les
cas, le lanceur natif écoute exclusivement sur `127.0.0.1`.

### Répertoires de données

Sans `--data-dir` ni configuration explicite, `githealth.db` est créé dans :

| Système | Répertoire par défaut |
|---|---|
| Windows | `%LOCALAPPDATA%\GitHealth` |
| macOS | `$HOME/Library/Application Support/GitHealth` |
| Linux | `$XDG_DATA_HOME/GitHealth` ou `$HOME/.local/share/GitHealth` |

Sous Windows, `%USERPROFILE%\AppData\Local\GitHealth` sert de repli si le dossier
local d'application n'est pas fourni par le système. Sous Linux, `XDG_DATA_HOME`
n'est utilisé que s'il contient un chemin absolu.

Le paramètre `--data-dir` est prioritaire sur `GitHealth__DataDirectory`. Un
`Persistence__DatabasePath` explicite reste utilisable lorsqu'aucun répertoire de
données n'est imposé.

### Diagnostics de démarrage

Le lanceur termine avec le code `1` et un message exploitable lorsqu'un argument
est invalide, qu'un port demandé est déjà utilisé, que le répertoire de données
est inaccessible ou que SQLite ne peut pas ouvrir la base. Un fichier
`githealth.db.instance.lock` réserve la base pendant toute la vie du processus :
une seconde instance visant la même base échoue clairement, sans lancer de
migration ni écrire dans SQLite.

Si Git est absent ou inutilisable, l'application reste accessible mais `/health`
signale l'indisponibilité et en décrit la cause ; installer Git puis relancer
rétablit les analyses.

Le smoke test natif exerce le point d'entrée publié, l'interface, `/health`, le
préremplissage `--repo`, la création de la base, puis les diagnostics de conflit de
port et de base :

```powershell
./tests/Infrastructure/Invoke-NativeSmokeTest.ps1 `
  -PublishDirectory artifacts/publish/win-x64
```

## Docker Compose

Copier `.env.example` vers `.env`, puis renseigner la racine contenant les dépôts
à rendre visibles. Ce chemin est monté dans `/repositories` en lecture seule
(`:ro`) et le système de fichiers du conteneur est lui aussi en lecture seule.
Sur Windows, utiliser des barres obliques : `D:/Dev/Repos`. Le port hôte reste
`8080` par défaut ; `GITHEALTH_HTTP_PORT` permet d’en choisir un autre si ce port
est déjà réservé, sans changer l’écoute limitée à `127.0.0.1`.

La valeur `.` de l'exemple monte la racine du dépôt GitHealth lui-même. Pour une
utilisation normale, la remplacer par le chemin absolu du dossier de dépôts.

```shell
docker compose up --build
```

L’application est disponible uniquement sur `http://127.0.0.1:8080`. Le volume
nommé `githealth-data` conserve `/data` lors de la recréation du conteneur.

Pour vérifier la persistance sans supprimer le volume :

```shell
docker compose exec githealth touch /data/persistence-check
docker compose up --detach --force-recreate
docker compose exec githealth test -f /data/persistence-check
```

Ne pas exécuter `docker compose down --volumes` si les données doivent être
conservées.

## Sécurité du montage Git

Le conteneur s’exécute avec l’utilisateur non privilégié de l’image ASP.NET. Sa
configuration Git autorise uniquement `/repositories` et ses descendants comme
répertoires sûrs ; elle n’utilise jamais le joker global `safe.directory=*`.

## Analyse Git en lecture seule

Git est détecté par le diagnostic `/health`. Chaque commande est lancée sans shell,
avec un délai, une sortie bornée et l'annulation de tout l'arbre de processus. Le
scanner fixe `GIT_OPTIONAL_LOCKS=0`, `GIT_NO_LAZY_FETCH=1` et
`GIT_TERMINAL_PROMPT=0` : il ne fait ni checkout, ni fetch, ni écriture de ref.

Le calcul groupé utilise l'atome `ahead-behind` lorsqu'il est disponible. Une
installation Git plus ancienne passe automatiquement par `rev-list` avec une
concurrence bornée. Les comparaisons utilisent toujours les identifiants capturés
au début du scan, même si une branche bouge ensuite.

## Persistance SQLite

La migration EF Core est appliquée au démarrage. En mode natif, la base se trouve
dans le répertoire système décrit plus haut. Compose fixe explicitement
`Persistence__DatabasePath=/data/githealth.db` afin que le fichier reste dans le
volume `githealth-data`.

Les options disponibles sont :

| Configuration | Défaut | Effet |
|---|---:|---|
| `Persistence__DatabasePath` | `<données>/githealth.db` | chemin du fichier SQLite |
| `Persistence__WriteTimeoutSeconds` | `5` | attente maximale d'un verrou d'écriture |
| `Persistence__RetentionDays` | vide | ancienneté des analyses à supprimer |

La rétention est désactivée lorsque sa valeur est vide. Lorsqu'elle est activée,
elle ne supprime jamais le dernier snapshot réussi d'un projet. Les clés étrangères
sont actives, le journal utilise WAL et chaque analyse terminée est persistée avec
ses branches et contributeurs dans une transaction unique. Une analyse interrompue
ou échouée ne remplace donc pas le dernier résultat réussi.

L'export utilise l'API de sauvegarde SQLite pendant que l'application reste active,
puis normalise la copie en journal `DELETE`. Le fichier exporté est autonome : il
peut être archivé ou restauré sans fichier `-wal` ni `-shm`. Avant une restauration
manuelle, arrêter GitHealth, conserver une copie de la base courante, remplacer le
fichier configuré par l'export, puis redémarrer afin d'appliquer les migrations
éventuelles. La sauvegarde se télécharge avec `GET /api/exports/database`. Le nom
de fichier inclut un horodatage UTC et la réponse est une base SQLite autonome.

## API locale et analyses

Les routes sous `/api` exposent la validation et la configuration des projets,
la file d'analyses, leur progression, les snapshots paginés et leur détail. Une
route API inconnue renvoie toujours un Problem Details JSON ; elle n'est jamais
absorbée par le fallback de l'application Angular.

`AnalysisQueue__Capacity` limite le nombre d'analyses en attente (32 par défaut,
1 024 maximum). Un projet ne peut avoir qu'une analyse active et un lancement
accepté renvoie `202 Accepted` avec l'URL de suivi dans l'en-tête `Location`.

## Parcours web et mode d'exécution

`GET /api/runtime` indique à l'interface si GitHealth s'exécute en mode natif ou
Docker. En conteneur, la racine configurée est affichée et l'explorateur de
dossiers est désactivé : seuls les chemins déjà montés sous cette racine sont
acceptés.

En mode natif, `GET /api/runtime/directories` alimente l'explorateur local. Il ne
retourne que les dossiers accessibles, triés et limités à 250 éléments par niveau ;
il ne lit ni ne renvoie le contenu des fichiers. Les erreurs d'accès deviennent
des Problem Details et aucune trace technique n'est exposée au navigateur.

Le tableau de bord interroge l'état d'une analyse par polling, limite chaque page
à 50 branches et conserve le dernier snapshot réussi pendant un nouveau scan ou
après un échec. Recherche, relation Git, tri et ordre sont reflétés dans l'URL.

## Politiques, historique et export CSV

La politique d'un projet se modifie avec `PUT /api/projects/{id}/policy`. Cette
opération ne relance pas Git et ne modifie aucun fait capturé : le dernier snapshot
est seulement reclassé avec les seuils et motifs courants. L'aperçu
`POST /api/projects/{id}/policy/preview` applique les mêmes règles sans les
enregistrer et indique, branche par branche, la raison d'une exclusion ou d'une
protection.

Les pages historiques sous `/api/analyses/{id}/branches` et le détail d'un
snapshot conservent au contraire la politique capturée pendant l'analyse.
`GET /api/projects/{id}/analyses` restitue ce reçu de configuration avec chaque
exécution, y compris celles qui ont échoué.

L'export `GET /api/projects/{id}/analyses/latest/branches.csv` applique exactement
les filtres et l'ordre de la vue courante, sans pagination. Il est encodé en UTF-8
et neutralise les cellules qui pourraient être interprétées comme des formules
par un tableur. Il reste distinct de la sauvegarde SQLite, destinée à restaurer
l'application complète.

## Intégration continue

Le workflow `.github/workflows/ci.yml` s’exécute sur chaque pull request. Il
restaure et compile .NET, exécute les tests .NET et Angular, publie l’application
intégrée, contrôle la présence du bundle dans `wwwroot`, valide Compose et analyse
le Dockerfile avec BuildKit.

Le workflow `.github/workflows/release.yml` s'exécute manuellement ou pour un tag
`v*`. Sa matrice publie et teste `win-x64` sur `windows-latest`, `osx-x64` sur
`macos-15-intel` et `osx-arm64` sur `macos-15`. Les archives sont chargées comme
artefacts du workflow. Un job Ubuntu séparé construit l'image et exécute le smoke
test Docker : interface disponible, Git installé, UID non privilégié, montage des
dépôts non inscriptible et volume SQLite persistant après recréation.
