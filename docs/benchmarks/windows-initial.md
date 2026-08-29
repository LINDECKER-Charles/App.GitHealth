# Baseline initiale Windows

## Contexte

Cette baseline a été mesurée localement le 29 août 2026 avec le runner de l'étape 9.
Les échantillons bruts sont conservés dans `windows-initial.json`.

| Propriété | Valeur |
|---|---|
| OS | Microsoft Windows 10.0.26200 |
| Runtime | .NET 10.0.11, `win-x64`, processus X64 |
| Processeur | Intel64 Family 6 Model 186 Stepping 3, GenuineIntel |
| Processeurs logiques visibles | 12 |
| Git | 2.55.0.windows.5 |
| Commit de départ | `2a9575fe77adf8254a7e913414e0c9695de92ab8` |
| Chauffe | 1 itération par phase |
| Mesure | 3 itérations conservées par phase |

Le worktree était volontairement sale : il contenait l'implémentation non commitée
de l'étape 9 et les autres chantiers de durcissement exécutés en parallèle. Le commit
indiqué identifie donc la base de travail, tandis que le prochain commit de l'étape 9
figera le code exact du runner. Aucun autre programme intensif n'a été lancé par le
runner et les caches système n'ont pas été purgés.

## Commande

```powershell
dotnet run --project benchmarks/App.GitHealth.Benchmarks/App.GitHealth.Benchmarks.csproj `
  --configuration Release -- `
  --sizes 100,500,1000 --warmup 1 --iterations 3 `
  --output docs/benchmarks/windows-initial.json
```

## Résultats

Les durées sont en millisecondes. Avec trois mesures, le P95 correspond au maximum
observé. Le budget a été défini après cette baseline et n'apparaît donc pas comme
chargé dans le JSON brut.

| Branches | Phase | Médiane | P95 | Budget P95 |
|---:|---|---:|---:|---:|
| 100 | topologie | 161,818 | 171,515 | 300 |
| 100 | enrichissement | 9 226,789 | 9 772,377 | 15 000 |
| 100 | persistance | 105,441 | 110,714 | 200 |
| 100 | API | 17,398 | 21,527 | 50 |
| 500 | topologie | 334,706 | 350,410 | 550 |
| 500 | enrichissement | 47 319,656 | 49 585,567 | 75 000 |
| 500 | persistance | 367,854 | 459,746 | 750 |
| 500 | API | 43,664 | 55,180 | 100 |
| 1 000 | topologie | 506,596 | 548,262 | 850 |
| 1 000 | enrichissement | 94 344,122 | 98 289,182 | 150 000 |
| 1 000 | persistance | 290,797 | 328,163 | 750 |
| 1 000 | API | 53,210 | 64,459 | 125 |

Les fingerprints des fixtures sont :

| Branches | SHA-256 des références |
|---:|---|
| 100 | `5449a8ee4b1fd3ef6513f7e0c5d4365f9d97365a5cd2e078adf8b75026a1536c` |
| 500 | `22abc881b613ecdc007ab7b3544135497751827114b54a430441ebb3a72c9729` |
| 1 000 | `55078751d0dc13f7e6112378ba72cf30f8e2206a5276a2a209a2e67a798c96ff` |

## Interprétation

L'enrichissement domine : il représente plus de 98 % du temps à 1 000 branches et
évolue presque linéairement, autour de 94 ms par branche sur cet environnement. Cela
correspond au comportement actuel, un processus `git log` distinct pour chaque
commit de branche. Une future optimisation devra conserver l'exactitude mailmap et
la borne de sortie avant de réduire ce nombre de processus.

La topologie reste sous 550 ms à 1 000 branches grâce au chemin rapide
`for-each-ref ahead-behind`. La persistance présente davantage de variance à 500
branches, d'où des budgets monotones et arrondis. La phase API, qui recharge toute
l'analyse puis sérialise une page de 200 éléments, reste sous 65 ms dans cette série.

Les budgets ajoutent au moins 50 % au P95 observé. Ils ne constituent pas une cible
produit universelle : ils servent de garde-fou sur un environnement Windows
comparable. Le guide `docs/BENCHMARKING.md` détaille le protocole et ses limites.
