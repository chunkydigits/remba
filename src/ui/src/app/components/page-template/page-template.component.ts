import { AsyncPipe } from '@angular/common';
import { Component, Input, ViewChild, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatSidenav } from '@angular/material/sidenav';
import { Observable } from 'rxjs';
import { MaterialModule } from '../../core/material.module';
import { AuthUiService } from '../../core/services/auth-ui.service';
import { ThemeService } from '../../core/services/theme.service';
import { ThemeMode } from '../../core/models/theme.model';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-page-template',
  imports: [AsyncPipe, RouterLink, RouterLinkActive, MaterialModule],
  templateUrl: './page-template.component.html',
  styleUrl: './page-template.component.scss',
})
export class PageTemplateComponent {
  @Input() title = '';
  @Input() showHeader = true;

  @ViewChild('drawer') private readonly drawer?: MatSidenav;

  readonly authConfigured =
    !!environment.auth0.domain &&
    !!environment.auth0.clientId &&
    !!environment.auth0.audience;

  readonly isAuthenticated$: Observable<boolean>;
  readonly isLoading$: Observable<boolean>;
  readonly authError$: Observable<string | null>;
  readonly theme$: Observable<ThemeMode>;

  private readonly authUi = inject(AuthUiService);
  private readonly themeService = inject(ThemeService);

  constructor() {
    this.isAuthenticated$ = this.authUi.isAuthenticated$;
    this.isLoading$ = this.authUi.isLoading$;
    this.authError$ = this.authUi.error$;
    this.theme$ = this.themeService.theme$;
  }

  openMenu(): void {
    void this.drawer?.open();
  }

  closeMenu(): void {
    void this.drawer?.close();
  }

  login(): void {
    this.authUi.login();
  }

  logout(): void {
    this.closeMenu();
    this.authUi.logout();
  }

  markLogoSrc(theme: ThemeMode): string {
    return theme === 'dark'
      ? 'assets/images/word-icon-transparent-dark.png'
      : 'assets/images/word-icon-transparent.png';
  }

  fullLogoSrc(theme: ThemeMode): string {
    return theme === 'dark'
      ? 'assets/images/logo-transparent-dark.png'
      : 'assets/images/logo-transparent-light.png';
  }
}
