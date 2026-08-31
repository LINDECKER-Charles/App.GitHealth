import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

/**
 * Entry point of the application graph, kept apart from `main.ts` so it can be imported
 * dynamically once the locale catalog is loaded. See the note in `main.ts`.
 */
export function bootstrap(): Promise<unknown> {
  return bootstrapApplication(App, appConfig);
}
