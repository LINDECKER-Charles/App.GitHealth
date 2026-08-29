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

Ouvrir ensuite `http://127.0.0.1:8080`. Dans l'interface, employer les chemins du
conteneur, par exemple `/repositories/mon-depot`.

## Ajouter un dépôt

1. Saisir son chemin absolu, ou utiliser **Parcourir** en mode natif.
2. Sélectionner **Vérifier**.
3. Choisir le nom affiché, la référence de comparaison et le périmètre des branches.
4. Sélectionner **Ajouter et ouvrir**.

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

Les filtres, le tri et la recherche sont inscrits dans l'URL. Ils peuvent être partagés
entre deux onglets de la même session locale.

## Expliquer une branche

Ouvrir une ligne du tableau pour consulter :

- sa topologie et ses compteurs d'avance et de retard ;
- les SHA et l'heure de capture ;
- les contributeurs normalisés par `.mailmap`, lorsqu'il existe ;
- la politique capturée et la raison exacte de la recommandation.

Après certaines fusions, Git ne permet plus d'attribuer avec certitude les commits à
leur branche d'origine. GitHealth signale alors l'attribution comme indisponible au lieu
d'inventer une identité.

## Configurer les politiques

La page **Politiques** permet de définir :

- le nombre de jours pendant lequel une branche est active ;
- le seuil à partir duquel elle devient inactive ;
- des motifs protégés ou exclus, avec `*` et `?` comme jokers.

**Prévisualiser** applique la proposition au dernier snapshot sans l'enregistrer et sans
relancer Git. L'enregistrement recalcule l'interprétation courante, mais ne modifie ni
les SHA ni les compteurs déjà capturés.

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

Deux exports répondent à des besoins différents :

- **Exporter en CSV** reprend les filtres et l'ordre de la vue courante ;
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
