# Dépannage

## Le navigateur ne s'ouvre pas

L'application peut être fonctionnelle même si l'ouverture automatique échoue. Copier
l'adresse `http://127.0.0.1:<port>` affichée dans la console. L'option `--no-browser`
désactive volontairement cette ouverture.

## Le port demandé est indisponible

Libérer le port, en choisir un autre avec `--port`, ou omettre l'option pour laisser le
système sélectionner un port disponible. GitHealth refuse de démarrer plutôt que de
basculer silencieusement sur une interface réseau.

## Une autre instance utilise la base

Une seule instance peut écrire dans un même répertoire de données. Fermer l'autre
processus ou lancer la nouvelle instance avec un autre `--data-dir`. Ne supprimer le
fichier `githealth.db.instance.lock` qu'après avoir vérifié qu'aucun processus GitHealth
n'utilise cette base.

## Git est introuvable

Installer Git, vérifier que `git --version` fonctionne dans un nouveau terminal, puis
relancer GitHealth. Le diagnostic `/health` reste disponible et décrit cette erreur.

## Le dépôt est refusé ou inaccessible

- utiliser un chemin absolu et vérifier les droits de lecture ;
- en Docker, utiliser un chemin sous `/repositories` ;
- vérifier que le dossier monté contient bien le dépôt attendu ;
- pour un worktree lié, conserver le dépôt principal accessible ;
- éviter un lien symbolique qui sort de la racine autorisée du conteneur.

Un projet devenu inaccessible conserve son dernier snapshot réussi en consultation.
Ouvrir **Politiques**, puis **Relocaliser le dépôt** pour rattacher son nouveau chemin
sans perdre les analyses. La référence déjà configurée et le dernier commit de référence
connu doivent exister dans ce dépôt.

## Le projet est occupé pendant une relocalisation

GitHealth refuse de relocaliser un projet pendant son analyse et refuse de lancer une
analyse pendant sa relocalisation. Attendre la fin de l'opération, puis réessayer. Ce
verrou évite d'associer un snapshot à l'ancien chemin après le déplacement.

## Le nouveau chemin ne correspond pas au dépôt connu

Le code `repository.identity_mismatch` indique que le candidat ne contient pas le commit
de référence du dernier snapshot réussi. Sélectionner une autre copie du même dépôt ou
restaurer ce commit avant de relocaliser ; ne pas rattacher un historique à un dépôt sans
rapport.

## La référence ou une branche manque

GitHealth ne fait ni `fetch` ni `remote prune`. Mettre à jour volontairement le dépôt
avec les outils habituels, puis relancer l'analyse. Les branches de suivi distant sont
les références présentes localement sous `refs/remotes`.

## L'analyse dépasse la limite

Une commande Git trop lente, trop bavarde ou une file saturée est arrêtée avec une
erreur explicite. Vérifier l'intégrité du dépôt avec les outils Git, réduire le périmètre
des branches, puis réessayer. Les limites évitent qu'un dépôt hostile monopolise la
machine ; leur configuration est décrite dans [DEVOPS.md](DEVOPS.md).

## Le dernier résultat ne change pas après un échec

C'est le comportement attendu. La persistance est transactionnelle : seuls les scans
réussis remplacent le dernier snapshot. L'échec reste visible dans l'historique.

Après un arrêt brutal, une analyse restée en cours apparaît comme annulée avec le code
`analysis.interrupted` au démarrage suivant. Elle peut être relancée normalement.

## L'export CSV s'ouvre mal dans un tableur

Importer le fichier en UTF-8 et choisir la virgule comme séparateur. Les cellules qui
commencent comme une formule sont neutralisées intentionnellement afin d'éviter leur
exécution par le tableur.

## Restaurer une sauvegarde SQLite

1. Arrêter toutes les instances GitHealth visant la base.
2. Copier la base courante dans un emplacement sûr.
3. Remplacer `githealth.db` par le fichier exporté.
4. Redémarrer GitHealth et vérifier `/health`.

Ne pas remplacer la base pendant que l'application tourne. L'export fourni par
GitHealth est autonome et ne nécessite pas les fichiers SQLite `-wal` ou `-shm`.

## Docker ne démarre pas

Exécuter `docker compose config`, vérifier la valeur de
`GITHEALTH_REPOSITORIES_ROOT`, puis consulter `docker compose logs githealth`. Le volume
de données doit rester inscriptible, tandis que le montage `/repositories` doit rester
en lecture seule.

## macOS bloque l'exécutable

La release candidate n'est ni signée ni notariée. Vérifier l'archive et son empreinte,
puis autoriser explicitement le premier lancement depuis les réglages de sécurité. Une
signature et une notarisation sont prévues avant une diffusion large.
