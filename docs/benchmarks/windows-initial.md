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
| Commit mesuré | `177eede7d88311c7fee8ac1df1203c76987d499b` |
| État du worktree | propre (`sourceWorkingTreeDirty: false`) |
| Chauffe | 1 itération par phase |
| Mesure | 3 itérations conservées par phase |

Le runner a mesuré un worktree propre au commit indiqué. La configuration Git de
l'hôte a été neutralisée pour rendre la fixture déterministe. Aucun autre programme
intensif n'a été lancé par le runner et les caches système n'ont pas été purgés.

## Commande

```powershell
dotnet run --project benchmarks/App.GitHealth.Benchmarks/App.GitHealth.Benchmarks.csproj `
  --configuration Release -- `
  --sizes 100,500,1000 --warmup 1 --iterations 3 `
  --enforce-budgets `
  --output docs/benchmarks/windows-initial.json
```

## Résultats

Les durées sont en millisecondes. Avec trois mesures, le P95 correspond au maximum
observé. Les budgets ont été chargés et appliqués pendant cette exécution ; aucune
régression n'a été détectée.

| Branches | Phase | Médiane | P95 | Budget P95 |
|---:|---|---:|---:|---:|
| 100 | topologie | 123,976 | 124,632 | 300 |
| 100 | enrichissement | 7 531,697 | 7 629,140 | 15 000 |
| 100 | persistance | 80,450 | 103,648 | 200 |
| 100 | API | 6,781 | 9,622 | 50 |
| 500 | topologie | 234,446 | 262,796 | 550 |
| 500 | enrichissement | 37 836,940 | 38 569,893 | 75 000 |
| 500 | persistance | 208,476 | 324,947 | 750 |
| 500 | API | 17,019 | 18,490 | 100 |
| 1 000 | topologie | 539,290 | 562,748 | 850 |
| 1 000 | enrichissement | 76 420,966 | 79 111,705 | 150 000 |
| 1 000 | persistance | 126,663 | 143,766 | 750 |
| 1 000 | API | 38,164 | 39,476 | 125 |

Les fingerprints des fixtures sont :

| Branches | SHA-256 des références |
|---:|---|
| 100 | `5449a8ee4b1fd3ef6513f7e0c5d4365f9d97365a5cd2e078adf8b75026a1536c` |
| 500 | `22abc881b613ecdc007ab7b3544135497751827114b54a430441ebb3a72c9729` |
| 1 000 | `55078751d0dc13f7e6112378ba72cf30f8e2206a5276a2a209a2e67a798c96ff` |

## Interprétation

L'enrichissement domine : il représente plus de 98 % du temps à 1 000 branches et
évolue presque linéairement, autour de 76 ms par branche sur cet environnement. Cela
correspond au comportement actuel, un processus `git log` distinct pour chaque
commit de branche. Une future optimisation devra conserver l'exactitude mailmap et
la borne de sortie avant de réduire ce nombre de processus.

La topologie reste sous 563 ms à 1 000 branches grâce au chemin rapide
`for-each-ref ahead-behind`. La persistance présente davantage de variance à 500
branches, d'où des budgets monotones et arrondis. La phase API, qui recharge toute
l'analyse puis sérialise une page de 200 éléments, reste sous 40 ms dans cette série.

Les budgets ajoutent au moins 50 % au P95 observé. Ils ne constituent pas une cible
produit universelle : ils servent de garde-fou sur un environnement Windows
comparable. Le guide `docs/BENCHMARKING.md` détaille le protocole et ses limites.
