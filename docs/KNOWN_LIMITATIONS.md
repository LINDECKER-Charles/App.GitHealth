# Limites connues de `0.1.0-rc.1`

- GitHealth analyse seulement les dépôts déjà présents ; il ne clone pas et ne gère pas
  les identifiants des forges.
- Aucun `fetch`, `pull` ou `remote prune` n'est lancé. Les références distantes peuvent
  être anciennes jusqu'à leur mise à jour volontaire par l'utilisateur.
- Le produit est local et mono-utilisateur. Il n'est pas conçu pour être exposé sur un
  LAN, Internet ou derrière un reverse proxy.
- Les archives macOS ne sont ni signées ni notariées. Gatekeeper peut demander une
  autorisation explicite au premier lancement.
- Il n'existe pas encore d'installeur, de mise à jour automatique ou de désinstallation
  guidée.
- Les dépôts de plusieurs milliers de branches peuvent nécessiter plusieurs minutes. Le
  rendu reste paginé, mais il n'utilise pas encore de virtualisation de lignes.
- L'activité d'une branche est approchée par la date de son commit de tête. Git ne stocke
  pas son intention de création ni l'historique partagé de tous les checkouts.
- Après une fusion, l'attribution des commits à leur branche d'origine peut devenir
  impossible. L'interface l'indique explicitement.
- `.mailmap` normalise les identités connues du dépôt. Sans ce fichier, une même personne
  utilisant plusieurs adresses peut apparaître plusieurs fois.
- GitHealth ne détecte pas les pull requests ouvertes et ne remplace pas les politiques
  de conservation de GitHub, GitLab ou Azure DevOps.
- Aucune opération de suppression, fusion, checkout ou push n'est proposée. Les
  recommandations doivent être vérifiées avant toute action réalisée hors de GitHealth.
- La protection locale limite les requêtes à la session et à l'origine générées au
  démarrage. Elle ne protège pas contre un logiciel malveillant exécuté avec les mêmes
  droits que l'utilisateur.
- Docker ne voit que les dépôts placés sous le montage `/repositories`. Les chemins du
  poste et ceux du conteneur ne sont pas interchangeables.
