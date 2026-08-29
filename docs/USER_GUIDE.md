# Guide utilisateur GitHealth

Ce guide couvre la release candidate `0.1.0-rc.1`. GitHealth s'exécute sur le poste,
lit des dépôts déjà présents et conserve ses résultats dans une base SQLite locale.

## Installer et démarrer

### Windows

1. Extraire entièrement `githealth-win-x64.zip`.
2. Lancer `githealth.exe`.
3. Autoriser l'ouverture du navigateur si Windows la demande.

Un dépôt peut être prérempli dès le lancement :

```powershell
githealth.exe --repo D:\Dev\MonDepot
```

### macOS

Extraire l'archive correspondant au processeur, puis lancer `githealth` :

```shell
./githealth --repo "$HOME/Dev/MonDepot"
```

La release candidate n'est ni signée ni notariée. Si Gatekeeper bloque le premier
lancement, vérifier l'origine de l'archive avant d'autoriser explicitement le binaire
dans les réglages de confidentialité de macOS.

### Docker

Copier `.env.example` vers `.env`, indiquer dans `GITHEALTH_REPOSITORIES_ROOT` le
dossier parent des dépôts, puis lancer :

```shell
docker compose up --build
```

Ouvrir ensuite `http://127.0.0.1:8080`. Le bouton **Parcourir** affiche les dossiers
montés sous `/repositories` et permet de choisir le dépôt sans saisir son chemin de
conteneur.

## Se repérer dans l'espace de travail

Une séquence d'ouverture décrit ce que GitHealth lit au démarrage. **Passer
l'introduction** ou la touche `Échap` la coupent ; elle ne rejoue plus pendant la
session. Un mouvement réduit demandé par le système la supprime entièrement.

L'écran tient en trois zones :

- la **barre supérieure** porte la recherche globale, le thème clair ou sombre, la
  sauvegarde des données et le guide ;
- le **rail** liste les dépôts observés, leur accessibilité et leur chemin ;
- la **zone centrale** présente le dépôt courant sous trois onglets : **Diagnostic**,
  **Historique** et **Politiques**.

`⌘K` ou `Ctrl+K` ouvre la palette de commandes : elle atteint une branche, un dépôt ou
une action au clavier. Les flèches parcourent les résultats, `Entrée` valide, `Échap`
referme.

## Ajouter un dépôt

1. Sélectionner **Ajouter un dépôt**.
2. Saisir son chemin absolu, ou utiliser **Parcourir**. Le chemin est vérifié pendant
   la saisie : GitHealth annonce les références candidates dès qu'il reconnaît le dépôt.
3. Choisir le nom affiché, la référence de comparaison et le périmètre des branches.
4. Sélectionner **Ajouter le dépôt**.

GitHealth accepte les dépôts standards, bare et les worktrees liés. Il ne clone pas de
dépôt et n'utilise aucun identifiant distant.

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

- le nombre de jours pendant lequel une branche est active ;
- le seuil à partir duquel elle devient inactive ;
- des motifs protégés ou exclus, avec `*` et `?` comme jokers.

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

Pour restaurer une sauvegarde SQLite, arrêter GitHealth, conserver une copie de la base
courante, remplacer `githealth.db`, puis redémarrer l'application.

## Arrêter et reprendre

Fermer le processus `githealth` arrête l'application. Au prochain démarrage avec le même
répertoire de données, les projets, politiques et snapshots sont restaurés. Les options
du lanceur et les emplacements de données sont détaillés dans [DEVOPS.md](DEVOPS.md).
Une analyse interrompue par un arrêt brutal est classée **Annulée** au redémarrage ; le
dernier snapshot réussi reste disponible.

En cas d'échec, consulter [TROUBLESHOOTING.md](TROUBLESHOOTING.md) et les
[limites connues](KNOWN_LIMITATIONS.md).
