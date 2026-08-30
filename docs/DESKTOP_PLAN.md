# Plan — Passage en application de bureau

> Document de passation. Statut : plan validé, non implémenté.
> Public : agent chargé de l'implémentation.

## 1. Objectif

Transformer GitHealth en **application de bureau installable et lançable au
double-clic** sur Windows, macOS et Linux, sans réécrire le front Angular et
sans casser le mode Docker existant.

Objectif secondaire, non bloquant : un bouton « Mise à jour disponible » dans
l'application, et une distribution via des gestionnaires de paquets gratuits.

## 2. Décisions arrêtées

Ces choix ont été débattus et tranchés. **Ne pas les rouvrir** sans élément
technique nouveau.

| Sujet | Décision | Raison |
|---|---|---|
| Coque | **Photino.NET** | Kestrel et la fenêtre vivent dans le même processus : aucune supervision de processus enfant, aucun handshake de port, aucun zombie. Electron imposerait ~150 lignes de plomberie de cycle de vie. |
| Découpage | **Un seul exécutable**, pas de projet `Desktop` séparé | `Publish-Native.ps1`, les smoke tests et `release.yml` continuent de fonctionner sans modification. Le poids des natives Photino inutilisées en mode Docker est négligeable (1-2 Mo, jamais chargées). |
| Installeur / MAJ | **Velopack** sur Windows et macOS | Gratuit, flux GitHub Releases déjà produit par `release.yml`, MAJ delta, installation per-user sans UAC. |
| MAJ sur Linux | **Aucune MAJ in-app** | Le support Linux de Velopack (AppImage seul) est son maillon faible, et un utilisateur Linux attend son gestionnaire de paquets, pas un bouton. |
| Priorité plateformes | **Windows et macOS d'abord**, Linux ensuite | Linux est explicitement secondaire. |

**Porte de sortie assumée** : le front est servi en HTTP sur loopback, donc la
coque est un composant isolé et remplaçable. Si WebKitGTK ou WebView2 posent un
problème rédhibitoire, on bascule sur Electron sans rien jeter d'autre.

## 3. Non-objectifs

- Ne pas réécrire le front Angular. Les modifications côté `App.GitHealth.Web`
  doivent rester **additives**.
- Ne pas supprimer le mode Docker ni `compose.yaml`. Le `.env` et son bind mount
  ne concernent que ce mode et restent valides.
- Ne pas traiter la signature de code ni la notarisation macOS dans ce plan
  (sujet de coût, à trancher avant la 1.0 publique).
- Ne pas embarquer MinGit pour l'instant : le lot 0 se limite à rendre le chemin
  de Git configurable et l'erreur actionnable.

## 4. État actuel du dépôt

À lire avant de coder — une grande partie du chemin est déjà faite.

- `src/App.GitHealth.Api/Program.cs:51` — `useNativeLauncher = isDirectLaunch && !IsContainer()`,
  détection du mode conteneur via `DOTNET_RUNNING_IN_CONTAINER`.
- `src/App.GitHealth.Api/Program.cs:182` — `RunNativeAsync` : démarre Kestrel sur
  loopback, résout le port bindé, ouvre le navigateur système.
- `src/App.GitHealth.Api/Hosting/SystemBrowserLauncher.cs` — ouverture du navigateur.
- `src/App.GitHealth.Api/Hosting/DataDirectoryResolver.cs` — données dans
  `%LOCALAPPDATA%\GitHealth`, `~/Library/Application Support/GitHealth`, XDG.
- `src/App.GitHealth.Api/Hosting/LauncherOptionsParser.cs` — flags existants :
  `--repo`, `--port`, `--data-dir`, `--no-browser`, `--help` / `-h`.
- `src/App.GitHealth.Api/Git/Paths/RepositoryPathGuard.cs:9` — `IsAllowed` renvoie
  `true` quand `RepositoriesRoot` est null : **en mode natif il n'y a aucune
  racine à configurer**, l'utilisateur pointe n'importe quel dossier.
- `src/App.GitHealth.Api/Features/Runtime/RuntimeEndpoints.cs` — `/api/runtime`
  (expose déjà `Mode` = `native` / `docker`) et `/api/runtime/directories`
  (navigateur de dossiers HTML).
- `eng/Publish-Native.ps1` — publish self-contained, `ValidateSet` limité à
  `win-x64`, `osx-x64`, `osx-arm64`.
- `.github/workflows/release.yml` — matrice 3 RID, smoke test natif, SBOM,
  attestation, GitHub Release.
- `tests/Infrastructure/Invoke-NativeSmokeTest.ps1:172` — lance le binaire avec
  `--no-browser --port --data-dir --repo` et vérifie la création de la base.

---

## Lot 0 — Découpler Git du PATH

