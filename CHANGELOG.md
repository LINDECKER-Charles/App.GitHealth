# Journal des changements

Toutes les évolutions notables de GitHealth sont consignées dans ce fichier. Le format
suit Keep a Changelog et le versionnage sémantique.

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
