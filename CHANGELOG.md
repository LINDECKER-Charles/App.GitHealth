# Journal des changements

Toutes les évolutions notables de GitHealth sont consignées dans ce fichier. Le format
suit Keep a Changelog et le versionnage sémantique.

## [Non publié]

### Ajouté

- **application de bureau** : GitHealth ouvre une fenêtre native au double-clic. Kestrel et la
  fenêtre vivent dans le même processus, et la fenêtre embarque le moteur de rendu du système —
  WebView2 sur Windows, WKWebView sur macOS, WebKitGTK sur Linux. Elle s'ouvre maximisée : à
  taille fixe, la largeur minimale de l'espace de travail n'est pas garantie sur un écran mis à
  l'échelle. Si le moteur est inutilisable, l'application avertit sur `stderr` et bascule sur le
  navigateur système au lieu de s'arrêter ;
- **dialogue de dossier du système** : en fenêtre, « Parcourir » ouvre le sélecteur de dossier
  natif et le chemin choisi revient dans le champ. En navigateur et en Docker, le navigateur de
  dossiers HTML reste inchangé ;
- **installeur et mise à jour in-app** sur Windows et macOS : `App.GitHealth-win-x64-Setup.exe`
  et `App.GitHealth-<rid>-Setup.pkg` installent par utilisateur dans
  `%LocalAppData%\App.GitHealth`, sans invite UAC, avec raccourcis Bureau et menu Démarrer. Un
  bouton « Mettre à jour » n'apparaît dans la barre supérieure que lorsqu'une version plus
  récente est publiée. La base reste dans `%LOCALAPPDATA%\GitHealth`, hors du dossier
  d'installation : elle survit aux mises à jour comme à la désinstallation. Les archives
  portables `.zip` et `.tar.gz` restent publiées en plus des installeurs ;
- **manifeste Scoop** `githealth.json`, produit et publié à chaque release Windows, pointant sur
  l'archive portable déjà publiée. Les données vivant dans `%LOCALAPPDATA%\GitHealth`, elles
  survivent à `scoop uninstall` ;
- **manifestes winget** générés et publiés avec la release ; la soumission à
  `microsoft/winget-pkgs` reste une action humaine ;
- **publication `linux-x64`** : `githealth-linux-x64.tar.gz` rejoint les artefacts de release.
  La fenêtre y dépend de WebKitGTK — à défaut, l'application ouvre le navigateur — et il n'y a
  pas de mise à jour in-app ;
- **résolution de Git hors du `PATH`** : l'option `--git-path <chemin>` ou la configuration
  `GitHealth:Git:ExecutablePath` prime, puis vient le `PATH`, puis les emplacements
  d'installation standards de la plateforme. `GET /api/runtime` expose la disponibilité, le
  chemin retenu et le diagnostic ; sans Git, un bandeau nomme les emplacements testés et
  `--git-path` au lieu de laisser le premier scan échouer ;
- **favoris et groupes de dépôts** : le rail épingle les dépôts favoris en tête et range
  les autres dans des groupes nommés, chacun repliable d'un clic. Le rangement est écrit
  dans la base — il suit la sauvegarde SQLite — tandis que l'état replié reste local au
  navigateur. Un rail sans favori ni groupe garde sa liste plate d'origine ;
- **scan d'un dossier entier** : GitHealth y détecte les dépôts Git jusqu'à une profondeur
  choisie, signale ceux déjà suivis, et analyse la sélection retenue en une fois. Les
  dépôts inconnus sont enregistrés au passage, chacun partant en analyse dès son
  enregistrement ;
- les analyses avancent désormais **en parallèle** — `AnalysisQueue:MaximumParallelAnalyses`
  fixe le nombre de lecteurs de la file, quatre par défaut, `1` restaurant le comportement
  strictement séquentiel. Un dépôt refusé par une file pleine repart dès qu'une place se
  libère ;
