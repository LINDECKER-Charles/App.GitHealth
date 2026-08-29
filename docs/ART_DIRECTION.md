# Direction éditoriale et visuelle

Ce document fixe la manière dont GitHealth se présente dans le README, la documentation,
les captures de release et les futurs supports publics. Il prolonge le design system
**Établi** de l'application ; il n'introduit pas une seconde marque.

## Positionnement

**Phrase maîtresse**

> Les faits Git avant la décision.

**Promesse courte**

> Voyez quelles branches comptent encore — sans toucher au dépôt.

**Signature**

> GitHealth observe. Vous gardez la décision.

GitHealth n'est pas vendu comme un score magique, un robot de nettoyage ou une forge
supplémentaire. C'est un poste de diagnostic local qui rend une situation Git lisible,
explique son interprétation et laisse l'action finale à l'utilisateur.

## Concept : le dossier de diagnostic

La documentation suit la même chaîne que le produit :

```text
fait observé  →  interprétation  →  politique  →  verdict  →  action humaine
```

Chaque promesse importante doit pouvoir remonter à sa preuve. Un visuel de verdict montre
donc au minimum le signal, la règle appliquée, la recommandation et la part d'incertitude.
Le vocabulaire d'enquête sert la traçabilité, sans dramatisation judiciaire ni certitude
médicale excessive.

Le concept se décline en trois familles :

1. **Dossier** — numéros, références, horodatages et informations reproductibles.
2. **Anatomie** — lignes fines reliant les faits au verdict, jamais un score isolé.
3. **Registre des refus** — les frontières du produit rendues visibles et vérifiables.

## Grammaire visuelle

### Palette

| Rôle | Couleur | Usage |
|---|---|---|
| Graphite | `#1a1815` / `#fcfbf9` | Châssis, texte, surfaces calmes |
| Laiton | `#a87b27` / `#d9b25f` | Marque, topologie, points de conclusion |
| Cobalt | `#2e45c9` / `#6b82e8` | Interaction, preuve active, impulsion |
| Vert | `#157f4b` / `#93cfae` | État sûr, lecture locale, succès |
| Ambre | `#b45b09` / `#f3c37c` | Attention et examen nécessaire |
| Rouge | `#c0322b` / `#f0b0a9` | Danger avéré, jamais simple décoration |

Les valeurs complètes restent définies dans
[`_colors.scss`](../src/App.GitHealth.Web/src/styles/ds/tokens/_colors.scss). Les supports
ne créent pas une nouvelle nuance si un jeton existant exprime déjà l'intention.

### Typographie

- **IBM Plex Sans** porte les titres, les explications et la voix éditoriale.
- **IBM Plex Mono** porte les références, métriques, commandes, versions et preuves.
- Les grands titres sont courts, fermes et légèrement serrés.
- Le mono n'est jamais utilisé sur un paragraphe entier : il marque la donnée, pas la voix.

Une image SVG doit garder une pile de repli système, car GitHub ne charge pas les polices
locales de l'application dans tous les contextes.

### Formes et composition

- grands aplats calmes, grille technique discrète et densité maîtrisée ;
- lignes de topologie arrondies, points creux et impulsion cobalt au point d'analyse ;
- cartes peu élevées, rayon mesuré, bordures plus présentes que les ombres ;
- numérotation `01`, `02`, `03` pour construire un dossier, pas pour décorer ;
- beaucoup d'espace autour d'un verdict, peu autour des données qui le prouvent ;
- variantes claire et sombre obligatoires pour toute hero ou capture principale.

## Architecture du README

Le README suit un entonnoir de décision :

1. **Promesse** — comprendre la valeur en moins de dix secondes.
2. **Preuve visuelle** — voir le vrai produit sur un vrai diagnostic.
3. **Capacités** — vérifier que le besoin fonctionnel est couvert.
4. **Frontière** — comprendre pourquoi le local et le read-only sont crédibles.
5. **Activation** — lancer GitHealth sans parcourir toute la documentation.
6. **Profondeur** — rejoindre architecture, sécurité, benchmarks ou contribution.

Les badges ne remplacent jamais une phrase de positionnement. Les tableaux servent les
comparaisons exactes ; les listes servent les capacités ; les alertes GitHub sont réservées
aux limites qui modifient réellement une décision d'installation.

## Voix

La voix de GitHealth est calme, précise et adulte.

| Faire | Éviter |
|---|---|
| « GitHealth explique pourquoi cette branche demande une revue. » | « Une IA révolutionnaire nettoie vos branches. » |
| « Aucune écriture Git n'est exécutée. » | « 100 % safe. » |
| « Les références peuvent être anciennes sans fetch volontaire. » | Cacher une limite dans une note de bas de page. |
| « Candidate au nettoyage manuel. » | « Branche morte » ou « supprimer maintenant ». |

Les verbes préférés sont **lire**, **observer**, **comparer**, **expliquer**, **proposer**
et **vérifier**. Les verbes **guérir**, **juger** et **nettoyer automatiquement** sont
écartés : ils dépassent le comportement réel.

## Captures et démonstrations

- utiliser un scénario déterministe ou le dépôt GitHealth lui-même ;
- ne montrer aucune information qui ne serait pas publiable dans le dépôt ;
- conserver le même viewport pour les variantes claire et sombre ;
- laisser visibles référence, métriques, topologie et recommandation ;
- légender la capture par le scénario, pas par « screenshot de l'application » ;
- régénérer les captures quand le parcours ou la sémantique change.

Le duo actuel vit dans [`docs/assets/readme`](assets/readme). Le README sélectionne la
bonne variante avec `<picture>` et `prefers-color-scheme`.

## Pièges écartés

- **Cabinet médical** : immédiat mais trop cliché et trop certain pour des heuristiques.
- **Métaphores biologiques savantes** : originales, mais plus longues à expliquer que Git.
- **README entièrement navigué par la topologie** : spectaculaire, instable et peu scannable.
- **Refus en promesse principale** : crédible, mais vend les limites avant la valeur.
- **Grammaire de sceaux partout** : la traçabilité devient vite une surcharge visuelle.

Le choix final combine le dossier probatoire, l'anatomie du verdict et le registre des
refus. La piste plus expérimentale — plusieurs supports générés depuis un même scénario
de dépôt exécutable — reste une évolution possible si les captures deviennent automatisées.

## Maintenance

Lorsqu'une capacité publique change :

1. mettre à jour la phrase de valeur ou la limite concernée ;
2. vérifier le lien vers la preuve technique ;
3. régénérer la capture si le parcours visible change ;
4. contrôler les deux thèmes ;
5. relire le README à largeur mobile et desktop ;
6. joindre la documentation au même changement que le code.

Une direction artistique réussie ne masque pas la réalité du produit : elle rend sa
rigueur immédiatement perceptible.
