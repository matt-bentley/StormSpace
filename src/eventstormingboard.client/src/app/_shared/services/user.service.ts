import { inject, Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly storageKey = 'userName';
  private userName$ = new BehaviorSubject<string>(localStorage.getItem(this.storageKey) ?? '');
  private authService = inject(AuthService);

  constructor() {
    // When auth is enabled, override with the Entra ID display name
    if (this.authService.isAuthEnabled) {
      const authName = this.authService.getUserName();
      if (authName) {
        this.userName$.next(authName);
      }
    }
  }

  get displayName$() {
    return this.userName$.asObservable();
  }

  get displayName(): string {
    return this.authService.getCurrentUserName(this.userName$.value);
  }

  get isReadOnly(): boolean {
    return this.authService.isAuthEnabled;
  }

  setDisplayName(name: string): void {
    if (this.authService.isAuthEnabled) {
      return; // Name is managed by Entra ID when auth is enabled
    }
    const trimmed = name.trim();
    if (trimmed) {
      localStorage.setItem(this.storageKey, trimmed);
    }
    this.userName$.next(trimmed);
  }
}
