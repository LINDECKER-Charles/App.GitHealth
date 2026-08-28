# Plan d'implémentation de GitHealth

> Statut : exécution en cours — étapes 1 à 7 terminées le 29 août 2026
> Architecture de référence : [`ARCHITECTURE.md`](../ARCHITECTURE.md)

## Objectif du MVP

Livrer une application locale qui démarre depuis un seul exécutable ou avec
`docker compose up`, analyse sans le modifier un dépôt Git existant, compare ses
branches à une référence et conserve des snapshots consultables dans SQLite.

Le plan privilégie des incréments verticaux démontrables. Chaque étape doit laisser
la branche compilable et testée ; aucune étape ne dépend d'une interface simulée
qui ne serait jamais reliée au produit final.

## Règles d'exécution

- Une fonctionnalité et ses tests sont livrés ensemble.
- Les commandes Git restent en lecture seule pendant tout le MVP.
- Les métriques affichées sont accompagnées de leur définition.
- Les erreurs attendues deviennent des résultats métier ou des Problem Details.
- Les versions .NET, Node et npm sont verrouillées dès le socle.
- Les mesures de performance précèdent toute optimisation complexe.
- Le dernier snapshot réussi reste visible si une nouvelle analyse échoue.

## Étape 1 — Construire le socle exécutable

> Statut : terminée

### Résultat attendu

Un squelette vide démarre depuis un seul processus ASP.NET Core, sert Angular et
répond à un endpoint de santé. Le même artefact fonctionne en mode natif et dans
un conteneur.

### Travaux

1. Créer la solution .NET et verrouiller le SDK .NET 10.
2. Créer `App.GitHealth.Core`, `App.GitHealth.Api` et leurs projets de tests.
3. Initialiser Angular 22 en mode standalone, TypeScript strict et styles SCSS.
4. Organiser le front par fonctionnalités et configurer son proxy de développement.
5. Intégrer le build Angular aux fichiers statiques publiés par ASP.NET Core.
6. Ajouter `/health`, OpenAPI et une page d'accueil technique minimale.
7. Configurer les règles communes de compilation et d'analyse statique.
8. Créer une image multi-stage et un `compose.yaml` à service unique.
9. Ajouter un volume de données et un montage de dépôts en lecture seule.
10. Créer une CI minimale : restore, build et tests sur chaque pull request.

### Vérifications de sortie

- [x] `dotnet test` réussit depuis la racine.
- [x] Le build de publication contient l'application Angular.
- [x] L'ouverture de l'URL racine ne nécessite pas un serveur Node séparé.
- [x] `docker compose up --build` expose uniquement `127.0.0.1:8080`.
- [x] Le conteneur redémarre sans perdre un fichier témoin placé dans `/data`.

## Étape 2 — Formaliser le domaine d'analyse

> Statut : terminée le 28 août 2026

### Résultat attendu

Les métriques et recommandations existent sous forme de règles métier pures,
indépendantes de Git, de SQLite et de l'API.

### Travaux

1. Créer les types `Project`, `GitRef`, `CommitId` et `BranchComparison`.
2. Ne supposer aucune longueur d'identifiant d'objet Git.
3. Modéliser séparément topologie, activité et recommandation.
4. Introduire une horloge injectable pour les calculs d'ancienneté.
5. Définir les seuils par défaut et leur validation.
6. Gérer les motifs exclus/protégés sans les confondre avec un état Git.
7. Définir les contrats du scanner et les erreurs fonctionnelles.

### Matrice de tests

- avance et retard nuls : synchronisée ;
- avance seule : en avance ;
- retard seul avec ancêtre : fusionnée ;
- avance et retard : divergente ;
- aucun ancêtre commun : état explicite, pas une erreur générique ;
- seuils exactement atteints et franchis ;
- branche protégée jamais candidate au nettoyage ;
- branche inactive non fusionnée signalée à examiner, jamais à supprimer.

## Étape 3 — Lire un dépôt avec Git

> Statut : terminée le 28 août 2026

### Résultat attendu

Un adaptateur testé valide un dépôt, liste ses références et calcule une comparaison
exacte sans toucher à son index, son worktree ou ses refs.

