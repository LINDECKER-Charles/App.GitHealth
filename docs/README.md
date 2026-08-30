# Centre de documentation GitHealth

> Les faits Git avant la décision.

Ce centre oriente chaque lecteur vers le document qui répond à son intention. Les guides
d'utilisation partent d'une action concrète ; les documents techniques remontent jusqu'aux
preuves, aux limites et aux choix structurants.

## Utiliser GitHealth

| Besoin | Document | Résultat attendu |
|---|---|---|
| Installer et lancer l'application | [README](../README.md#04--démarrer-en-quelques-minutes) | Un premier diagnostic local |
| Ajouter, scanner et organiser des dépôts | [Guide utilisateur](USER_GUIDE.md) | Un workspace prêt à analyser |
| Comprendre une branche et son verdict | [Guide utilisateur](USER_GUIDE.md#expliquer-une-branche) | Une décision reliée aux faits Git |
| Résoudre un incident | [Dépannage](TROUBLESHOOTING.md) | Un diagnostic ciblé et une marche à suivre |
| Vérifier une limite de la RC | [Limites connues](KNOWN_LIMITATIONS.md) | Une attente produit explicite |

## Exploiter et distribuer

| Besoin | Document | Preuve associée |
|---|---|---|
| Publier une archive native | [DevOps](DEVOPS.md) | Sommes SHA-256, SBOM et smoke tests |
| Déployer avec Docker Compose | [DevOps](DEVOPS.md#docker-compose) | Montage read-only et conteneur durci |
| Préparer une release candidate | [Checklist de release](RELEASE_CHECKLIST.md) | Matrice de qualification |
| Relire la première RC | [Rapport 0.1.0-rc.1](release/0.1.0-rc.1.md) | Résultats de recette versionnés |

## Comprendre le système

| Angle | Document | Question centrale |
|---|---|---|
| Domaine et flux | [Architecture](ARCHITECTURE.md) | Comment les faits deviennent-ils un verdict ? |
| Frontière de confiance | [Modèle de sécurité](SECURITY_MODEL.md) | Que lit GitHealth, et que refuse-t-il de faire ? |
| Revue indépendante du code | [Audit de sécurité](SECURITY_AUDIT.md) | Quels risques restent ouverts ? |
| Performance | [Benchmarks](BENCHMARKING.md) | Comment détecter une régression mesurable ? |
| Construction du MVP | [Plan d'implémentation](IMPLEMENTATION_PLAN.md) | Comment le produit a-t-il été découpé et qualifié ? |

## Contribuer

- [Guide de contribution](../.github/CONTRIBUTING.md) — environnement, conventions, tests et PR ;
- [Code de conduite](../.github/CODE_OF_CONDUCT.md) — cadre de collaboration ;
- [Support](../.github/SUPPORT.md) — choisir le bon canal et fournir un cas reproductible ;
- [Politique de sécurité](../.github/SECURITY.md) — signaler une vulnérabilité sans l'exposer ;
- [Journal des changements](../CHANGELOG.md) — suivre les capacités livrées ;
- [Direction éditoriale et visuelle](ART_DIRECTION.md) — prolonger l'identité sans la diluer.

## Trois parcours de lecture

### Je découvre le produit

1. [README](../README.md)
2. [Guide utilisateur](USER_GUIDE.md)
3. [Limites connues](KNOWN_LIMITATIONS.md)

### Je dois l'évaluer techniquement

1. [Architecture](ARCHITECTURE.md)
2. [Modèle de sécurité](SECURITY_MODEL.md)
3. [Benchmarks](BENCHMARKING.md)
4. [Audit de sécurité](SECURITY_AUDIT.md)

### Je prépare une livraison

1. [DevOps](DEVOPS.md)
2. [Checklist de release](RELEASE_CHECKLIST.md)
3. [Recette 0.1.0-rc.1](release/0.1.0-rc.1.md)
4. [Changelog](../CHANGELOG.md)

---

Toute affirmation sensible doit pouvoir remonter à un test, un contrôle d'architecture,
une mesure ou une limite documentée. Si une page ne permet plus ce trajet, elle doit être
mise à jour en même temps que le comportement qu'elle décrit.
