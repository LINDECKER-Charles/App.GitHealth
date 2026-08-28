import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/home/home').then(({ Home }) => Home),
  },
  {
    path: 'projects/:projectId/history',
    loadComponent: () =>
      import('./features/analysis-history/analysis-history').then(
        ({ AnalysisHistory }) => AnalysisHistory,
      ),
  },
  {
    path: 'projects/:projectId/settings',
    loadComponent: () =>
      import('./features/project-settings/project-settings').then(
        ({ ProjectSettings }) => ProjectSettings,
      ),
  },
  {
    path: 'projects/:projectId/analyses/:analysisId',
    loadComponent: () =>
      import('./features/dashboard/dashboard').then(({ Dashboard }) => Dashboard),
  },
  {
    path: 'projects/:projectId',
    loadComponent: () =>
      import('./features/dashboard/dashboard').then(({ Dashboard }) => Dashboard),
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