**Prérequis dur à toute distribution.** Une application installable qui échoue au
premier scan sur un poste Windows sans Git, c'est le cas par défaut.

**Pourquoi** — `src/App.GitHealth.Api/Git/Process/GitProcessRunner.cs:120` fait
`new ProcessStartInfo("git")` : la résolution dépend entièrement du `PATH`.

**Fichiers**

- `src/App.GitHealth.Api/Git/GitScannerOptions.cs` — ajouter
  `public string? ExecutablePath { get; init; }` (lié à la section
  `GitHealth:Git`, déjà bindée dans `GitServiceCollectionExtensions.cs:14`).
- `src/App.GitHealth.Api/Git/Process/GitProcessRunner.cs` — consommer le chemin
  résolu au lieu du littéral `"git"`.
- Nouveau : un résolveur dédié sous `src/App.GitHealth.Api/Git/Process/`.
- `src/App.GitHealth.Api/Git/GitRuntimeDiagnostic.cs` — exposer le chemin retenu.
- `src/App.GitHealth.Api/Hosting/LauncherOptionsParser.cs` et
  `StartupFailureReporter.HelpText` — ajouter `--git-path <chemin>` sur le modèle
  de `--data-dir`.

**Travail**

Ordre de résolution, premier trouvé gagne :

1. `--git-path` / `GitHealth:Git:ExecutablePath`
2. `git` via le `PATH`
3. Emplacements standards par plateforme :
   - Windows : `%ProgramFiles%\Git\cmd\git.exe`,
     `%ProgramFiles(x86)%\Git\cmd\git.exe`,
     `%LOCALAPPDATA%\Programs\Git\cmd\git.exe`
   - macOS : `/opt/homebrew/bin/git`, `/usr/local/bin/git`, `/usr/bin/git`
   - Linux : `/usr/bin/git`, `/usr/local/bin/git`

`GitStartupProbe` (déjà un `IHostedService`) reste le point de sonde. Enrichir le
message d'indisponibilité pour qu'il soit **actionnable** : indiquer où l'on a
cherché et proposer `--git-path`.

Ajouter la disponibilité de Git et le chemin résolu à `RuntimeInfoResponse` pour
que le front puisse afficher un bandeau bloquant au lieu d'échouer au premier scan.

**Critères d'acceptation**

- Sur un poste sans Git dans le `PATH` mais avec Git installé à un emplacement
  standard, l'analyse fonctionne.
- Sans Git du tout, `/api/runtime` le signale et le message nomme `--git-path`.
- Le mode Docker n'est pas affecté (Git est dans l'image, résolution par `PATH`).

**Tests** — `tests/App.GitHealth.Api.Tests` : unitaires sur le résolveur
(configuration prioritaire, repli PATH, repli emplacements standards, cas
introuvable). Ne pas tester l'exécution réelle de Git, déjà couverte par
`tests/App.GitHealth.Git.IntegrationTests`.

---

## Lot 1 — Coque Photino

**Fichiers**

- `src/App.GitHealth.Api/App.GitHealth.Api.csproj` — `PackageReference` Photino.NET,
  version épinglée.
- Nouveau dossier `src/App.GitHealth.Api/Hosting/Desktop/`.
  ⚠️ `Hosting/` contient déjà 9 fichiers et la convention projet plafonne à 10
  par dossier : créer le sous-dossier, ne pas empiler.
- `src/App.GitHealth.Api/Program.cs:182` — `RunNativeAsync`.
- `src/App.GitHealth.Api/Hosting/LauncherOptions.cs` et `LauncherOptionsParser.cs`.

**Travail**

Remplacer l'ouverture du navigateur par une fenêtre, en gardant le repli. La
structure de `RunNativeAsync` reste la même : démarrer l'hôte, résoudre l'adresse
loopback via `BoundPort`, ouvrir l'interface, attendre la fermeture.

**Sémantique des flags — à respecter à la lettre, la CI en dépend :**

| Invocation | Comportement |
|---|---|
| (défaut, mode natif) | Fenêtre Photino |
| `--no-window` | Pas de fenêtre, navigateur système (comportement actuel) |
| `--no-browser` | **Aucune interface**, implique `--no-window` |
| mode conteneur | Inchangé, `app.RunAsync()` |

`--no-browser` doit valoir « aucune UI » et non « pas de navigateur mais une
fenêtre » : `tests/Infrastructure/Invoke-NativeSmokeTest.ps1:172` passe ce flag,
et une fenêtre s'ouvrirait sur les runners CI où elle resterait bloquée sur
l'attente de fermeture.

