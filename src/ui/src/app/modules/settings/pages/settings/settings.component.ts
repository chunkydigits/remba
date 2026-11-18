import { Component, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ThemeMode } from '../../../../core/models/theme.model';
import { ThemeService } from '../../../../core/services/theme.service';

@Component({
  selector: 'app-settings',
  standalone: false,
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent {
  private readonly themeService = inject(ThemeService);

  readonly theme$: Observable<ThemeMode> = this.themeService.theme$;

  onThemeChange(theme: ThemeMode | null): void {
    if (theme === 'light' || theme === 'dark') {
      this.themeService.setTheme(theme);
    }
  }
}
