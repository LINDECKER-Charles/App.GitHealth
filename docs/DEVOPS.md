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

## Intégration continue

Le workflow `.github/workflows/ci.yml` s’exécute sur chaque pull request. Il
restaure et compile .NET, exécute les tests .NET et Angular, publie l’application
intégrée, contrôle la présence du bundle dans `wwwroot`, valide Compose et analyse
le Dockerfile avec BuildKit.
