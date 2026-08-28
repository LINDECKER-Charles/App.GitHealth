# App.GitHealth.Web

Interface Angular de GitHealth. Le front fonctionne en serveur de développement
avec un proxy vers l’API, puis son build de production est publié dans `wwwroot`
par le projet ASP.NET Core.

## Prérequis

- Node.js `24.20.0` (version du fichier `.nvmrc` à la racine) ;
- npm `11.19.0`.

## Développement

Depuis ce dossier :

```shell
npm ci
npm start
```

Le serveur Angular écoute sur `http://localhost:4200` et transmet `/api`,
`/health` et `/openapi` à l’API locale sur `http://localhost:5115`.

## Vérifications

```shell
npm run test:ci
npm run build
```

Le build est produit dans `dist/app-git-health-web/browser`.
