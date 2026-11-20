import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, Subject, catchError, of, switchMap, tap } from 'rxjs';
import { PageTemplateComponent } from '../../../../components/page-template/page-template.component';
import { MaterialModule } from '../../../../core/material.module';
import { User } from '../../../../core/models/user.model';
import { UserService } from '../../../../core/services/user.service';

@Component({
  selector: 'app-account',
  imports: [AsyncPipe, ReactiveFormsModule, MaterialModule, PageTemplateComponent],
  templateUrl: './account.component.html',
  styleUrl: './account.component.scss',
})
export class AccountComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly userService = inject(UserService);

  readonly user$: Observable<User | null>;
  readonly bootstrapUser$: Observable<User | null>;
  saveError = '';
  saveSuccess = false;

  private readonly saveRequests = new Subject<void>();

  readonly form = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(80)]],
    username: ['', [Validators.required, Validators.maxLength(40)]],
    bio: ['', [Validators.maxLength(500)]],
    visibility: ['private', Validators.required],
  });

  readonly saveResult$: Observable<User | null> = this.saveRequests.pipe(
    switchMap(() => {
      if (this.form.invalid) {
        this.form.markAllAsTouched();
        return of(null);
      }

      this.saveError = '';
      this.saveSuccess = false;
      const value = this.form.getRawValue();

      return this.userService
        .updateCurrentUser({
          displayName: value.displayName,
          username: value.username,
          profile: {
            bio: value.bio,
            visibility: value.visibility,
          },
        })
        .pipe(
          tap(() => {
            this.saveSuccess = true;
          }),
          catchError((error) => {
            this.saveError = error?.error?.message || 'Unable to update your profile.';
            return of(null);
          }),
        );
    }),
  );

  constructor() {
    this.bootstrapUser$ = this.userService.loadCurrentUser();
    this.user$ = this.userService.currentUser$.pipe(
      tap((user) => {
        if (!user) {
          return;
        }

        this.form.patchValue({
          displayName: user.displayName,
          username: user.username,
          bio: user.profile.bio,
          visibility: user.profile.visibility || 'private',
        });
      }),
    );
  }

  onSave(): void {
    this.saveRequests.next();
  }
}
