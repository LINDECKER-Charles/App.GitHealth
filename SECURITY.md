# Politique de sécurité

## Versions prises en charge

Pendant la phase release candidate, seule la dernière version `0.1.0-rc.*` reçoit des
correctifs de sécurité. Cette politique sera révisée à la première version stable.

## Signaler une vulnérabilité

Ne pas ouvrir d'issue publique avec une preuve d'exploitation, un chemin local ou des
données d'auteur. Utiliser la fonctionnalité **Security advisories** du dépôt GitHub pour
créer un signalement privé. Si elle n'est pas disponible, contacter le mainteneur par le
canal confidentiel habituel de l'organisation.

Inclure, si possible :

- la version et le mode d'exécution concernés ;
- les préconditions et étapes minimales de reproduction ;
- l'impact attendu, sans joindre de dépôt d'entreprise ;
- une proposition de correction ou de test de non-régression.

Un accusé de réception est visé sous trois jours ouvrés. La correction est priorisée
selon l'exploitabilité et l'impact sur les dépôts, les identités d'auteur et la base
locale. Une publication coordonnée est convenue avant toute divulgation.

## Périmètre de confiance

GitHealth est une application locale mono-utilisateur. L'exposition volontaire sur un
réseau, la modification du binaire, un système déjà compromis ou un logiciel exécuté
avec les mêmes droits que l'utilisateur sortent du modèle de menace du MVP. Les défauts
qui permettent néanmoins une écriture Git, une sortie de racine Docker, une lecture
inter-origine ou une exfiltration silencieuse restent dans le périmètre.