### Travaux

1. Implémenter un exécuteur de processus sans shell avec timeout et annulation.
2. Détecter Git au démarrage et exposer un diagnostic compréhensible.
3. Reconnaître dépôts standards, bare repositories et worktrees liés.
4. Canonicaliser le chemin et retrouver le répertoire de travail/Git effectif.
5. Lister `refs/heads/*` et `refs/remotes/*`, en excluant les pseudo-références.
6. Détecter `origin/HEAD`, puis proposer `main` ou `master` en repli.
7. Capturer les SHA avant toute comparaison.
8. Implémenter le chemin rapide `for-each-ref` avec `ahead-behind`.
9. Implémenter le repli `rev-list --left-right --count` à concurrence bornée.
10. Détecter la fusion par relation d'ancêtre et les historiques sans base commune.
11. Lire la date et l'auteur du sommet de chaque branche.
12. Agréger les auteurs de `référence..branche`, hors merges et avec mailmap.
13. Mettre en cache l'enrichissement par SHA de référence et SHA de branche.

### Fixtures Git d'intégration

Construire les dépôts dans un répertoire temporaire avec de vrais commits :

- branche identique à `main` ;
- branche en avance de plusieurs auteurs ;
- branche seulement en retard et entièrement fusionnée ;
- branche divergente ;
- branche avec merge de la référence ;
- branche dont les dates sont anciennes ;
- branche sans ancêtre commun ;
- noms valides contenant slashs, caractères sensibles à l'échappement ou non ASCII ;
- `.mailmap` regroupant deux identités.

### Vérifications de sortie

- Les résultats correspondent aux commandes Git de référence.
- Une version Git sans atome `ahead-behind` utilise le chemin de repli.
- Une annulation termine tous les processus enfants.
- Les références, l'index et le worktree sont identiques avant et après le scan.
- Une sortie ou un nom de branche malformé produit une erreur maîtrisée.

## Étape 4 — Persister projets et snapshots

> Statut : terminée le 28 août 2026

### Résultat attendu

Les configurations et analyses survivent à un redémarrage et une sauvegarde SQLite
peut être exportée pendant que l'application fonctionne.

### Travaux

1. Ajouter EF Core SQLite et créer le premier schéma.
2. Mapper projets, analyses, branches et contributeurs.
3. Stocker toutes les dates en UTC et les références sous leur nom complet.
4. Activer les clés étrangères, WAL et un délai d'attente d'écriture.
5. Créer les dépôts d'accès aux données nécessaires aux cas d'usage.
6. Persister une exécution et ses snapshots par lots transactionnels.
7. Ne promouvoir que les analyses terminées comme dernier résultat réussi.
8. Gérer la relocalisation d'un chemin devenu inaccessible.
9. Créer le service d'export avec l'API de sauvegarde SQLite.
10. Prévoir une politique de rétention configurable, désactivée par défaut au MVP.

### Vérifications de sortie

- Les migrations fonctionnent sur une base vide et une base déjà initialisée.
- L'arrêt au milieu d'un scan ne corrompt ni le scan précédent ni la base.
- Deux écritures concurrentes respectent le délai et remontent une erreur contrôlée.
- La sauvegarde exportée s'ouvre seule, sans dépendre d'un fichier WAL séparé.
- Un projet relocalisé conserve ses analyses historiques.

## Étape 5 — Exposer les cas d'usage par l'API

> Statut : terminée le 29 août 2026

### Résultat attendu

L'API permet d'enregistrer un projet, de configurer sa référence, de lancer une
analyse et de consulter sa progression et ses résultats.

### Travaux

1. Implémenter la validation d'un chemin et la découverte des références.
2. Implémenter la création et la liste des projets.
3. Implémenter la modification de la référence, des seuils et des exclusions.
4. Créer une file d'analyses avec un worker ASP.NET Core.
5. Refuser ou dédupliquer deux analyses simultanées du même projet.
6. Retourner `202 Accepted` et une URL de suivi au lancement.
7. Exposer la progression : attente, topologie, enrichissement, persistance, fin.
8. Exposer les snapshots paginés, triés et filtrés côté serveur.
9. Exposer le détail d'un snapshot et ses contributeurs.
10. Ajouter l'endpoint de sauvegarde SQLite.
11. Uniformiser les erreurs avec Problem Details et codes stables.