**Repli obligatoire** — si la création de la fenêtre échoue (moteur système
absent, typiquement WebKitGTK sur Linux), attraper `DllNotFoundException` et
`TypeInitializationException`, écrire un avertissement sur `stderr` et basculer
sur `SystemBrowserLauncher`. L'application ne doit jamais mourir faute de webview.

Garder `Program.cs` sous 300 lignes : extraire la logique de fenêtre dans
`Hosting/Desktop/`, pas dans les top-level statements.

**Critères d'acceptation**

- Double-clic sur `githealth.exe` : fenêtre GitHealth, aucun navigateur ouvert.
- `--no-window` : comportement actuel inchangé.
- `--no-browser` : aucune UI, le smoke test natif passe **sans modification**.
- Sur une machine sans moteur webview, l'app démarre et ouvre le navigateur.

**Tests** — unitaires sur la résolution du mode d'affichage à partir des
`LauncherOptions` (matrice des 4 lignes ci-dessus). La création de fenêtre
elle-même n'est pas testable en CI, ne pas essayer.

**Point de vigilance à lever dès ce lot** : valider le rendu du front Angular 22
sous WKWebView (macOS) et WebView2 (Windows). C'est le seul risque du plan qui ne
se découvre pas en le lisant. Le faire avant d'entamer le lot 2.

---

## Lot 2 — Dialogue de dossier natif

**Pourquoi** — c'est le vrai gain UX face au navigateur de dossiers HTML actuel,
et la réponse directe au problème de départ (« pointer un dossier »).

**Fichiers**

- `src/App.GitHealth.Api/Hosting/Desktop/` — pont de messages côté hôte.
- `src/App.GitHealth.Web/src/app/core/workspace/` — nouveau service de pont.
- `src/App.GitHealth.Web/src/app/shell/scan-folder/scan-folder-dialog.ts`
- `src/App.GitHealth.Web/src/app/shell/add-repository/`

**Travail**

Photino expose un pont `postMessage` bidirectionnel entre l'hôte et la page.
Côté hôte : enregistrer un handler de messages web, ouvrir le dialogue de dossier
natif, renvoyer le chemin choisi. **Vérifier les signatures exactes contre la
version de Photino épinglée au lot 1** plutôt que de se fier à ce document.

Côté Angular, **strictement additif** : un service qui détecte la présence du
pont et l'utilise s'il existe, sinon retombe sur `/api/runtime/directories` via
`src/App.GitHealth.Web/src/app/core/api/git-health-api-client.ts:40`. Les deux
modes restent vivants — Docker et le mode navigateur continuent d'utiliser le
navigateur HTML.

Le pont étant asynchrone, corréler requête et réponse par un identifiant. Une
seule requête en vol suffit : un dialogue modal à la fois.

**Critères d'acceptation**

- En fenêtre : le bouton de sélection ouvre le dialogue système.
- En navigateur ou en Docker : le navigateur de dossiers HTML actuel, inchangé.
- Aucune régression sur le parcours d'ajout de dépôt et de scan de dossier.

**Tests** — front : le service de pont testé sur ses deux branches (pont présent,
pont absent) avec Vitest.

---

## Lot 3 — Velopack : installeur et bouton de mise à jour

**Fichiers**

- `src/App.GitHealth.Api/App.GitHealth.Api.csproj` — `PackageReference` Velopack.
- `src/App.GitHealth.Api/Program.cs` — **première ligne du programme**.
- Nouveau `src/App.GitHealth.Api/Features/Updates/`.
- `eng/` — script de packaging `vpk`.
- `.github/workflows/release.yml`.

**Deux pièges à ne pas manquer**

1. **`VelopackApp.Build().Run()` doit être la toute première instruction**, avant
   `LauncherOptionsParser.Parse(args)` (`Program.cs:15`). Velopack y intercepte
   les hooks d'installation et de mise à jour ; placé plus bas, il ne fonctionne
   pas.
2. **Collision de chemins.** Velopack installe par défaut dans
   `%LocalAppData%\<packId>`, et `DataDirectoryResolver.cs:5` place déjà la base
   dans `%LOCALAPPDATA%\GitHealth`. Utiliser **`--packId App.GitHealth`** au
   `vpk pack` : l'installation va dans `%LocalAppData%\App.GitHealth`, les données
   restent dans `%LOCALAPPDATA%\GitHealth`, et une mise à jour ne peut pas écraser
   la base. **Aucune modification de `DataDirectoryResolver`.**

**Travail**

Abstraction, conforme au D de SOLID des conventions projet :

- `IUpdateService` dans `Features/Updates/`, avec un statut du type
  « non supporté » / « à jour » / « mise à jour disponible ».
- `NullUpdateService` — implémentation par défaut, renvoie « non supporté ».
  C'est elle qui sert en Docker, en mode navigateur et sur Linux.
