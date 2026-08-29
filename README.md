<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)"
      srcset="docs/assets/readme/hero-dark.svg">
    <source media="(prefers-color-scheme: light)"
      srcset="docs/assets/readme/hero-light.svg">
    <img alt="GitHealth — les faits Git avant la décision"
      src="docs/assets/readme/hero-light.svg" width="100%">
  </picture>
</p>

<h1 align="center">GitHealth</h1>

<p align="center">
  <a href="https://github.com/LINDECKER-Charles/App.GitHealth/actions/workflows/ci.yml">
    <img alt="CI"
      src="https://github.com/LINDECKER-Charles/App.GitHealth/actions/workflows/ci.yml/badge.svg">
  </a>
  <a href="https://github.com/LINDECKER-Charles/App.GitHealth/actions/workflows/security.yml">
    <img alt="Sécurité"
      src="https://github.com/LINDECKER-Charles/App.GitHealth/actions/workflows/security.yml/badge.svg">
  </a>
  <a href="https://github.com/LINDECKER-Charles/App.GitHealth/releases/latest">
    <img alt="Version 0.1.0-rc.1"
      src="https://img.shields.io/badge/version-0.1.0--rc.1-a87b27">
  </a>
  <a href="LICENSE">
    <img alt="Licence MIT"
      src="https://img.shields.io/badge/licence-MIT-2434a6">
  </a>
</p>

<p align="center">
  <strong>Voyez quelles branches comptent encore — sans toucher au dépôt.</strong><br>
  Local par conception · explicable par défaut · Windows, macOS et Docker
</p>

<p align="center">
  <a href="https://github.com/LINDECKER-Charles/App.GitHealth/releases/latest">
    <strong>Télécharger GitHealth</strong>
  </a>
  &nbsp;·&nbsp;
  <a href="docs/USER_GUIDE.md">Guide utilisateur</a>
  &nbsp;·&nbsp;
  <a href="docs/README.md">Documentation</a>
  &nbsp;·&nbsp;
  <a href="ARCHITECTURE.md">Architecture</a>
</p>

---

GitHealth transforme un historique de branches difficile à lire en décisions argumentées.
Il compare les références déjà présentes sur la machine, mesure leur topologie et leur
activité, puis explique pourquoi une branche est à conserver, à examiner ou probablement
prête à être nettoyée.

Ce n'est ni un bot de suppression, ni une nouvelle forge. C'est un poste de diagnostic
local : les faits restent visibles, les politiques restent sous votre contrôle et toute
action Git reste entre vos mains.

> [!IMPORTANT]
> GitHealth n'exécute aucun `clone`, `fetch`, `pull`, `checkout`, `merge`, `push` ou
> suppression. Il ne modifie ni les références, ni l'index, ni le worktree, ni les reflogs.

## 01 — Un dépôt. Des faits. Un verdict lisible.

| Observer | Comprendre | Décider | Suivre |
|---|---|---|---|
| Avance, retard, ancêtre commun et dernier commit | Topologie, activité et contributeurs normalisés | Conserver, examiner ou nettoyer — avec la raison | Snapshots, historique, CSV et sauvegarde SQLite |

<picture>
  <source media="(prefers-color-scheme: dark)"
    srcset="docs/assets/readme/diagnostic-dark.jpg">
  <source media="(prefers-color-scheme: light)"
    srcset="docs/assets/readme/diagnostic-light.jpg">
  <img alt="GitHealth analysant un scénario local dans le tableau de diagnostic"
    src="docs/assets/readme/diagnostic-light.jpg" width="100%">
</picture>

<p align="center"><sub>Scénario local construit à partir du dépôt GitHealth</sub></p>

Chaque ligne conserve la chaîne de raisonnement :

```text
faits Git  →  topologie  →  activité  →  politique  →  recommandation + explication
```

Une branche divergente n'est donc pas simplement « rouge ». GitHealth montre son écart,
sa dernière activité, la règle appliquée et la raison qui justifie l'attention demandée.
Quand les faits ne suffisent pas, l'interface le dit au lieu d'inventer une certitude.

## 02 — Pensé pour un dépôt comme pour tout un workspace

- analyser les branches locales ou de suivi distant par rapport à la référence choisie ;
- scanner un dossier, détecter les dépôts qu'il contient et lancer plusieurs analyses
  en parallèle ;
- distinguer branches synchronisées, en avance, fusionnées, divergentes ou sans base
  commune ;
- repérer l'activité récente, vieillissante ou inactive avec des seuils configurables ;
- protéger ou exclure des branches par motifs, avec prévisualisation avant application ;
- expliquer une branche jusque dans sa fiche : commits propres, contributeurs et raison
  du verdict ;
- relocaliser un dépôt déplacé sans perdre son historique d'analyses ;
- filtrer, trier, comparer les snapshots et exporter la vue en CSV ;
- fonctionner en archive native autonome ou dans un conteneur durci.

## 03 — Local n'est pas un slogan, c'est la frontière du produit

