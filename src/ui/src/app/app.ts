import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/services/theme.service';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  // Ensure theme is applied as soon as the app boots.
  private readonly themeService = inject(ThemeService);

  readonly devFrame = environment.devFrame;
}
