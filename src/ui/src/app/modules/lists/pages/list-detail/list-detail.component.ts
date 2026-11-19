import { Component, inject } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  Observable,
  Subject,
  catchError,
  map,
  of,
  shareReplay,
  switchMap,
  tap,
} from 'rxjs';
import { ListService } from '../../services/list.service';
import { ListDetail } from '../../models/list.model';

@Component({
  selector: 'app-list-detail',
  standalone: false,
  templateUrl: './list-detail.component.html',
  styleUrl: './list-detail.component.scss',
})
export class ListDetailComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly listService = inject(ListService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly saveRequests = new Subject<void>();
  private readonly deleteRequests = new Subject<void>();
  private readonly viewSettingsRequests = new Subject<void>();

  saveError = '';
  saveSuccess = false;
  deleteError = '';

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    description: ['', [Validators.maxLength(1000)]],
    entityType: ['', [Validators.maxLength(40)]],
    visibility: ['private', Validators.required],
    sortBy: ['manual', Validators.required],
    sortDirection: ['ascending', Validators.required],
    viewMode: ['list', Validators.required],
  });

  readonly list$: Observable<ListDetail | null> = this.route.paramMap.pipe(
    map((params) => params.get('listId')),
    switchMap((listId) => {
      if (!listId) {
        return of(null);
      }

      return this.listService.getOwnList(listId).pipe(
        tap((list) => {
          this.form.patchValue({
            name: list.name,
            description: list.description,
            entityType: list.entityType || '',
            visibility: list.visibility || 'private',
            sortBy: list.viewSettings.sortBy,
            sortDirection: list.viewSettings.sortDirection,
            viewMode: list.viewSettings.viewMode,
          });
        }),
        catchError(() => of(null)),
      );
    }),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly saveResult$: Observable<ListDetail | null> = this.saveRequests.pipe(
    switchMap(() => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId || this.form.invalid) {
        this.form.markAllAsTouched();
        return of(null);
      }

      this.saveError = '';
      this.saveSuccess = false;
      const value = this.form.getRawValue();

      return this.listService
        .update(listId, {
          name: value.name,
          description: value.description,
          entityType: value.entityType || undefined,
          visibility: value.visibility,
        })
        .pipe(
          tap(() => {
            this.saveSuccess = true;
          }),
          catchError((error) => {
            this.saveError = error?.error?.message || 'Unable to update list.';
            return of(null);
          }),
        );
    }),
  );

  readonly viewSettingsResult$: Observable<ListDetail | null> = this.viewSettingsRequests.pipe(
    switchMap(() => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId) {
        return of(null);
      }

      const value = this.form.getRawValue();
      return this.listService
        .updateViewSettings(listId, {
          sortBy: value.sortBy,
          sortDirection: value.sortDirection,
          viewMode: value.viewMode,
        })
        .pipe(
          tap(() => {
            this.saveSuccess = true;
          }),
          catchError((error) => {
            this.saveError = error?.error?.message || 'Unable to update view settings.';
            return of(null);
          }),
        );
    }),
  );

  readonly deleteResult$: Observable<boolean | null> = this.deleteRequests.pipe(
    switchMap(() => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId) {
        return of(null);
      }

      this.deleteError = '';
      return this.listService.delete(listId).pipe(
        tap(() => {
          void this.router.navigate(['/lists']);
        }),
        map(() => true),
        catchError((error) => {
          this.deleteError = error?.error?.message || 'Unable to delete list.';
          return of(false);
        }),
      );
    }),
  );

  onSave(): void {
    this.saveRequests.next();
  }

  onSaveViewSettings(): void {
    this.viewSettingsRequests.next();
  }

  onDelete(): void {
    this.deleteRequests.next();
  }
}
