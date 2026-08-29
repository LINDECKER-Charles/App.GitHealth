# App.GitHealth

GitHealth est une application web locale qui analyse les branches d'un dépôt Git
par rapport à une branche de référence. Elle aide à repérer les branches fusionnées,
inactives, divergentes ou probablement abandonnées sans modifier le dépôt analysé.

## Démarrage rapide

Les archives natives contiennent l'API, l'interface Angular et le runtime .NET. Après
avoir extrait toute l'archive correspondant à la machine, GitHealth se lance depuis
n'importe quel répertoire courant :

```powershell
# Windows x64
C:\Applications\GitHealth\githealth.exe --repo D:\Dev\MonDepot
```

```shell
# macOS Intel ou Apple Silicon
/Applications/GitHealth/githealth --repo "$HOME/Dev/MonDepot"
```

Le lanceur choisit un port disponible sur `127.0.0.1` et ouvre le navigateur. Les
options `--port`, `--data-dir` et `--no-browser` permettent de remplacer ces
valeurs ; `--help` affiche l'aide complète.

Pour exécuter GitHealth avec Docker, copier `.env.example` vers `.env`, renseigner
le dossier qui contient les dépôts, puis démarrer le service :

```shell
docker compose up --build
```

L'interface est alors disponible sur `http://127.0.0.1:8080`. Le dossier des dépôts
est monté dans le conteneur en lecture seule.

## Documentation

- [Architecture technique](ARCHITECTURE.md)
- [Plan d'implémentation](docs/IMPLEMENTATION_PLAN.md)
- [Installation, publication et exploitation](docs/DEVOPS.md)
