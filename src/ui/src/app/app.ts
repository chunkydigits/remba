import { Component, inject } from '@angular/core';
import { ThemeService } from './core/services/theme.service';

@Component({
  selector: 'app-root',
  standalone: false,
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  // Ensure theme is applied as soon as the app boots.
  private readonly themeService = inject(ThemeService);
}
