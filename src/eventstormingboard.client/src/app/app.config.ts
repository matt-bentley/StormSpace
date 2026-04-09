import { ApplicationConfig, Provider, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes, authRoutes } from './app.routes';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { AuthConfig } from './_shared/models/auth-config.model';
import {
  MSAL_GUARD_CONFIG,
  MSAL_INSTANCE,
  MSAL_INTERCEPTOR_CONFIG,
  MsalBroadcastService,
  MsalGuard,
  MsalInterceptor,
  MsalService,
} from '@azure/msal-angular';
import {
  InteractionType,
  PublicClientApplication,
} from '@azure/msal-browser';

function createMsalProviders(config: AuthConfig, instance: PublicClientApplication): Provider[] {
  const protectedResourceMap = new Map<string, string[]>();
  protectedResourceMap.set('/api/*', config.scopes ?? []);
  protectedResourceMap.set('/hub/*', config.scopes ?? []);

  return [
    { provide: MSAL_INSTANCE, useValue: instance },
    { provide: MSAL_GUARD_CONFIG, useValue: { interactionType: InteractionType.Redirect, authRequest: { scopes: config.scopes ?? [] } } },
    { provide: MSAL_INTERCEPTOR_CONFIG, useValue: { interactionType: InteractionType.Redirect, protectedResourceMap } },
    { provide: HTTP_INTERCEPTORS, useClass: MsalInterceptor, multi: true },
    MsalService,
    MsalGuard,
    MsalBroadcastService,
  ];
}

export function createAppConfig(authConfig: AuthConfig, msalInstance: PublicClientApplication | null): ApplicationConfig {
  const msalProviders = (authConfig.enabled && msalInstance) ? createMsalProviders(authConfig, msalInstance) : [
    { provide: MsalService, useValue: null },
  ];
  const activeRoutes = authConfig.enabled ? authRoutes : routes;

  return {
    providers: [
      provideZoneChangeDetection({ eventCoalescing: true }),
      provideRouter(activeRoutes),
      provideAnimationsAsync(),
      provideHttpClient(withInterceptorsFromDi()),
      ...msalProviders,
    ]
  };
}