- design system Établi : jetons, polices IBM Plex et glyphes Lucide servis localement ;
- espace de travail unifié avec rail des dépôts, onglets et fiche de branche latérale ;
- palette de commandes `⌘K` pour atteindre une branche, un dépôt ou une action ;
- thème sombre mémorisé et séquence d'ouverture, toutes deux coupables au clavier ;
- tuiles de répartition, jetons de filtres actifs et actions groupées sur une sélection ;
- projection immédiate d'une politique en cours d'édition sur le dernier snapshot ;
- **licence MIT** : l'usage, la modification et la redistribution sont libres, à
  condition de conserver la mention de copyright ; `CITATION.cff` fournit les
  métadonnées pour citer le projet d'origine ;
- code de conduite, guide de contribution, page de support et mentions des composants
  tiers redistribués — polices IBM Plex sous SIL OFL 1.1 et glyphes Lucide sous ISC ;
- gabarits d'issues et de pull request, et propriétaires de code ;
- guide utilisateur complété : périmètre du produit, options du lanceur, lecture des
  recommandations, raccourcis clavier et questions fréquentes.

### Modifié

- le lancement natif ouvre désormais une **fenêtre de bureau**, là où il ouvrait le navigateur
  système. `--no-window` restaure ce comportement, et le mode conteneur est inchangé ;
- `--no-browser` vaut désormais **« aucune interface »** et implique `--no-window` : il sert les
  exécutions sans affichage, dont le smoke test natif ;
- **l'application de bureau devient le chemin d'installation par défaut**, et Docker le mode
  d'auto-hébergement ;
- une branche sans commit propre — fusionnée dans la référence, ou pointant sur le même
  commit — suit désormais une échelle d'activité réduite : vieillissante après 7 jours,
  inactive après 30. Elle n'est plus jamais recommandée « Conserver », alors que la règle
  précédente la laissait à conserver pendant trois mois ;
- nouvelle recommandation **« Terminée »**, en violet, pour une branche fusionnée dont le
  délai court encore. Le vert de « Conserver » signalait à tort qu'il n'y avait rien à
  faire et qu'il ne fallait pas y toucher ;
- nouvelle famille sémantique `--status-merged-*` dans le design system, déclinée en clair
  et en sombre sur la rampe prune existante ;
- le snapshot est chargé une fois puis filtré, trié et compté sans nouvel appel ;
- l'export CSV est produit localement et suit la vue ou la sélection ;
- l'historique affiche le nombre de branches lues et l'écart avec le passage précédent.

### Corrigé

- la feuille de styles globale ne s'appliquait pas dans le paquet publié : le
  gestionnaire `onload` du CSS critique était bloqué par la politique de sécurité ;
- une adresse profonde rechargée servait une page vide : `base-uri 'none'` bloquait
  la balise `<base href="/">`, et les URL relatives d'`index.html` se résolvaient
  depuis la route courante. La directive passe à `'self'`.

## [0.1.0-rc.1] - 2026-08-29

### Ajouté

- analyse locale des branches avec avance, retard, fusion, activité et contributeurs ;
- prise en charge des dépôts standards, bare et worktrees liés ;
- tableau de bord filtrable, détail des branches et historique des snapshots ;
- politiques d'activité, motifs protégés ou exclus et prévisualisation ;
- relocalisation vérifiée d'un dépôt déplacé avec conservation de son historique ;
- export CSV filtré et sauvegarde cohérente de SQLite ;
- lanceurs autonomes Windows x64, macOS Intel et macOS Apple Silicon ;
- image Docker non privilégiée avec montage des dépôts en lecture seule ;
- benchmark reproductible jusqu'à 1 000 branches et scénario E2E Playwright.

### Sécurité

- commandes Git sans shell, bornées en temps et en volume de sortie ;
- validation canonique des chemins, `commondir`, object databases et alternates ;
- isolation de l'environnement Git pour l'application, la recette et les benchmarks ;
- écoute loopback, session locale, contrôle d'origine et protection anti-forgery ;
- reprise explicite des analyses interrompues et exclusion analyse/relocalisation ;
- audit de dépendances, CodeQL, SBOM et provenance dans les workflows de livraison.

### Limites

- archives macOS non signées et non notariées ;
- absence de récupération réseau et d'intégration aux forges ;
- produit local mono-utilisateur, non prévu pour une exposition réseau.
