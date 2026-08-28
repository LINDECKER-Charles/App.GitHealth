# Exploitation du socle GitHealth

## Versions verrouillées

| Outil | Version |
|---|---:|
| SDK .NET | 10.0.400 |
| Runtime ASP.NET Core | 10.0.11 |
| Node.js | 24.20.0 LTS |
| npm | 11.19.0 |

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
Sur Windows, utiliser des barres obliques : `D:/Dev/Repos`.

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