- `VelopackUpdateService` — enregistrée **uniquement** quand `useNativeLauncher`
  est vrai et que la plateforme est Windows ou macOS. Source : `GithubSource` sur
  `https://github.com/LINDECKER-Charles/App.GitHealth`.
- Endpoints `GET /api/updates` et `POST /api/updates/apply`, montés à côté de
  `MapRuntimeEndpoints`.
- Front : un bouton discret dans le shell, affiché seulement si le statut le
  justifie. Additif, aucune refonte de la navigation.

Packaging : un script `eng/` sur le modèle de `Publish-Native.ps1`, qui prend le
dossier de publish et produit `Setup.exe` (Windows) ou `.pkg` (macOS) plus les
paquets delta. Le brancher dans `release.yml` après l'étape de smoke test natif,
et publier ces artefacts **en plus** des archives actuelles, pas à leur place :
les archives portables servent Scoop et les utilisateurs qui ne veulent pas
d'installeur.

**Critères d'acceptation**

- `Setup.exe` installe sans invite UAC et crée un raccourci.
- Lancement post-installation : fenêtre GitHealth, base intacte entre deux
  versions.
- En Docker et sur Linux, `/api/updates` renvoie « non supporté » et le bouton
  n'apparaît pas.
- Les archives `.zip` et `.tar.gz` actuelles restent publiées.

**Tests** — unitaires sur la sélection de l'implémentation d'`IUpdateService`
selon le mode et la plateforme. Ne pas tester Velopack lui-même.

---

## Lot 4 — Canaux de distribution

Par ordre de rapport effort / bénéfice.

1. **Scoop** (Windows, gratuit) — gain immédiat : un manifeste JSON d'une
   quinzaine de lignes pointant sur le `githealth-win-x64.zip` **déjà publié**.
   Ne demande ni installeur ni signature. Faisable avant même le lot 3.
2. **winget** (Windows, gratuit) — PR sur `winget-pkgs`. Exige une installation
   silencieuse, que le `Setup.exe` Velopack fournit.
3. **Homebrew Cask** (macOS) — techniquement gratuit, mais Gatekeeper met en
   quarantaine tout `.app` non notarisé. Suppose de trancher d'abord la question
   du compte développeur Apple (99 $/an). **Bloqué, hors périmètre.**
4. **Linux** — ajouter `linux-x64` au `ValidateSet` de `eng/Publish-Native.ps1`
   et un runner Linux à la matrice de `release.yml`. Le `.tar.gz` fonctionne déjà
   en mode navigateur, donc Linux est livrable **avant** que la question de la
   fenêtre y soit réglée. Cible idéale ensuite : **Flathub**, dont le runtime
   fournit WebKitGTK et supprime le problème de dépendance système.

---

## 5. Conventions du dépôt à respecter

Voir `CLAUDE.md`. Les points qui vont mordre sur ce chantier :

- **Commits** : Conventional Commits en français, sujet ≤ 72 caractères, un
  commit = un changement cohérent. Scopes : `src/App.GitHealth.Api/**` → `api`,
  `src/App.GitHealth.Web/**` → `front`, `.github/**` → `ci`, `docs/**` → `docs`.
  `eng/**` n'est pas cartographié : le rattacher à `infra`.
- **Branches** : `type/description-courte` en kebab-case, une branche par sujet.
  Un lot = une branche.
- **Tests livrés avec la feature**, même branche. Le nécessaire, pas la course à
  la couverture ; on ne teste ni le framework ni les bibliothèques tierces.
- **Limites** : fichier ≤ 300 lignes (400 max), 10 fichiers par dossier, méthode
  ≤ 30 lignes, ≤ 3 paramètres, imbrication ≤ 3, ligne ≤ 100 caractères.
- **Un seul élément public par fichier**, nommé comme le fichier.
- `TreatWarningsAsErrors` est actif (`Directory.Build.props`) : aucun warning ne
  passe.

## 6. Documentation à mettre à jour en fin de chantier

- `docs/KNOWN_LIMITATIONS.md:11` — « Il n'existe pas encore d'installeur, de mise
  à jour automatique ou de désinstallation ».
- `docs/IMPLEMENTATION_PLAN.md:367` — « Installeurs signés et mise à jour
  automatique ».
- `README.md` et `docs/USER_GUIDE.md` — le chemin d'installation par défaut
  devient l'application de bureau, Docker passe en mode auto-hébergement.
- `ARCHITECTURE.md` — la coque et le pont de messages.

## 7. Ordre d'exécution

Lot 0 → lot 1 (et validation du rendu webview) → lot 4.1 (Scoop) → lot 2 →
lot 3 → lot 4.2 puis 4.4.

Les lots 0 et 1 sont les seuls réellement bloquants. Chacun est livrable et
testable indépendamment.
