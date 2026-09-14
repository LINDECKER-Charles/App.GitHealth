import { HttpClient, provideHttpClient, withXsrfConfiguration } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { routes } from './app.routes';
import { ExternalLinkInterceptor } from './core/desktop/external-link-interceptor';

const localSessionUrl = '/api/session';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    provideAppInitializer(() => firstValueFrom(inject(HttpClient).get<void>(localSessionUrl))),
    provideAppInitializer(() => inject(ExternalLinkInterceptor).start()),
    provideRouter(routes),
  ],
};
