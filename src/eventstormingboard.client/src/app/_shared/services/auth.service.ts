import { Injectable } from '@angular/core';
import { MsalService } from '@azure/msal-angular';
import { AuthConfig } from '../models/auth-config.model';
import { ReplaySubject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private static authConfig: AuthConfig = { enabled: false };
  private _initialized$ = new ReplaySubject<void>(1);

  public initialized$ = this._initialized$.asObservable();

  constructor(private msalService: MsalService | null) {
    // MSAL is already initialized and redirect-handled in main.ts before Angular bootstraps.
    // Signal ready immediately so SignalR and other consumers can proceed.
    this._initialized$.next();
  }

  static setAuthConfig(config: AuthConfig): void {
    AuthService.authConfig = config;
  }

  get isAuthEnabled(): boolean {
    return AuthService.authConfig.enabled;
  }

  async getAccessToken(): Promise<string | null> {
    if (!this.isAuthEnabled || !this.msalService) {
      return null;
    }

    const account = this.msalService.instance.getActiveAccount();
    if (!account) {
      throw new Error('No active MSAL account. User must be logged in when auth is enabled.');
    }

    try {
      const result = await this.msalService.instance.acquireTokenSilent({
        scopes: AuthService.authConfig.scopes ?? [],
        account: account
      });
      return result.accessToken;
    } catch {
      // Silent token acquisition failed — trigger interactive redirect
      await this.msalService.instance.loginRedirect({
        scopes: AuthService.authConfig.scopes ?? []
      });
      throw new Error('Token acquisition failed. Redirecting to login.');
    }
  }

  getUserName(): string | null {
    if (!this.isAuthEnabled || !this.msalService) {
      return null;
    }

    const account = this.msalService.instance.getActiveAccount();
    return account?.name ?? account?.username ?? null;
  }

  isAuthenticated(): boolean {
    if (!this.isAuthEnabled || !this.msalService) {
      return false;
    }

    return this.msalService.instance.getAllAccounts().length > 0;
  }

  getCurrentUserName(fallback: string): string {
    if (this.isAuthEnabled) {
      return this.getUserName() ?? fallback;
    }
    return fallback;
  }
}
