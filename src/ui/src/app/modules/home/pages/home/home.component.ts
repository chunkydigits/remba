import { Component, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthUiService } from '../../../../core/services/auth-ui.service';
import { User } from '../../../../core/models/user.model';
import { UserService } from '../../../../core/services/user.service';
import { ThemeService } from '../../../../core/services/theme.service';
import { ThemeMode } from '../../../../core/models/theme.model';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-home',
  standalone: false,
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  readonly authConfigured =
    !!environment.auth0.domain &&
    !!environment.auth0.clientId &&
    !!environment.auth0.audience;

  readonly isAuthenticated$: Observable<boolean>;
  readonly currentUser$: Observable<User | null>;
  readonly bootstrapUser$: Observable<User | null>;
  readonly theme$: Observable<ThemeMode>;

  private readonly authUi = inject(AuthUiService);
  private readonly userService = inject(UserService);
  private readonly themeService = inject(ThemeService);

  constructor() {
    this.isAuthenticated$ = this.authUi.isAuthenticated$;
    this.currentUser$ = this.userService.currentUser$;
    this.bootstrapUser$ = this.userService.loadCurrentUser();
    this.theme$ = this.themeService.theme$;
  }

  login(): void {
    this.authUi.login();
  }

  heroLogoSrc(theme: ThemeMode): string {
    return theme === 'dark'
      ? 'assets/images/logo-transparent-dark.png'
      : 'assets/images/logo-transparent-light.png';
  }
}
