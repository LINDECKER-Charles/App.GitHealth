# Obtenir de l'aide

GitHealth est un projet libre maintenu sur du temps limité. Il n'existe ni support
commercial, ni engagement de délai. Ce document indique où chercher une réponse et
comment poser une question pour qu'elle puisse être traitée.

## Commencer par la documentation

La majorité des questions ont déjà une réponse écrite :

| Question | Document |
| --- | --- |
| Comment installer, lancer et utiliser GitHealth | [Guide utilisateur](../docs/USER_GUIDE.md) |
| Ça ne démarre pas, le port est pris, Git manque | [Dépannage](../docs/TROUBLESHOOTING.md) |
| Pourquoi ce résultat est-il surprenant | [Limites connues](../docs/KNOWN_LIMITATIONS.md) |
| Comment les mesures sont-elles calculées | [Architecture](../docs/ARCHITECTURE.md) |
| Ce que l'application lit, écrit et n'envoie pas | [Sécurité](../docs/SECURITY_MODEL.md) |
| Publication native, Docker, exploitation | [DEVOPS](../docs/DEVOPS.md) |
| Ce qui a changé d'une version à l'autre | [Changelog](../CHANGELOG.md) |

Beaucoup de comportements déroutants ne sont pas des bugs mais des conséquences de la
sémantique de Git : une branche fusionnée dont l'attribution des commits devient
impossible, une branche distante figée parce que GitHealth ne lance jamais `fetch`, un
contributeur qui apparaît deux fois faute de `.mailmap`. Les limites connues expliquent
chacun de ces cas.

## Choisir le bon canal

| Vous avez | Canal |
| --- | --- |
| Une question d'usage | **GitHub Discussions**, ou une issue si les discussions sont fermées |
| Un bug reproductible | Issue **Rapport de bug** |
| Une idée de fonctionnalité | Issue **Proposition de fonctionnalité** |
| Une documentation fausse ou incomplète | Issue **Documentation** |
| Une faille de sécurité | **Jamais une issue publique** — [SECURITY.md](SECURITY.md) |
| Une envie de contribuer | [CONTRIBUTING.md](CONTRIBUTING.md) |

Une faille de sécurité se signale en privé, par les **Security advisories** du dépôt.
Cela couvre notamment toute écriture Git non voulue, toute sortie de la racine montée en
Docker, toute lecture inter-origine et toute transmission de données vers l'extérieur.

## Ce qui rend une demande traitable

Sans ces éléments, une question reste le plus souvent sans réponse utile :

- la **version** de GitHealth — nom de l'archive téléchargée, tag de la release, ou
  balise de l'image Docker utilisée ;
- le **mode d'exécution** : exécutable natif Windows, natif macOS, ou Docker Compose ;
- le **système** et sa version, ainsi que la sortie de `git --version` ;
- ce que vous **attendiez**, ce que vous avez **obtenu**, et les étapes minimales pour le
  reproduire ;
- les messages d'erreur de la console, **anonymisés**.

La forme du dépôt compte souvent plus que sa taille : dépôt standard, *bare*, worktree
lié, nombre approximatif de branches, présence d'un `.mailmap`.

## Ce qu'il ne faut jamais publier

GitHealth manipule des données qui identifient des personnes et des projets. Dans une
issue publique, ne joignez jamais :

- un chemin de dépôt d'entreprise ou de client ;
- des noms de branches internes ;
- des noms ou adresses d'auteurs issus de l'historique ;
- un export CSV ou une sauvegarde SQLite non expurgés.

Anonymisez avant de publier : `D:\Dev\ClientX\facturation` devient `D:\Dev\depot`,
`feature/JIRA-4210-refonte-paiement` devient `feature/exemple`. Si le problème n'est
reproductible qu'avec des données réelles, dites-le dans l'issue plutôt que de les
joindre — un échange privé sera proposé.

Un dépôt de reproduction minimal, fabriqué à partir de commits vides, vaut mieux que
n'importe quel extrait de dépôt réel.

## Délais

Il n'y a pas de délai garanti. Les priorités, dans l'ordre :

1. les failles de sécurité, en particulier celles qui touchent à l'intégrité d'un dépôt
   ou à la confidentialité des identités d'auteur ;
2. les régressions par rapport à la version publiée précédente ;
3. les bugs de calcul — une recommandation ou une mesure fausse ;
4. le reste.

Une issue sans réponse n'est pas une issue refusée. Une relance après quelques semaines
est légitime.

## Hors périmètre

Certaines demandes seront closes sans être traitées, non par manque d'intérêt mais parce
qu'elles sortent du produit : supprimer ou fusionner des branches, lancer `fetch`
automatiquement, cloner un dépôt distant, s'intégrer aux API de GitHub, GitLab ou Azure
DevOps, ou exposer une instance partagée sur un réseau. Les raisons de ces frontières
sont documentées dans [ARCHITECTURE.md](../docs/ARCHITECTURE.md).
