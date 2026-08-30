# Limites connues de `0.1.0-rc.1`

- GitHealth analyse seulement les dépôts déjà présents ; il ne clone pas et ne gère pas
  les identifiants des forges.
- Aucun `fetch`, `pull` ou `remote prune` n'est lancé. Les références distantes peuvent
  être anciennes jusqu'à leur mise à jour volontaire par l'utilisateur.
- Le produit est local et mono-utilisateur. Il n'est pas conçu pour être exposé sur un
  LAN, Internet ou derrière un reverse proxy.
- Les archives macOS et l'installeur `.pkg` ne sont ni signés ni notariés. Gatekeeper
  peut demander une autorisation explicite au premier lancement.
- L'installeur Windows n'est pas signé. SmartScreen peut avertir au premier lancement,
  tant qu'un certificat de signature de code n'est pas en place.
- La mise à jour depuis l'application n'existe que sur Windows et macOS. Sur Linux,
  seules les archives portables sont publiées et la mise à jour reste manuelle.
- Sous Windows, la fenêtre a besoin du runtime WebView2. S'il est absent, l'application
  tente de le télécharger et peut s'arrêter sans message si ce téléchargement échoue.
- GitHealth n'embarque pas Git : un Git installé sur le poste reste nécessaire.
  `--git-path` désigne un exécutable situé hors du `PATH` et des emplacements standards.
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