### Vérifications de sortie

- Tests d'API en mémoire avec une vraie base SQLite temporaire.
- Validation des chemins inexistants, non-Git et non autorisés.
- Retour du dernier résultat réussi pendant un scan ou après un échec.
- Pagination stable même si plusieurs branches portent des noms proches.
- Annulation propre lors de l'arrêt de l'hôte.

## Étape 6 — Livrer le parcours projet et le tableau de bord

> Statut : terminée le 29 août 2026

### Résultat attendu

Un utilisateur peut réaliser le parcours principal sans connaître les commandes
Git et comprendre immédiatement quelles branches demandent son attention.

### Travaux

1. Créer l'écran d'accueil et la liste des projets récents.
2. Créer l'ajout par saisie de chemin et, en mode natif, un explorateur de dossiers.
3. Afficher clairement la racine autorisée en mode Docker.
4. Proposer la référence détectée tout en permettant sa modification.
5. Afficher la provenance des données : local/distant et heure du scan.
6. Créer le lancement d'analyse et le suivi de progression par polling.
7. Créer le tableau des branches avec :
   - nom et espace de référence ;
   - avance et retard ;
   - état de fusion ;
   - dernière activité et ancienneté ;
   - auteur principal lorsqu'il est déterminable ;
   - recommandation et justification.
8. Ajouter tri, recherche, filtres combinables et pagination/virtualisation.
9. Conserver les filtres utiles dans l'URL.
10. Gérer les vues vide, chargement, erreur et dépôt devenu inaccessible.

### Vérifications de sortie

- Le parcours complet fonctionne au clavier.
- Les informations ne reposent pas uniquement sur la couleur.
- Un tableau de 1 000 lignes reste navigable sans tout rendre dans le DOM.
- Les noms longs et caractères non ASCII ne cassent pas la mise en page.
- Une erreur d'analyse n'efface pas le résultat précédent.

## Étape 7 — Expliquer les branches et configurer les politiques

> Statut : terminée le 29 août 2026

### Résultat attendu

Chaque recommandation est vérifiable dans une fiche de branche et les règles
peuvent être adaptées aux conventions du dépôt.

### Travaux

1. Créer la page de détail d'un snapshot.
2. Afficher la définition d'avance, retard et activité.
3. Afficher les contributeurs, leur nombre de commits et la prise en compte du mailmap.
4. Signaler explicitement une attribution impossible après fusion.
5. Afficher les SHA utilisés et l'heure de capture.
6. Créer l'édition des seuils active/vieillissante/inactive.
7. Créer les motifs d'exclusion et de protection avec aperçu des correspondances.
8. Ajouter les filtres rapides : fusionnées, inactives, divergentes, à examiner.
9. Ajouter un export CSV de la vue filtrée, distinct de la sauvegarde SQLite.
10. Consulter l'historique des analyses d'un projet.

### Vérifications de sortie

- Modifier une politique recalcule l'interprétation sans falsifier les faits Git.
- Une branche protégée affiche la raison exacte de son exclusion.
- Le CSV respecte le filtre, l'encodage UTF-8 et les noms internationaux.
- Les données historiques restent rattachées aux politiques utilisées lors du scan.

## Étape 8 — Finaliser les points d'entrée Windows, macOS et Docker

### Résultat attendu

Les trois distributions lancent le même produit et utilisent les mêmes migrations
et contrats d'API.

### Travaux

1. Ajouter le lanceur au processus ASP.NET Core.
2. Choisir un port loopback disponible et ouvrir le navigateur par défaut.
3. Implémenter `--repo`, `--port`, `--data-dir` et `--no-browser`.
4. Déterminer les répertoires de données conformes à Windows et macOS.
5. Produire les publications autonomes pour les architectures supportées.
6. Vérifier l'arrêt gracieux et l'absence de processus Git orphelin.
7. Finaliser l'image Docker avec Git installé et utilisateur non privilégié.
8. Documenter `.env`, les chemins Windows/macOS et le montage `:ro`.
9. Ajouter les smoke tests natifs et Docker à la matrice de publication.
10. Fournir un diagnostic de démarrage pour Git absent, port indisponible ou DB invalide.

