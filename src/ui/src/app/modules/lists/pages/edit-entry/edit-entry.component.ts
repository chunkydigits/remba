import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  BehaviorSubject,
  Observable,
  Subject,
  catchError,
  combineLatest,
  finalize,
  map,
  of,
  shareReplay,
  switchMap,
  take,
  tap,
} from 'rxjs';
import { MaterialModule } from '../../../../core/material.module';
import { PageTemplateComponent } from '../../../../components/page-template/page-template.component';
import { EntryAttributesFieldsComponent } from '../../components/entry-attributes-fields/entry-attributes-fields.component';
import { ListService } from '../../services/list.service';
import { EntryService } from '../../services/entry.service';
import {
  ListAttributeDefinition,
  ListDetail,
  ListFieldOption,
  availableFieldsForEntityType,
} from '../../models/list.model';
import { ListEntry, UpdateEntryRequest } from '../../models/entry.model';
import {
  createAttributesFormGroup,
  serializeAttributes,
  syncAttributesFormGroup,
} from '../../utils/entry-attributes.util';

@Component({
  selector: 'app-edit-entry',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MaterialModule,
    PageTemplateComponent,
    RouterLink,
    EntryAttributesFieldsComponent,
  ],
  templateUrl: './edit-entry.component.html',
  styleUrl: './edit-entry.component.scss',
})
export class EditEntryComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly listService = inject(ListService);
  private readonly entryService = inject(EntryService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly saveRequests = new Subject<void>();
  private readonly reload$ = new BehaviorSubject<void>(undefined);

  error = '';
  saving = false;
  loaded = false;
  customDefinitions: ListAttributeDefinition[] = [];

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    author: ['', [Validators.maxLength(400)]],
    director: ['', [Validators.maxLength(400)]],
    actors: ['', [Validators.maxLength(800)]],
    genre: ['', [Validators.maxLength(400)]],
    themes: ['', [Validators.maxLength(800)]],
    link: ['', [Validators.maxLength(2000)]],
    read: [false],
    visited: [false],
    seen: [false],
    attributes: createAttributesFormGroup(this.formBuilder, []),
  });

  readonly listId$: Observable<string | null> = this.route.paramMap.pipe(
    map((params) => params.get('listId')),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly entryId$: Observable<string | null> = this.route.paramMap.pipe(
    map((params) => params.get('entryId')),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly list$: Observable<ListDetail | null> = this.listId$.pipe(
    switchMap((listId) => {
      if (!listId) {
        return of(null);
      }

      return this.listService.getOwnList(listId).pipe(catchError(() => of(null)));
    }),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly entityType$: Observable<string> = this.list$.pipe(
    map((list) => (list?.entityType || '').trim().toLowerCase()),
  );

  /** Every property available for this list's entity type (not just list-view columns). */
  readonly detailFields$: Observable<ListFieldOption[]> = this.entityType$.pipe(
    map((type) => availableFieldsForEntityType(type).filter((field) => field.key !== 'added')),
  );

  readonly entry$: Observable<ListEntry | null> = combineLatest([
    this.listId$,
    this.entryId$,
    this.list$,
    this.reload$,
  ]).pipe(
    switchMap(([listId, entryId, list]) => {
      if (!listId || !entryId) {
        return of(null);
      }

      return this.entryService.get(listId, entryId).pipe(
        tap((entry) => {
          this.loaded = true;
          this.customDefinitions = [...(list?.attributes || [])];
          syncAttributesFormGroup(
            this.form.controls.attributes,
            this.formBuilder,
            this.customDefinitions,
            entry.attributes,
          );
          this.form.patchValue({
            name: entry.name || '',
            author: entry.author || '',
            director: entry.director || '',
            actors: entry.actors || '',
            genre: entry.genre || '',
            themes: entry.themes || '',
            link: entry.link || '',
            read: !!entry.read,
            visited: !!entry.visited,
            seen: !!entry.seen,
          });
        }),
        catchError(() => {
          this.loaded = true;
          this.error = 'Unable to load item.';
          return of(null);
        }),
      );
    }),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly saveResult$: Observable<ListEntry | null> = this.saveRequests.pipe(
    switchMap(() => {
      const listId = this.route.snapshot.paramMap.get('listId');
      const entryId = this.route.snapshot.paramMap.get('entryId');
      if (!listId || !entryId || this.form.controls.name.invalid) {
        this.form.markAllAsTouched();
        return of(null);
      }

      const value = this.form.getRawValue();
      this.error = '';
      this.saving = true;

      return this.list$.pipe(
        take(1),
        switchMap((list) => {
          const entityType = (list?.entityType || '').trim().toLowerCase();
          const fields = new Set(availableFieldsForEntityType(entityType).map((field) => field.key));
          const definitions = list?.attributes || [];
          const request: UpdateEntryRequest = {
            name: value.name.trim(),
            attributes: serializeAttributes(definitions, value.attributes),
          };

          if (fields.has('author')) {
            request.author = value.author.trim();
          }
          if (fields.has('director')) {
            request.director = value.director.trim();
          }
          if (fields.has('actors')) {
            request.actors = value.actors.trim();
          }
          if (fields.has('genre')) {
            request.genre = value.genre.trim();
          }
          if (fields.has('themes')) {
            request.themes = value.themes.trim();
          }
          if (fields.has('link')) {
            request.link = value.link.trim();
          }
          if (fields.has('read')) {
            request.read = value.read;
          }
          if (fields.has('visited')) {
            request.visited = value.visited;
          }
          if (fields.has('seen')) {
            request.seen = value.seen;
          }

          return this.entryService.update(listId, entryId, request);
        }),
        tap(() => {
          void this.router.navigate(['/lists', listId]);
        }),
        catchError((error) => {
          this.error = error?.error?.message || 'Unable to save item.';
          return of(null);
        }),
        finalize(() => {
          this.saving = false;
        }),
      );
    }),
  );

  showWatchProviders(entityType: string | null | undefined): boolean {
    const type = (entityType || '').trim().toLowerCase();
    return type === 'film' || type === 'tv';
  }

  identifierLabel(entry: ListEntry): { label: string; value: string } | null {
    const type = (entry.identifierType || '').trim().toLowerCase();
    const value = (entry.identifierValue || '').trim();
    if (!value) {
      return null;
    }

    switch (type) {
      case 'isbn':
        return { label: 'ISBN', value };
      case 'imdbid':
        return { label: 'IMDb', value };
      case 'openlibrarywork':
        return { label: 'Open Library', value };
      case 'iso2':
        return { label: 'ISO', value };
      case 'url':
        return { label: 'URL', value };
      default:
        return type ? { label: type, value } : { label: 'Identifier', value };
    }
  }

  formatAdded(value: string | null | undefined): string {
    if (!value) {
      return '—';
    }

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
  }

  onSave(): void {
    if (this.saving) {
      return;
    }

    this.saveRequests.next();
  }
}
