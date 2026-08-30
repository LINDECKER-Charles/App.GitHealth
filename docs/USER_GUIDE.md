# Guide utilisateur GitHealth

Ce guide couvre la release candidate `0.1.0-rc.1`. GitHealth s'exécute sur le poste,
lit des dépôts déjà présents et conserve ses résultats dans une base SQLite locale.

## Sommaire

- [Ce que GitHealth fait, et ne fait jamais](#ce-que-githealth-fait-et-ne-fait-jamais)
- [Installer et démarrer](#installer-et-démarrer)
- [Options du lanceur](#options-du-lanceur)
- [Se repérer dans l'espace de travail](#se-repérer-dans-lespace-de-travail)
- [Raccourcis clavier](#raccourcis-clavier)
- [Ajouter un dépôt](#ajouter-un-dépôt)
- [Scanner un dossier entier](#scanner-un-dossier-entier)
- [Lire une analyse](#lire-une-analyse)
- [Comprendre les recommandations](#comprendre-les-recommandations)
- [Expliquer une branche](#expliquer-une-branche)
- [Configurer les politiques](#configurer-les-politiques)
- [Relocaliser un dépôt déplacé](#relocaliser-un-dépôt-déplacé)
- [Historique et exports](#historique-et-exports)
- [Arrêter et reprendre](#arrêter-et-reprendre)
- [Questions fréquentes](#questions-fréquentes)
- [Aller plus loin](#aller-plus-loin)

## Ce que GitHealth fait, et ne fait jamais

GitHealth **observe**. Il lit l'historique d'un dépôt déjà présent sur la machine, en
tire des mesures, et propose une lecture de l'état de ses branches. Il ne décide rien à
votre place et n'exécute aucune action de nettoyage.

| GitHealth fait | GitHealth ne fait jamais |
| --- | --- |
| Lire les branches locales et de suivi distant | Supprimer, fusionner ou pousser une branche |
| Comparer chaque branche à une référence choisie | Faire un checkout ou modifier le worktree |
| Mesurer avance, retard, fusion et activité | Lancer `git fetch` ou `git remote prune` |
| Identifier les contributeurs d'une branche | Cloner un dépôt ou gérer un identifiant |
| Proposer une recommandation et l'expliquer | Transmettre vos données à l'extérieur |
| Conserver l'historique des analyses en local | Écrire quoi que ce soit dans le dépôt |
| Copier pour vous la commande de suppression | Exécuter cette commande |

Aucune analyse n'écrit dans le dépôt : les références, l'index, le worktree et les
reflogs restent intacts. Le détail de ces garanties est décrit dans le
[modèle de sécurité](SECURITY_MODEL.md).

## Installer et démarrer

GitHealth s'installe comme une application de bureau : il ouvre sa propre fenêtre,
adossée au moteur de rendu du système. Les archives portables restent publiées pour qui
préfère se passer d'installeur, et le mode Docker sert l'auto-hébergement.

### Windows

1. Télécharger `App.GitHealth-win-x64-Setup.exe` depuis la page des releases.
2. Le lancer : GitHealth s'installe pour l'utilisateur courant dans
   `%LOCALAPPDATA%\App.GitHealth`, sans invite UAC, avec un raccourci sur le Bureau et
   dans le menu Démarrer.
3. Ouvrir GitHealth : la fenêtre s'affiche maximisée.

L'installeur n'est pas signé. Si Windows demande une confirmation supplémentaire au
premier lancement, vérifier l'origine du fichier avant de poursuivre.

**Scoop** installe l'archive portable plutôt que l'installeur : chaque release Windows
publie un manifeste `githealth.json` à côté des archives, et `scoop install` accepte son
URL directement. Les données vivant dans `%LOCALAPPDATA%\GitHealth`, elles survivent à
`scoop uninstall`.

Sans installeur, extraire entièrement `githealth-win-x64.zip` puis lancer
`githealth.exe`. Un dépôt peut être prérempli dès le lancement :

```powershell
githealth.exe --repo D:\Dev\MonDepot
```

### macOS

Télécharger `App.GitHealth-osx-arm64-Setup.pkg`, ou la variante `osx-x64` sur un Mac
Intel, puis ouvrir le paquet et suivre l'installation.

Ni l'installeur ni les archives ne sont signés ni notariés. Si Gatekeeper bloque le
premier lancement, vérifier l'origine du fichier avant d'autoriser explicitement
l'application dans les réglages de confidentialité de macOS.

Sans installeur, extraire l'archive correspondant au processeur, puis lancer
`githealth` :

```shell
./githealth --repo "$HOME/Dev/MonDepot"
```

### Linux

Extraire `githealth-linux-x64.tar.gz`, puis lancer `githealth`. La fenêtre y dépend de
WebKitGTK : sans cette bibliothèque, GitHealth écrit un avertissement et ouvre
l'interface dans le navigateur système. Il n'y a pas d'installeur ni de mise à jour
depuis l'application ; une nouvelle version se récupère comme la première.

### Auto-hébergement avec Docker

Le mode conteneur n'ouvre aucune fenêtre : il sert l'interface en HTTP, à ouvrir dans un
navigateur.

Copier `.env.example` vers `.env`, indiquer dans `GITHEALTH_REPOSITORIES_ROOT` le
dossier parent des dépôts, puis lancer :

```shell
docker compose up --build
```

Ouvrir ensuite `http://127.0.0.1:8080`. Le bouton **Parcourir** affiche les dossiers
montés sous `/repositories` et permet de choisir le dépôt sans saisir son chemin de
conteneur.

### Mettre à jour

Installé par `Setup.exe` ou par le paquet macOS, GitHealth vérifie si une version plus
récente est publiée. Le cas échéant, un bouton **Mettre à jour** apparaît dans la barre
supérieure : il télécharge la version, l'installe et relance l'application. Hors d'une
installation gérée — archive portable, Scoop, Docker, Linux — le bouton n'apparaît pas.

Si la source des releases est injoignable, rien ne s'affiche et rien n'échoue :
l'application reste utilisable hors ligne. La base vit dans un dossier disjoint de
l'installation : elle survit aux mises à jour comme à la désinstallation.

### Prérequis

Git 2.38 ou plus récent est recommandé. GitHealth embarque le runtime .NET, mais **pas
Git** : il doit déjà être installé sur le poste. Il le cherche seul, et le premier trouvé
gagne : le chemin donné par `--git-path`, puis `git` via le `PATH`, puis les emplacements
d'installation standards — `%ProgramFiles%\Git\cmd`, `%ProgramFiles(x86)%\Git\cmd` et
`%LOCALAPPDATA%\Programs\Git\cmd` sur Windows, `/opt/homebrew/bin`, `/usr/local/bin` et
`/usr/bin` sur macOS, `/usr/bin` et `/usr/local/bin` sur Linux.

Si aucun ne convient, l'interface affiche un bandeau nommant les emplacements testés au
lieu d'échouer au premier scan. `--git-path <chemin>`, ou la configuration
`GitHealth:Git:ExecutablePath`, désigne alors l'exécutable à utiliser.

Les archives ne sont pas des exécutables monofichiers — les extraire entièrement et
garder leurs fichiers ensemble.

## Options du lanceur

| Option | Défaut | Effet |
| --- | --- | --- |
| `--repo <chemin>` | vide | préremplit le dépôt proposé sur l'accueil |
| `--port <1-65535>` | port disponible | impose un port précis sur l'interface loopback |
| `--data-dir <chemin>` | répertoire système | déplace la base et son verrou d'instance |
| `--git-path <chemin>` | résolution automatique | impose l'exécutable Git à utiliser |
| `--no-window` | fenêtre de bureau | ouvre l'interface dans le navigateur système |
| `--no-browser` | interface ouverte | n'ouvre aucune interface au démarrage |
| `--help`, `-h` | — | affiche l'aide puis quitte |

Les formes `--repo=…`, `--port=…`, `--data-dir=…` et `--git-path=…` sont également
acceptées. `--no-browser` implique `--no-window` : ni fenêtre ni navigateur, l'interface
reste joignable à l'adresse annoncée sur la console. En mode conteneur, aucune interface
n'est ouverte et ces deux options n'ont pas d'objet.

Sans `--port`, le système attribue un port disponible ; GitHealth n'écoute que sur
`127.0.0.1` et refuse de démarrer plutôt que de basculer silencieusement sur une
interface réseau.

Les emplacements de données par défaut et les variables d'environnement équivalentes
sont détaillés dans [DEVOPS.md](DEVOPS.md).

## Se repérer dans l'espace de travail

Une séquence d'ouverture décrit ce que GitHealth lit au démarrage. **Passer
l'introduction** ou la touche `Échap` la coupent ; elle ne rejoue plus pendant la
session. Un mouvement réduit demandé par le système la supprime entièrement.

La fenêtre s'ouvre maximisée : l'espace de travail réclame au moins 1180 pixels CSS de
large, et une taille fixe ne les garantit pas sur un écran mis à l'échelle. La restaurer
la ramène à 1360 × 860 pixels, sans jamais descendre sous 960 × 600.

L'écran tient en trois zones :

- la **barre supérieure** porte la recherche globale, le thème clair ou sombre, la
  sauvegarde des données et le guide — ainsi que **Mettre à jour** quand une version plus
  récente est publiée ;
- le **rail** liste les dépôts observés, leur accessibilité et leur chemin, rangés en
  sections repliables ;
- la **zone centrale** présente le dépôt courant sous trois onglets : **Diagnostic**,
  **Historique** et **Politiques**.

## Raccourcis clavier

| Raccourci | Effet |
| --- | --- |
| `⌘K` / `Ctrl+K` | ouvrir la palette de commandes |
| `↑` `↓` | parcourir les résultats de la palette |
| `Entrée` | valider le résultat surligné |
| `Échap` | fermer la palette, un panneau, ou couper la séquence d'ouverture |

La palette atteint une branche, un dépôt ou une action sans quitter le clavier. Sur un
dépôt de plusieurs centaines de branches, c'est le chemin le plus court entre une idée
et la fiche correspondante.

## Ranger le rail : favoris et groupes

Passer la souris sur un dépôt du rail fait apparaître deux actions.

- L'**étoile** l'épingle dans la section **Favoris**, tout en haut du rail. L'étoile reste
  pleine et visible une fois le dépôt en favori. Un favori ne paraît que là : il quitte la
  section de son groupe, et le rail ne montre jamais deux fois le même dépôt.
- Le **dossier ouvert** ouvre **Ranger dans un groupe** : choisir un groupe existant,
  **Sans groupe**, ou saisir un nom et sélectionner **Créer**. Un groupe naît de son premier
  dépôt et disparaît quand le dernier le quitte. Les deux mêmes actions sont dans la palette
  `⌘K` pour le dépôt ouvert.

Chaque en-tête de section replie ou déplie son contenu, et le compteur à droite dit combien
de dépôts s'y trouvent. Les groupes sont classés par ordre alphabétique, les dépôts aussi à
l'intérieur d'une section ; **Sans groupe** ferme la marche. Tant qu'aucun favori ni groupe
n'existe, le rail reste une liste plate, sans en-tête.

Favoris et groupes sont enregistrés dans la base locale : ils suivent la sauvegarde des
données. Les sections repliées, elles, restent dans le navigateur de ce poste.

## Ajouter un dépôt

1. Sélectionner **Ajouter un dépôt**.
2. Saisir son chemin absolu, ou utiliser **Parcourir**. Le chemin est vérifié pendant
   la saisie : GitHealth annonce les références candidates dès qu'il reconnaît le dépôt.
3. Choisir le nom affiché, la référence de comparaison et le périmètre des branches.
4. Sélectionner **Ajouter le dépôt**.

En fenêtre, **Parcourir** ouvre le dialogue de dossier du système : le chemin choisi
revient dans le champ. Dans un navigateur et en Docker, il affiche le navigateur de
dossiers servi par l'application.

GitHealth accepte les dépôts standards, bare et les worktrees liés. Il ne clone pas de
dépôt et n'utilise aucun identifiant distant.

## Scanner un dossier entier

Pour prendre en charge plusieurs dépôts d'un coup, sélectionner **Scanner un dossier**.

1. Saisir le dossier à explorer, ou utiliser **Parcourir**.
2. Choisir la **profondeur** : le nombre de niveaux inspectés sous ce dossier. Un dépôt
   trouvé n'est pas ouvert plus loin, et les dossiers cachés ou de build sont ignorés.
3. Sélectionner **Détecter les dépôts**. La liste distingue les dépôts déjà suivis, les
   bare repositories et ceux dont aucune référence n'est lisible — ces derniers ne sont
   pas sélectionnables.
4. Décocher ce qui ne doit pas être mesuré, puis sélectionner **Analyser N dépôts**.

Les dépôts inconnus sont d'abord enregistrés avec la référence proposée et les seuils
initiaux ; ceux déjà suivis gardent leur configuration. Chaque dépôt part en analyse dès
son enregistrement, sans attendre les suivants.

Les analyses avancent **en parallèle** dans la limite fixée par l'hôte, et en file pour
le reste : un dépôt refusé par une file pleine repart automatiquement dès qu'une place se
libère. Le suivi reste lisible dans le rail. Fermer l'onglet du navigateur n'interrompt
rien ; fermer la fenêtre de bureau arrête GitHealth, et les analyses en cours avec lui.

## Lire une analyse

Sélectionner **Lancer une analyse** depuis le tableau de bord. Les phases visibles
distinguent la lecture de la topologie, l'enrichissement et la persistance.

Pour une référence `R` et une branche `B` :

- l'avance compte les commits accessibles depuis `B`, mais pas depuis `R` ;
- le retard compte les commits accessibles depuis `R`, mais pas depuis `B` ;
- l'activité correspond à la date du commit pointé par la branche ;
- les contributeurs viennent des commits propres à `B`, hors commits de fusion.

La capture affiche les SHA réellement comparés. Les références peuvent évoluer ensuite
sans modifier ce snapshot. GitHealth ne lance jamais automatiquement `fetch` : une
branche distante affichée reflète donc l'état local de `refs/remotes`.

Le snapshot est chargé une fois, puis filtré, trié et compté sans nouvel appel. Les
tuiles donnent la répartition des recommandations et servent de filtre. Les jetons
**Filtres actifs** rappellent ce qui restreint la vue et se retirent un par un.

Cocher des lignes ouvre les actions groupées : protéger, exclure, copier les commandes
`git` correspondantes ou exporter la sélection.

## Comprendre les recommandations

Chaque branche reçoit trois qualifications indépendantes : sa **topologie** vis-à-vis de
la référence, son **activité**, et la **recommandation** qui en découle.

### Topologie

| Étiquette | Signification |
| --- | --- |
| **Synchronisée** | la branche et la référence pointent le même commit |
| **En avance** | elle porte des commits que la référence n'a pas, sans retard |
| **Fusionnée** | son sommet est un ancêtre de la référence : tout son travail y est déjà |
| **Divergente** | chacune porte des commits que l'autre n'a pas |
| **Sans base** | aucun ancêtre commun avec la référence |

### Activité

L'activité mesure l'âge du commit pointé par la branche, pas le temps passé dessus :
Git ne conserve ni l'intention de création ni les checkouts.

| Étiquette | Signification |
| --- | --- |
| **Active** | plus récente que le seuil d'activité |
| **Vieillissante** | entre les deux seuils |
| **Inactive** | plus ancienne que le seuil d'inactivité |
| **Inconnue** | aucune date de sommet exploitable |

### Recommandation

| Recommandation | Quand elle apparaît |
| --- | --- |
| **Conserver** | commits propres, activité dans les seuils, topologie sans alerte |
| **À examiner** | branche inactive, divergente ou sans base ; ou fusionnée hors délai |
| **Nettoyage possible** | aucun commit propre et plus aucune activité depuis longtemps |
| **Terminée** | aucun commit propre, mais le délai court encore — ou date de sommet illisible |
| **Exclue** | un motif protégé ou exclu capture la référence, avant toute autre règle |

Un motif **protégé** ou **exclu** l'emporte sur tout le reste : la branche sort du
classement et la fiche indique lequel des deux motifs l'a capturée.

« Terminée » n'est pas « Conserver ». Une branche fusionnée ne détient plus rien
d'unique — la référence contient déjà tout son historique — et le vert de « Conserver »
laisserait croire qu'il faut la préserver. Le vert reste réservé aux branches qui
portent des commits que la référence n'a pas.

La fiche de branche indique toujours la règle appliquée en clair, ainsi que l'échelle de
seuils utilisée.

## Expliquer une branche

Ouvrir une ligne du tableau : la fiche s'ouvre à droite et la branche est inscrite dans
l'URL, donc partageable entre deux onglets de la même session locale. La fiche donne :

- la recommandation et la trace des règles qui y mènent ;
- sa topologie et ses compteurs d'avance et de retard ;
- les SHA et l'heure de capture ;
- les contributeurs normalisés par `.mailmap`, lorsqu'il existe ;
- la commande de suppression manuelle, à copier. GitHealth ne l'exécute jamais.

**Protéger** et **Exclure** ajoutent la référence aux motifs du dépôt et enregistrent la
politique aussitôt. **Suivante** parcourt la vue courante sans quitter la fiche.

Après certaines fusions, Git ne permet plus d'attribuer avec certitude les commits à
leur branche d'origine. GitHealth signale alors l'attribution comme indisponible au lieu
d'inventer une identité.

## Configurer les politiques

La page **Politiques** permet de définir :

- le nombre de jours pendant lequel une branche est active — **30 jours** par défaut ;
- le seuil à partir duquel elle devient inactive — **90 jours** par défaut ;
- des motifs protégés ou exclus, avec `*` et `?` comme jokers.

Les motifs s'appliquent au nom complet de la référence, `refs/heads/…` ou
`refs/remotes/…`, et respectent la casse. Le seuil d'inactivité doit rester strictement
supérieur au seuil d'activité.

Ces deux seuils valent pour les branches qui portent des commits propres. Une branche
**fusionnée dans la référence**, ou pointant sur le même commit qu'elle, ne détient plus
rien d'unique : la référence contient déjà tout son historique, et la supprimer ne perd
aucun commit. Elle suit donc une échelle réduite, et ne passe jamais par « Conserver » :

| âge du sommet | recommandation |
| --- | --- |
| jusqu'à 7 jours | **Terminée**, en violet |
| de 7 à 30 jours | **À examiner** |
| au-delà de 30 jours | **Nettoyage possible** |

« Terminée » n'est pas « Conserver » : il n'y a rien à préserver, seulement rien à faire
dans l'immédiat. Le vert de « Conserver » reste réservé aux branches qui portent des
commits que la référence n'a pas. La fiche de branche indique l'échelle appliquée et
pourquoi. Si les seuils du projet sont déjà plus courts, ce sont eux qui s'appliquent :
l'échelle réduite ne rallonge jamais rien.

Le panneau **Effet sur le dernier snapshot** projette la politique en cours d'édition
sur les faits déjà capturés, sans relancer Git : il compare chaque recommandation à la
politique enregistrée et liste les branches touchées par les motifs. L'enregistrement
recalcule l'interprétation courante, mais ne modifie ni les SHA ni les compteurs déjà
capturés.

## Relocaliser un dépôt déplacé

Si le chemin d'un dépôt change, ouvrir **Politiques**, saisir son nouveau chemin absolu
dans **Relocaliser le dépôt**, puis confirmer. GitHealth vérifie la référence configurée
et, s'il existe un snapshot réussi, la présence de son commit de référence avant de
remplacer le chemin. Le projet garde le même identifiant, ses politiques, toutes ses
analyses et son dernier snapshot réussi. La relocalisation est refusée pendant une
analyse ; attendre sa fin puis réessayer.

En Docker, le nouveau chemin doit se trouver sous `/repositories`. Si le dossier parent
monté sur l'hôte change, recréer d'abord le conteneur avec la nouvelle valeur de
`GITHEALTH_REPOSITORIES_ROOT`, puis utiliser le chemin vu depuis le conteneur.

## Historique et exports

**Historique** conserve chaque exécution et la politique utilisée. Une analyse échouée
ne remplace jamais le dernier snapshot réussi.

Chaque passage indique sa référence, ses seuils, le nombre de branches lues et l'écart
avec le passage précédent. **Politique** déplie les motifs capturés à ce moment-là ;
**Ouvrir ce snapshot** relit l'analyse avec la politique de l'époque.

Trois exports répondent à des besoins différents :

- **Exporter en CSV** reprend l'ensemble du snapshot courant ;
- **Exporter la sélection** ne reprend que les lignes cochées ;
- **Sauvegarder les données** télécharge une copie cohérente de toute la base SQLite.

Le CSV est encodé en UTF-8 et neutralise les cellules qu'un tableur pourrait interpréter
comme des formules. Il contient des noms de branches et des identités d'auteur :
traitez-le comme une donnée interne.

Pour restaurer une sauvegarde SQLite, arrêter GitHealth, conserver une copie de la base
courante, remplacer `githealth.db`, puis redémarrer l'application.

## Arrêter et reprendre

Fermer la fenêtre de bureau, ou le processus `githealth`, arrête l'application. En mode
navigateur, fermer l'onglet laisse le processus en place : c'est lui qu'il faut arrêter.
Au prochain démarrage avec le même répertoire de données, les projets, politiques et
snapshots sont restaurés. Les options du lanceur et les emplacements de données sont
détaillés dans [DEVOPS.md](DEVOPS.md).
Une analyse interrompue par un arrêt brutal est classée **Annulée** au redémarrage ; le
dernier snapshot réussi reste disponible.

Une seule instance peut écrire dans un même répertoire de données. Pour en lancer deux en
parallèle, donner à la seconde un `--data-dir` distinct.

## Questions fréquentes

**Une branche distante affiche un état que je sais périmé.**
GitHealth ne lance jamais `fetch`. Il lit `refs/remotes` tel qu'il est sur le poste.
Faire un `git fetch --prune` dans le dépôt, puis relancer l'analyse.

**Un même contributeur apparaît deux fois.**
Il a utilisé plusieurs adresses. Ajouter un fichier `.mailmap` au dépôt pour les
regrouper : GitHealth le respecte quand il existe.

**Les contributeurs d'une branche fusionnée sont indisponibles.**
Après une fusion complète, `R..B` est vide et Git ne permet plus d'attribuer les commits
à leur branche d'origine. GitHealth le signale plutôt que d'inventer une réponse.

**Puis-je faire supprimer les branches par GitHealth ?**
Non, et ce n'est pas prévu. La commande de suppression est copiable depuis la fiche de
branche ou depuis les actions groupées ; c'est à vous de l'exécuter, après relecture.

**Où sont stockées mes données ?**
Dans un fichier `githealth.db` local, dont l'emplacement dépend du système et de
`--data-dir`. Rien n'est envoyé à l'extérieur. Sur Windows, il vit dans
`%LOCALAPPDATA%\GitHealth`, un dossier disjoint de l'installation : mise à jour,
désinstallation et `scoop uninstall` le laissent intact.

**Le bouton « Mettre à jour » n'apparaît jamais.**
Il ne concerne que les installations gérées, sur Windows et macOS. Depuis une archive
portable, Scoop, Docker ou Linux, la mise à jour passe par le canal d'origine. Sinon,
c'est qu'aucune version plus récente n'est publiée, ou que la source des releases est
injoignable.

**GitHealth ne trouve pas Git alors qu'il est installé.**
Il cherche dans le `PATH`, puis aux emplacements d'installation standards. Une
installation ailleurs se déclare avec `--git-path <chemin>` ; le bandeau d'alerte liste
les emplacements déjà testés.

**Puis-je exposer GitHealth à mon équipe sur le réseau ?**
Non. Le produit est mono-utilisateur, écoute sur `127.0.0.1` et n'a ni authentification
ni cloisonnement. Une exposition réseau sort de son modèle de menace.

**L'analyse est lente sur un très gros dépôt.**
Les mesures et les budgets de performance sont publiés dans
[BENCHMARKING.md](BENCHMARKING.md). Réduire le périmètre des branches — locales
seulement — raccourcit nettement la lecture.

## Aller plus loin

- [Dépannage](TROUBLESHOOTING.md) — l'application ne démarre pas, le port est pris,
  Git est introuvable, un dépôt est refusé.
- [Limites connues](KNOWN_LIMITATIONS.md) — les comportements surprenants qui sont des
  conséquences assumées de la sémantique Git.
- [Modèle de sécurité](SECURITY_MODEL.md) — ce que l'application lit, écrit, et
  n'envoie jamais.
- [Architecture](../ARCHITECTURE.md) — comment les mesures sont calculées.
- [Obtenir de l'aide](../SUPPORT.md) — choisir le bon canal et rédiger une demande
  traitable, sans exposer de données réelles.
