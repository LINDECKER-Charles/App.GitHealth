# Exploitation du socle GitHealth

## Versions verrouillées

| Outil                |     Version |
| -------------------- | ----------: |
| SDK .NET             |    10.0.400 |
| Runtime ASP.NET Core |     10.0.11 |
| Node.js              | 24.20.0 LTS |
| npm                  |     11.19.0 |

## Publication native

Depuis la racine du dépôt :

```shell
dotnet publish src/App.GitHealth.Api/App.GitHealth.Api.csproj \
  --configuration Release \
  --output artifacts/publish
```

La publication exécute `npm ci`, construit Angular et copie son bundle dans
`artifacts/publish/wwwroot`. L’application publiée doit être lancée depuis son
répertoire afin que celui-ci soit utilisé comme racine de contenu :

```shell
cd artifacts/publish
dotnet App.GitHealth.Api.dll
```

## Docker Compose

Copier `.env.example` vers `.env`, puis renseigner la racine contenant les dépôts
à rendre visibles. Ce chemin est monté dans `/repositories` en lecture seule.
Sur Windows, utiliser des barres obliques : `D:/Dev/Repos`. Le port hôte reste
`8080` par défaut ; `GITHEALTH_HTTP_PORT` permet d’en choisir un autre si ce port
est déjà réservé, sans changer l’écoute limitée à `127.0.0.1`.

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
par défaut dans `data/githealth.db`, relativement à la racine de contenu. Compose
fixe explicitement `Persistence__DatabasePath=/data/githealth.db` afin que le
fichier reste dans le volume `githealth-data`.

Les options disponibles sont :

| Configuration | Défaut | Effet |
|---|---:|---|
| `Persistence__DatabasePath` | `data/githealth.db` | chemin du fichier SQLite |
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
éventuelles. L'endpoint HTTP de téléchargement sera branché à l'étape 5.

## Intégration continue

Le workflow `.github/workflows/ci.yml` s’exécute sur chaque pull request. Il
restaure et compile .NET, exécute les tests .NET et Angular, publie l’application
intégrée, contrôle la présence du bundle dans `wwwroot`, valide Compose et analyse
le Dockerfile avec BuildKit.
