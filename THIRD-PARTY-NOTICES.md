# Mentions relatives aux composants tiers

GitHealth est publié sous [licence MIT](LICENSE). Il s'appuie sur des composants tiers
qui restent soumis à leur propre licence et à leur propre copyright. Ce document recense
ceux qui sont **redistribués** avec l'application, puis ceux qui ne servent qu'à la
construire ou à la tester.

Cette page est une synthèse lisible. Les inventaires générés font foi :

- `artifacts/publish/wwwroot/3rdpartylicenses.txt` — textes complets des licences des
  paquets npm inclus dans le bundle Angular, produit par la configuration `production` ;
- `dotnet list App.GitHealth.sln package --include-transitive` — arbre exact des
  dépendances NuGet, versions transitives comprises ;
- `npm ls --prefix src/App.GitHealth.Web` — arbre exact des dépendances npm.

## Redistribué avec l'application

### Ressources embarquées dans l'interface

| Composant | Licence | Emplacement |
| --- | --- | --- |
| IBM Plex Sans, IBM Plex Mono | SIL OFL 1.1 | `src/App.GitHealth.Web/public/ds/fonts/` |
| Lucide Icons | ISC | `src/App.GitHealth.Web/public/ds/icons/` |

- IBM Plex — © 2017 IBM Corp., nom de police réservé « Plex ».
- Lucide Icons — © Lucide Icons and Contributors.

Les textes de licence accompagnent les fichiers concernés — `fonts/NOTICE.txt` et
`icons/LICENSE-lucide.txt` — et sont servis avec l'application. Ces deux fichiers doivent
suivre les polices et les glyphes dans toute copie ou redistribution : c'est une
obligation de la SIL OFL 1.1 comme de la licence ISC.

Les polices et les icônes sont servies **localement**. GitHealth ne contacte aucun CDN,
aucune fonderie et aucun service distant pour les charger.

### Bibliothèques d'exécution

| Composant | Licence |
| --- | --- |
| Angular (`@angular/*`) | MIT |
| RxJS | Apache-2.0 |
| tslib | 0BSD |
| ASP.NET Core et le runtime .NET | MIT |
| Entity Framework Core, `Microsoft.Data.Sqlite` | MIT |
| SQLitePCLRaw | Apache-2.0 |
| Moteur SQLite | domaine public |
| `Microsoft.AspNetCore.OpenApi`, `Microsoft.OpenApi` | MIT |

Les distributions natives Windows et macOS embarquent le runtime .NET : les mentions de
copyright Microsoft les accompagnent dans l'archive publiée.

## Nécessaire à l'exécution, non redistribué

| Composant | Licence | Rôle |
| --- | --- | --- |
| Git | GPL-2.0 | invoqué comme processus externe |

GitHealth **n'embarque pas Git** et n'en dérive pas : il exécute le binaire `git` déjà
installé sur la machine, sans shell, et lit sa sortie. Aucune archive publiée ne contient
de code Git, ce qui laisse la GPL-2.0 hors du périmètre de distribution du projet. Git
doit être installé séparément — version 2.38 ou plus récente recommandée.

## Images de conteneur

L'image Docker est construite à partir d'images de base publiées par leurs éditeurs
respectifs et soumises à leurs propres conditions :

| Image | Rôle |
| --- | --- |
| `node:24.20.0-alpine3.24` | construction du bundle Angular |
| `mcr.microsoft.com/dotnet/sdk:10.0.400-noble` | compilation .NET |
| `mcr.microsoft.com/dotnet/aspnet:10.0.11-noble` | exécution |

L'image d'exécution installe `ca-certificates`, `curl` et `git` depuis les dépôts Ubuntu ;
ces paquets restent couverts par leurs licences d'origine.

## Outillage de construction et de test

Ces composants ne sont pas redistribués avec l'application ; ils servent à la construire,
la vérifier et la mesurer.

| Composant | Licence |
| --- | --- |
| TypeScript | Apache-2.0 |
| Angular CLI, `@angular/build` | MIT |
| Vitest | MIT |
| jsdom | MIT |
| Prettier | MIT |
| Playwright | Apache-2.0 |
| xUnit.net | Apache-2.0 |
| `Microsoft.NET.Test.Sdk` | MIT |
| Coverlet | MIT |
| `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.Design` | MIT |
| Actions GitHub utilisées par les workflows | MIT |

## Ajouter une dépendance

Toute nouvelle dépendance ou ressource tierce doit :

1. porter une licence compatible avec MIT — permissive, sans copyleft étendu ;
2. être ajoutée au tableau correspondant de ce document ;
3. si elle est redistribuée, voir son texte de licence copié auprès des fichiers
   concernés, comme pour IBM Plex et Lucide.

Une dépendance sous licence inconnue, sous copyleft fort (GPL, AGPL) ou dont l'origine
n'est pas vérifiable ne peut pas être intégrée. Les modalités sont rappelées dans
[CONTRIBUTING.md](CONTRIBUTING.md).

## Signaler une erreur d'attribution

Une attribution manquante ou inexacte se signale par une issue publique ordinaire — ce
n'est pas une faille de sécurité. Indiquez le composant, sa licence réelle et sa source
amont ; la correction est traitée en priorité.
