# Benchmarks GitHealth

## Objectif

Le runner mesure le coût du parcours d'analyse sur une fixture Git synthétique et
déterministe. Il couvre 100, 500 et 1 000 branches sans dépendre d'un dépôt privé.
Les résultats servent à détecter une régression, pas à comparer deux machines.

Le projet est volontairement un exécutable .NET sans framework de microbenchmark.
Les phases lancent Git et SQLite : leur durée est assez grande pour être mesurée par
`Stopwatch`, tandis qu'un runner simple conserve les mêmes chemins applicatifs.

## Fixture déterministe

Chaque scénario crée, hors mesure, un dépôt temporaire avec `git fast-import` :

- une branche locale `main` avec un commit de référence ;
- N références `refs/remotes/origin/benchmark/NNNN` ;
- un commit unique, directement en avance sur `main`, pour chaque référence ;
- des identités, dates, messages et contenus fixes ;
- un `.mailmap` local fixe.

Le rapport contient le SHA-256 de la liste triée `référence:commit`. Deux exécutions
avec la même version du générateur doivent obtenir le même fingerprint pour une
taille donnée. Le chemin temporaire et la création de la fixture sont exclus.
Le runner retire les redirections `GIT_*`, les traces et la configuration globale de
l'hôte avant chaque commande afin que la fixture reste dans son répertoire temporaire.

## Phases mesurées

- `topology` : de la lecture topologique sur les références déjà capturées aux
  divergences produites. La localisation et la capture initiale sont exclues.
- `enrichment` : d'un lecteur de contributeurs neuf, sans cache, jusqu'au
  `RepositoryScan` construit. Le résultat topologique est fourni en entrée.
- `persistence` : de la création d'une analyse à la transaction de complétion
  SQLite validée. La création du schéma et du projet est exclue.
- `api` : de la lecture du snapshot persisté au DTO paginé sérialisé en JSON.
  Kestrel, le réseau et le navigateur sont exclus.

L'enrichissement démarre un lecteur neuf à chaque itération. La fixture possède un
commit différent par branche : la mesure exécute donc un `git log` réel pour chaque
branche et ne transforme pas 1 000 branches en un seul accès mis en cache.

La persistance utilise une base neuve à chaque échantillon. Elle inclut les deux
écritures du flux réel (`StartAsync`, puis `CompleteAsync`) avec les branches et les
contributeurs. `EnsureCreated` et l'insertion du projet se produisent avant le
chronomètre.

La phase API recharge toute l'analyse depuis SQLite, classifie les branches, trie et
sérialise la première page de 200 éléments avec les options JSON du web. Elle mesure
le coût serveur du rendu, mais pas Angular, le navigateur, HTTP ou Kestrel.

## Exécution

Depuis la racine du dépôt :

```powershell
dotnet run --project benchmarks/App.GitHealth.Benchmarks `
  --configuration Release -- `
  --sizes 100,500,1000 `
  --warmup 1 `
  --iterations 3 `
  --output artifacts/benchmarks/latest.json
```

Smoke rapide :

```powershell
dotnet run --project benchmarks/App.GitHealth.Benchmarks `
  --configuration Release -- `
  --sizes 100 --warmup 0 --iterations 1
```

Le runner force un GC complet juste avant chaque échantillon conservé. Les caches du
système de fichiers et de Git ne sont pas purgés : la chauffe et les mesures
représentent un usage répété sur une machine déjà démarrée.

Pour obtenir des valeurs comparables :

1. utiliser le build `Release` et le SDK épinglé par `global.json` ;
2. fermer les tâches lourdes et désactiver la mise en veille pendant le run ;
3. conserver les mêmes nombres de chauffe et d'itérations ;
4. comparer le runtime, Git, l'OS, l'architecture et le processeur du rapport ;
5. vérifier les fingerprints avant de comparer les durées.

## Budgets

Les budgets absolus se trouvent dans `benchmarks/budgets.json`. Ils portent sur le
P95 de chaque couple taille/phase et ont été fixés après la première baseline
Windows. Le runner les affiche si le fichier existe.

L'option suivante retourne le code `2` lorsqu'un P95 dépasse son budget :

```powershell
dotnet run --project benchmarks/App.GitHealth.Benchmarks `
  --configuration Release -- --enforce-budgets
```

L'application de budgets absolus n'est pertinente que sur un agent de référence aux
caractéristiques stables. Sur une autre machine, conserver le résultat comme donnée
informative et comparer d'abord une baseline propre à cet agent.

Le workflow `benchmark.yml` conserve donc la mesure des runners GitHub hébergés comme
une donnée informative par défaut. Son lancement manuel propose l'option
`enforce_budgets` uniquement lorsqu'un runner comparable à la baseline est retenu. Cette
séparation évite qu'une variation de capacité d'un runner mutualisé bloque une fusion.

Pour réviser un budget :

1. mesurer avant toute modification du budget ;
2. expliquer la régression ou l'amélioration dans le rapport associé ;
3. garder une marge explicite au-dessus du P95 observé ;
4. modifier le JSON dans le même commit que le nouveau rapport validé.

## Baseline publiée

La baseline locale initiale et son interprétation sont disponibles dans
`docs/benchmarks/windows-initial.md`. Son JSON brut est conservé à côté du rapport.
Il contient les échantillons, l'environnement exact et l'état du worktree.

## Limites

- La fixture représente des branches courtes, toutes en avance d'un commit. Elle ne
  couvre pas un graphe profond, les sous-modules, Git LFS ou les clones partiels.
- Les processus Git dominent l'enrichissement sous Windows. Un antivirus ou un mode
  économie d'énergie peut modifier fortement les résultats.
- Le P95 de trois échantillons correspond au maximum observé. Une campagne de
  diagnostic doit augmenter `--iterations`.
- Le rendu côté navigateur doit être profilé séparément avec les outils du navigateur.
- La baseline ne remplace pas la recette sur de vrais dépôts volumineux.

Aucun dépôt distant n'est contacté et aucune identité réelle n'est utilisée. Le
runner ne transmet aucune donnée hors du poste.