| GitHealth refuse | Pourquoi | Preuve documentée |
|---|---|---|
| Cloner ou actualiser un remote | Le diagnostic porte sur l'état réellement présent sur le poste | [Isolation de Git](docs/SECURITY_MODEL.md#isolation-de-git) |
| Écrire dans le dépôt | L'observation ne doit jamais devenir une mutation | [Analyse en lecture seule](docs/DEVOPS.md#analyse-git-en-lecture-seule) |
| Envoyer de la télémétrie | Les identités et l'historique restent locaux | [Communications sortantes](docs/SECURITY_MODEL.md#confidentialité-et-communications-sortantes) |
| Exposer un service réseau | GitHealth est un outil local mono-utilisateur | [Frontière de confiance](docs/SECURITY_MODEL.md#objectif-et-frontière-de-confiance) |

Les commandes Git sont lancées sans shell, avec délai, budget de sortie, concurrence
bornée et environnement neutralisé. Le navigateur et l'API partagent une origine locale ;
la CSP, la session et les jetons anti-forgery renforcent cette frontière. La chaîne de
publication produit sommes SHA-256 et SBOM SPDX.

[Lire le modèle de sécurité](docs/SECURITY_MODEL.md) ·
[Consulter l'audit](SECURITY_AUDIT.md) ·
[Voir les limites assumées](docs/KNOWN_LIMITATIONS.md)

## 04 — Démarrer en quelques minutes

### Archive native — parcours recommandé

Télécharger l'archive correspondant à la machine depuis la
[dernière release](https://github.com/LINDECKER-Charles/App.GitHealth/releases/latest),
puis lancer l'exécutable depuis n'importe quel répertoire :

```powershell
# Windows x64
C:\Applications\GitHealth\githealth.exe --repo D:\Dev\MonDepot
```

```shell
# macOS Intel ou Apple Silicon
/Applications/GitHealth/githealth --repo "$HOME/Dev/MonDepot"
```

Le lanceur choisit un port libre sur `127.0.0.1`, ouvre le navigateur et conserve les
données dans le répertoire applicatif de l'utilisateur. Le runtime .NET est inclus ;
Git 2.38 ou plus récent est recommandé.

> [!NOTE]
> La release candidate macOS n'est pas encore signée ni notariée. Gatekeeper peut
> demander une autorisation explicite au premier lancement.

### Docker Compose

```shell
cp .env.example .env
# Renseigner GITHEALTH_REPOSITORIES_HOST_PATH dans .env
docker compose up --build
```

GitHealth répond alors sur `http://127.0.0.1:8080`. Les dépôts sont montés en lecture
seule ; seuls `/data` et `/tmp` restent inscriptibles dans le conteneur.

### Depuis les sources

```shell
dotnet restore App.GitHealth.sln
dotnet test App.GitHealth.sln
dotnet run --project src/App.GitHealth.Api -- --no-browser
```

Le développement de l'interface Angular et la boucle complète sont détaillés dans
[CONTRIBUTING.md](CONTRIBUTING.md).

## 05 — Une documentation organisée par intention

| Je veux… | Point d'entrée |
|---|---|
| prendre GitHealth en main | [Guide utilisateur](docs/USER_GUIDE.md) |
| comprendre les choix techniques | [Architecture](ARCHITECTURE.md) |
| installer, publier ou exploiter | [Guide DevOps](docs/DEVOPS.md) |
| résoudre un problème de lancement | [Dépannage](docs/TROUBLESHOOTING.md) |
| connaître précisément la frontière de confiance | [Modèle de sécurité](docs/SECURITY_MODEL.md) |
| mesurer ou reproduire les performances | [Benchmarks](docs/BENCHMARKING.md) |
| contribuer proprement | [Guide de contribution](CONTRIBUTING.md) |
| parcourir toute la documentation | [Centre de documentation](docs/README.md) |

## 06 — Un socle volontairement simple

```text
Angular 21  ──HTTP local──▶  ASP.NET Core 10  ──▶  domaine C# pur
                                    │
                                    ├──▶  processus Git bornés et read-only
                                    └──▶  SQLite · projets, politiques, snapshots
```

Le cœur métier ne dépend ni de Git, ni d'Entity Framework, ni du web. L'API orchestre
les lectures et la persistance ; Angular présente les faits et garde les filtres
partageables dans l'URL. Les tests couvrent le domaine, l'API, de vrais dépôts Git,
l'infrastructure Docker et le parcours navigateur.

[Explorer l'architecture complète](ARCHITECTURE.md) ·
[Lire les résultats de benchmark](docs/benchmarks/windows-initial.md)

## 07 — État du projet

La version préparée est **`0.1.0-rc.1`**. Elle inclut les distributions Windows et
macOS, Docker Compose, la qualification CI, un audit de sécurité et une baseline de
performance jusqu'à 1 000 branches. Le projet reste une release candidate : les
[limites connues](docs/KNOWN_LIMITATIONS.md) font partie du contrat public.

Les contributions sont bienvenues. Commencez par
[CONTRIBUTING.md](CONTRIBUTING.md), consultez le
[code de conduite](CODE_OF_CONDUCT.md) et utilisez le canal privé décrit dans
[SECURITY.md](SECURITY.md) pour toute vulnérabilité.

---

<p align="center">
  <strong>GitHealth observe. Vous gardez la décision.</strong><br>
  <sub>Distribué sous licence <a href="LICENSE">MIT</a>.</sub>
</p>
