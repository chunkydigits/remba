import { NgModule, provideBrowserGlobalErrorListeners } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authHttpInterceptorFn, provideAuth0 } from '@auth0/auth0-angular';
import { AppRoutingModule } from './app-routing-module';
import { App } from './app';
import { environment } from '../environments/environment';

const auth0Domain = environment.auth0.domain || 'configure-me.auth0.com';
const auth0ClientId = environment.auth0.clientId || 'configure-me';

@NgModule({
  declarations: [App],
  imports: [BrowserModule, AppRoutingModule],
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withInterceptors([authHttpInterceptorFn])),
    provideAuth0({
      domain: auth0Domain,
      clientId: auth0ClientId,
      authorizationParams: {
        redirect_uri: window.location.origin,
        audience: environment.auth0.audience || undefined,
      },
      httpInterceptor: {
        allowedList: [`${environment.apiBaseUrl}/api/*`],
      },
    }),
  ],
  bootstrap: [App],
})
export class AppModule {}
