import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () =>
      import('./features/home/workspace-home').then(({ WorkspaceHome }) => WorkspaceHome),
  },
  {
    path: 'projects/:projectId',
    loadComponent: () =>
      import('./features/project/project-shell').then(({ ProjectShell }) => ProjectShell),
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/dashboard/dashboard').then(({ Dashboard }) => Dashboard),
      },
      {
        path: 'visualisation',
        loadComponent: () =>
          import('./features/visualisation/visualisation').then(
            ({ Visualisation }) => Visualisation,
          ),
        children: [
          { path: '', pathMatch: 'full', redirectTo: 'topologie' },
          {
            path: 'topologie',
            loadComponent: () =>
              import('./features/visualisation/topology/topology-view').then(
                ({ TopologyView }) => TopologyView,
              ),
          },
          {
            path: 'registre',
            loadComponent: () =>
              import('./features/visualisation/activity/activity-view').then(
                ({ ActivityView }) => ActivityView,
              ),
          },
          {
            path: 'ecart',
            loadComponent: () =>
              import('./features/visualisation/drift/drift-view').then(
                ({ DriftView }) => DriftView,
              ),
          },
        ],
      },
      {
        path: 'history',
        loadComponent: () =>
          import('./features/analysis-history/analysis-history').then(
            ({ AnalysisHistory }) => AnalysisHistory,
          ),
      },
      {
        path: 'settings',
        loadComponent: () =>
          import('./features/project-settings/project-settings').then(
            ({ ProjectSettings }) => ProjectSettings,
          ),
      },
    ],
  },
  {
    path: 'branches/:snapshotId',
    loadComponent: () =>
      import('./features/branch-details/branch-details').then(({ BranchDetails }) => BranchDetails),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
