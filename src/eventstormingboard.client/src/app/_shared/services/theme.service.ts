import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type Theme = 'dark' | 'light';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private static readonly STORAGE_KEY = 'stormspace-theme';

  private readonly themeSubject = new BehaviorSubject<Theme>(this.loadTheme());
  public readonly theme$ = this.themeSubject.asObservable();

  constructor() {
    this.applyTheme(this.themeSubject.value);
  }

  public get currentTheme(): Theme {
    return this.themeSubject.value;
  }

  public get isDark(): boolean {
    return this.themeSubject.value === 'dark';
  }

  public toggleTheme(): void {
    this.setTheme(this.isDark ? 'light' : 'dark');
  }

  public setTheme(theme: Theme): void {
    this.applyTheme(theme);
    localStorage.setItem(ThemeService.STORAGE_KEY, theme);
    this.themeSubject.next(theme);
  }

  private loadTheme(): Theme {
    const stored = localStorage.getItem(ThemeService.STORAGE_KEY);
    return stored === 'light' ? 'light' : 'dark';
  }

  private applyTheme(theme: Theme): void {
    document.documentElement.setAttribute('data-theme', theme);
  }
}
