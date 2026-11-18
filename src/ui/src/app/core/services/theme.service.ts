import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { THEME_STORAGE_KEY, ThemeMode } from '../models/theme.model';

@Injectable({
  providedIn: 'root',
})
export class ThemeService {
  private readonly themeSubject: BehaviorSubject<ThemeMode>;
  readonly theme$: Observable<ThemeMode>;

  constructor() {
    const initial = this.readStoredTheme();
    this.themeSubject = new BehaviorSubject<ThemeMode>(initial);
    this.theme$ = this.themeSubject.asObservable();
    this.applyTheme(initial);
  }

  setTheme(theme: ThemeMode): void {
    if (theme !== 'light' && theme !== 'dark') {
      return;
    }

    this.themeSubject.next(theme);
    localStorage.setItem(THEME_STORAGE_KEY, theme);
    this.applyTheme(theme);
  }

  private readStoredTheme(): ThemeMode {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') {
      return stored;
    }

    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }

  private applyTheme(theme: ThemeMode): void {
    document.documentElement.dataset['theme'] = theme;
    document.documentElement.style.colorScheme = theme;
  }
}
