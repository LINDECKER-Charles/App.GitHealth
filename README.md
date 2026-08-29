# GitHealth

> Version préparée : `0.1.0-rc.1`

GitHealth est une application web locale qui analyse les branches d'un dépôt Git
par rapport à une branche de référence. Elle aide à repérer les branches fusionnées,
inactives, divergentes ou probablement abandonnées sans modifier le dépôt analysé.

Les analyses restent sur la machine : GitHealth ne clone pas, ne lance aucun `fetch` et
ne transmet pas les identités d'auteur à un service externe.

## Fonctionnalités

- comparaison des branches locales ou de suivi distant avec une référence choisie ;
- avance, retard, fusion, activité et contributeurs normalisés par `.mailmap` ;
- politiques de protection et d'exclusion avec prévisualisation ;
- relocalisation d'un dépôt déplacé sans perdre ses analyses ;
- historique de snapshots, export CSV et sauvegarde SQLite ;
- distributions Windows, macOS et Docker Compose.

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

Git 2.38 ou plus récent est recommandé. Les archives natives embarquent le runtime .NET,
mais pas Git. La release candidate macOS n'est ni signée ni notariée.

Pour exécuter GitHealth avec Docker, copier `.env.example` vers `.env`, renseigner
le dossier qui contient les dépôts, puis démarrer le service :

```shell
docker compose up --build
```

L'interface est alors disponible sur `http://127.0.0.1:8080`. Le dossier des dépôts
est monté dans le conteneur en lecture seule.

## Premier diagnostic

1. Ajouter le chemin absolu d'un dépôt déjà présent.
2. Choisir la référence et le périmètre des branches.
3. Lancer l'analyse depuis le tableau de bord.
4. Ouvrir une branche pour expliquer ses métriques et ses contributeurs.
5. Exporter la vue en CSV ou sauvegarder toutes les données en SQLite.

Une analyse ne réalise aucune opération d'écriture Git. Les références, l'index, le
worktree et les reflogs ne sont pas modifiés.

## Documentation

- [Architecture technique](ARCHITECTURE.md)
- [Plan d'implémentation](docs/IMPLEMENTATION_PLAN.md)
- [Guide utilisateur](docs/USER_GUIDE.md)
- [Dépannage](docs/TROUBLESHOOTING.md)
- [Limites connues](docs/KNOWN_LIMITATIONS.md)
- [Installation, publication et exploitation](docs/DEVOPS.md)
- [Modèle de sécurité](docs/SECURITY_MODEL.md)
- [Benchmark](docs/BENCHMARKING.md)
- [Audit de sécurité](SECURITY_AUDIT.md)
- [Checklist de release](docs/RELEASE_CHECKLIST.md)

Les vulnérabilités ne doivent pas être publiées dans une issue publique. La procédure de
signalement responsable est décrite dans [SECURITY.md](SECURITY.md).
