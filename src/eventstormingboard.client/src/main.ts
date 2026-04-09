import { bootstrapApplication } from '@angular/platform-browser';
import { createAppConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { AuthConfig } from './app/_shared/models/auth-config.model';
import { AuthService } from './app/_shared/services/auth.service';
import {
  BrowserCacheLocation,
  PublicClientApplication,
} from '@azure/msal-browser';

async function fetchAuthConfig(): Promise<AuthConfig> {
  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 5000);
    const response = await fetch('/api/auth/config', { signal: controller.signal });
    clearTimeout(timeoutId);
    if (!response.ok) {
      console.warn('Auth config endpoint returned', response.status, '— bootstrapping with auth disabled');
      return { enabled: false };
    }
    return await response.json();
  } catch {
    console.warn('Failed to fetch auth config — bootstrapping with auth disabled');
    return { enabled: false };
  }
}

async function main(): Promise<void> {
  const authConfig = await fetchAuthConfig();
  AuthService.setAuthConfig(authConfig);

  let msalInstance: PublicClientApplication | null = null;

  if (authConfig.enabled) {
    msalInstance = new PublicClientApplication({
      auth: {
        clientId: authConfig.clientId!,
        authority: `${authConfig.instance ?? 'https://login.microsoftonline.com'}/${authConfig.tenantId}`,
        redirectUri: window.location.origin,
      },
      cache: {
        cacheLocation: BrowserCacheLocation.SessionStorage,
      },
    });

    // Initialize and process any redirect response BEFORE Angular bootstraps.
    // This prevents MsalGuard from triggering a new redirect before the auth code is handled.
    await msalInstance.initialize();
    const response = await msalInstance.handleRedirectPromise();

    const accounts = msalInstance.getAllAccounts();
    if (accounts.length > 0) {
      msalInstance.setActiveAccount(response?.account ?? accounts[0]);
    } else {
      // No accounts — trigger login and stop. Angular will bootstrap after redirect.
      await msalInstance.loginRedirect({ scopes: authConfig.scopes ?? [] });
      return;
    }
  }

  const appConfig = createAppConfig(authConfig, msalInstance);
  await bootstrapApplication(AppComponent, appConfig);
}

main().catch((err) => console.error(err));
