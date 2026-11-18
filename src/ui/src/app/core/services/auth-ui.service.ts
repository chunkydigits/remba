import { Injectable } from '@angular/core';
import { AuthService } from '@auth0/auth0-angular';
import { BehaviorSubject, Observable, catchError, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { UserService } from './user.service';

@Injectable({
  providedIn: 'root',
})
export class AuthUiService {
  readonly isAuthenticated$: Observable<boolean>;
  readonly isLoading$: Observable<boolean>;
  readonly error$: Observable<string | null>;

  private readonly errorSubject = new BehaviorSubject<string | null>(null);

  constructor(
    private readonly auth: AuthService,
    private readonly userService: UserService,
  ) {
    this.isAuthenticated$ = this.auth.isAuthenticated$;
    this.isLoading$ = this.auth.isLoading$;
    this.error$ = this.errorSubject.asObservable();

    this.auth.error$
      .pipe(
        tap((error) => {
          if (!error) {
            return;
          }

          console.error('Auth0 error', error);
          this.errorSubject.next(error.message || 'Authentication failed.');
        }),
      )
      .subscribe();
  }

  login(): void {
    this.errorSubject.next(null);

    this.auth
      .loginWithRedirect({
        appState: { target: '/' },
        authorizationParams: {
          redirect_uri: window.location.origin,
          audience: environment.auth0.audience,
        },
      })
      .pipe(
        catchError((error) => {
          console.error('Auth0 loginWithRedirect failed', error);
          this.errorSubject.next(error?.message || 'Unable to start sign-in.');
          return of(undefined);
        }),
      )
      .subscribe();
  }

  logout(): void {
    this.userService.clear();
    this.errorSubject.next(null);

    this.auth
      .logout({
        logoutParams: {
          returnTo: window.location.origin,
        },
      })
      .pipe(
        catchError((error) => {
          console.error('Auth0 logout failed', error);
          this.errorSubject.next(error?.message || 'Unable to sign out.');
          return of(undefined);
        }),
      )
      .subscribe();
  }
}
