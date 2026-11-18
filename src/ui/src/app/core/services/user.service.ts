import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, catchError, of, switchMap, tap } from 'rxjs';
import { AuthService } from '@auth0/auth0-angular';
import { environment } from '../../../environments/environment';
import { UpdateUserRequest, User } from '../models/user.model';

@Injectable({
  providedIn: 'root',
})
export class UserService {
  private readonly currentUserSubject = new BehaviorSubject<User | null>(null);
  readonly currentUser$ = this.currentUserSubject.asObservable();

  constructor(
    private readonly http: HttpClient,
    private readonly auth: AuthService,
  ) {}

  loadCurrentUser(): Observable<User | null> {
    return this.auth.isAuthenticated$.pipe(
      switchMap((isAuthenticated) => {
        if (!isAuthenticated) {
          this.currentUserSubject.next(null);
          return of(null);
        }

        return this.http.get<User>(`${environment.apiBaseUrl}/api/me`).pipe(
          tap((user) => this.currentUserSubject.next(user)),
          catchError(() => {
            this.currentUserSubject.next(null);
            return of(null);
          }),
        );
      }),
    );
  }

  updateCurrentUser(request: UpdateUserRequest): Observable<User> {
    return this.http.put<User>(`${environment.apiBaseUrl}/api/me`, request).pipe(
      tap((user) => this.currentUserSubject.next(user)),
    );
  }

  clear(): void {
    this.currentUserSubject.next(null);
  }
}