### Vérifications de sortie

- Un seul lancement démarre l'API et l'interface sur chaque plateforme.
- Deux instances ne corrompent pas la même base et signalent clairement le conflit.
- Le serveur n'écoute jamais sur le réseau sans option explicite future.
- Le conteneur n'écrit pas dans le dépôt monté.
- Les mêmes fixtures produisent les mêmes métriques sur les trois environnements.

## Étape 9 — Durcir, mesurer et préparer la première version

### Résultat attendu

Le MVP est reproductible, documenté et suffisamment robuste pour être testé sur
de vrais dépôts d'entreprise.

### Travaux

1. Générer un benchmark reproductible de 100, 500 et 1 000 branches.
2. Mesurer séparément topologie, enrichissement, persistance et rendu.
3. Fixer les budgets après la première baseline et les versionner.
4. Vérifier les limites de temps, sortie et concurrence des processus Git.
5. Tester traversée de chemins, liens symboliques et arguments Git hostiles.
6. Ajouter protection de l'origine, anti-forgery et jeton de session local.
7. Vérifier qu'aucune donnée d'auteur n'est transmise hors du poste.
8. Ajouter un scénario bout en bout du lancement à l'export.
9. Compléter README, guide utilisateur, dépannage et limites connues.
10. Produire une release candidate et la tester sur deux dépôts réels volumineux.

### Critères de sortie du MVP

- Installation et lancement démontrés sur Windows, macOS et Docker.
- Exactitude comparée manuellement à Git sur la suite de fixtures.
- Aucun changement observé dans les dépôts analysés.
- Parcours ajout → configuration → analyse → détail → export fonctionnel.
- Redémarrage et migration sans perte du dernier snapshot réussi.
- Erreurs Git fréquentes compréhensibles sans consulter les logs techniques.
- Résultats d'un benchmark de 1 000 branches publiés dans le dépôt.

## Scénario de recette principal

1. Lancer GitHealth avec le point d'entrée de la plateforme.
2. Ajouter un dépôt fixture contenant au moins six topologies de branche.
3. Choisir `main` comme référence et `refs/remotes/origin/*` comme périmètre.
4. Lancer l'analyse et suivre sa progression.
5. Vérifier avance/retard avec `git rev-list --left-right --count`.
6. Filtrer les branches fusionnées et inactives.
7. Ouvrir une branche divergente et contrôler ses contributeurs.
8. Protéger un motif, modifier un seuil et vérifier la nouvelle interprétation.
9. Exporter CSV et SQLite.
10. Redémarrer l'application et retrouver le dernier résultat.
11. Comparer refs, index et worktree avec leur état avant la recette.

## Risques à suivre

| Risque | Traitement prévu |
|---|---|
| Références distantes obsolètes | Afficher que l'analyse ne fait pas de fetch et dater le scan |
| Git ancien sur macOS | Détection de capacités et chemin de repli |
| Milliers de branches | Calcul groupé, enrichissement différé, cache et virtualisation |
| Branche fusionnée sans attribution fiable | Afficher indéterminé et conserver l'historique antérieur |
| Chemins Docker différents du poste | Racine explicite, canonicalisation et relocalisation |
| Copie SQLite incohérente | Endpoint utilisant l'API de sauvegarde |
| Commande hostile issue d'un nom de branche | Aucun shell, arguments séparés et tests dédiés |
| Blocage macOS d'un binaire non signé | Documenter le MVP puis traiter signature/notarisation avant diffusion large |

## Après le MVP

Priorité proposée :

1. Clones miroirs gérés et `fetch` explicite sans toucher aux worktrees.
2. Intégrations aux fournisseurs Git et détection des pull requests ouvertes.
3. Regroupement manuel des identités d'auteur.
4. Tendances entre snapshots et notifications locales.
5. Installeurs signés et mise à jour automatique.

La suppression distante d'une branche reste volontairement hors de GitHealth tant
qu'un workflow d'approbation et une intégration fournisseur ne sont pas conçus.
