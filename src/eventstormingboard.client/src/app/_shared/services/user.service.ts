import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly storageKey = 'userName';
  private userName$ = new BehaviorSubject<string>(localStorage.getItem(this.storageKey) ?? '');

  get displayName$() {
    return this.userName$.asObservable();
  }

  get displayName(): string {
    return this.userName$.value;
  }

  setDisplayName(name: string): void {
    const trimmed = name.trim();
    if (trimmed) {
      localStorage.setItem(this.storageKey, trimmed);
    }
    this.userName$.next(trimmed);
  }
}
