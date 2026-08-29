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
      {
        path: 'analyses/:analysisId',
        loadComponent: () =>
          import('./features/dashboard/dashboard').then(({ Dashboard }) => Dashboard),
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
