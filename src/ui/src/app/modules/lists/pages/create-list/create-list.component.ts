import { Component, inject } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Observable, Subject, catchError, of, switchMap, tap } from 'rxjs';
import { ListService } from '../../services/list.service';
import { ListDetail } from '../../models/list.model';

@Component({
  selector: 'app-create-list',
  standalone: false,
  templateUrl: './create-list.component.html',
  styleUrl: './create-list.component.scss',
})
export class CreateListComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly listService = inject(ListService);
  private readonly router = inject(Router);
  private readonly saveRequests = new Subject<void>();

  saveError = '';

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    description: ['', [Validators.maxLength(1000)]],
    entityType: ['', [Validators.maxLength(40)]],
    visibility: ['private', Validators.required],
  });

  readonly saveResult$: Observable<ListDetail | null> = this.saveRequests.pipe(
    switchMap(() => {
      if (this.form.invalid) {
        this.form.markAllAsTouched();
        return of(null);
      }

      this.saveError = '';
      const value = this.form.getRawValue();

      return this.listService
        .create({
          name: value.name,
          description: value.description || undefined,
          entityType: value.entityType || undefined,
          visibility: value.visibility,
        })
        .pipe(
          tap((list) => {
            void this.router.navigate(['/lists', list.id]);
          }),
          catchError((error) => {
            this.saveError = error?.error?.message || 'Unable to create list.';
            return of(null);
          }),
        );
    }),
  );

  onSave(): void {
    this.saveRequests.next();
  }
}
