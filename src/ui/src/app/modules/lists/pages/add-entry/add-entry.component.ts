import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  BehaviorSubject,
  Observable,
  Subject,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  finalize,
  map,
  of,
  shareReplay,
  startWith,
  switchMap,
  tap,
} from 'rxjs';
import { MaterialModule } from '../../../../core/material.module';
import { PageTemplateComponent } from '../../../../components/page-template/page-template.component';
import { ListService } from '../../services/list.service';
import { EntryService } from '../../services/entry.service';
import { EntityService } from '../../services/entity.service';
import { ListDetail } from '../../models/list.model';
import { CreateEntryRequest, ListEntry } from '../../models/entry.model';
import { EntitySearchResult } from '../../models/entity.model';

const LOOKUP_ENTITY_TYPES = new Set(['film', 'tv', 'book']);

type AddMode = 'done' | 'another';

@Component({
  selector: 'app-add-entry',
  imports: [CommonModule, ReactiveFormsModule, MaterialModule, PageTemplateComponent],
  templateUrl: './add-entry.component.html',
  styleUrl: './add-entry.component.scss',
})
export class AddEntryComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly listService = inject(ListService);
  private readonly entryService = inject(EntryService);
  private readonly entityService = inject(EntityService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly addPlainRequests = new Subject<AddMode>();
  private readonly addSearchRequests = new Subject<{ result: EntitySearchResult; mode: AddMode }>();
  private readonly providerSearchQuery$ = new BehaviorSubject<string>('');
  private readonly entriesReload$ = new BehaviorSubject<void>(undefined);
  private readonly recentlyAddedKeys$ = new BehaviorSubject<ReadonlySet<string>>(new Set());

  error = '';
  success = false;
  searching = false;
  searchError = '';
  saving = false;
  savingKey: string | null = null;

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    link: ['', [Validators.maxLength(2000)]],
  });

  readonly providerSearchForm = this.formBuilder.nonNullable.group({
    query: ['', [Validators.maxLength(200)]],
  });

  readonly listId$: Observable<string | null> = this.route.paramMap.pipe(
    map((params) => params.get('listId')),
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

  readonly supportsProviderSearch$: Observable<boolean> = this.entityType$.pipe(
    map((type) => LOOKUP_ENTITY_TYPES.has(type)),
  );

  readonly existingEntries$: Observable<ListEntry[]> = combineLatest([this.listId$, this.entriesReload$]).pipe(
    switchMap(([listId]) => {
      if (!listId) {
        return of([]);
      }

      return this.entryService
        .getByList(listId, { sortBy: 'order', sortDirection: 'ascending' })
        .pipe(catchError(() => of([] as ListEntry[])));
    }),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  private readonly rawProviderSearchResults$: Observable<EntitySearchResult[]> = combineLatest([
    this.list$,
    this.providerSearchQuery$.pipe(debounceTime(300), distinctUntilChanged()),
  ]).pipe(
    switchMap(([list, query]) => {
      const entityType = list?.entityType?.trim().toLowerCase() || '';
      const trimmed = query.trim();
      if (!list || !LOOKUP_ENTITY_TYPES.has(entityType) || trimmed.length < 2) {
        this.searching = false;
        this.searchError = '';
        return of([]);
      }

      this.searching = true;
      this.searchError = '';
      return this.entityService.search(entityType, trimmed).pipe(
        tap(() => {
          this.searching = false;
        }),
        catchError((error) => {
          this.searching = false;
          this.searchError = error?.error?.message || 'Unable to search.';
          return of([]);
        }),
      );
    }),
    startWith([]),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly providerSearchResults$: Observable<EntitySearchResult[]> = combineLatest([
    this.rawProviderSearchResults$,
    this.existingEntries$,
    this.recentlyAddedKeys$,
  ]).pipe(
    map(([results, existingEntries, recentlyAddedKeys]) =>
      results.map((result) => ({
        ...result,
        alreadyOnList: this.isAlreadyOnList(result, existingEntries, recentlyAddedKeys),
      })),
    ),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly addPlainResult$: Observable<ListEntry | null> = this.addPlainRequests.pipe(
    switchMap((mode) => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId || this.form.controls.name.invalid) {
        this.form.markAllAsTouched();
        this.clearSaving();
        return of(null);
      }

      const value = this.form.getRawValue();
      return this.createEntry(
        listId,
        {
          name: value.name,
          link: value.link || undefined,
        },
        mode,
        this.plainSavingKey(mode),
        { name: value.name },
      );
    }),
  );

  readonly addSearchResult$: Observable<ListEntry | null> = this.addSearchRequests.pipe(
    switchMap(({ result, mode }) => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId || result.alreadyOnList) {
        this.clearSaving();
        return of(null);
      }

      const metadata: Record<string, string> = {};
      if (result.year) {
        metadata['year'] = result.year;
      }
      if (result.subtitle) {
        metadata['subtitle'] = result.subtitle;
      }
      if (result.imageUrl) {
        metadata['imageUrl'] = result.imageUrl;
      }
      if (result.author) {
        metadata['author'] = result.author;
      }
      if (result.director) {
        metadata['director'] = result.director;
      }
      if (result.actors) {
        metadata['actors'] = result.actors;
      }

      return this.createEntry(
        listId,
        {
          name: result.name,
          identifier: {
            type: result.identifierType,
            value: result.identifierValue,
          },
          link: result.link || undefined,
          author: result.author || undefined,
          director: result.director || undefined,
          actors: result.actors || undefined,
          metadata: Object.keys(metadata).length ? metadata : undefined,
        },
        mode,
        this.searchSavingKey(result, mode),
        result,
      );
    }),
  );

  onProviderSearchInput(): void {
    this.providerSearchQuery$.next(this.providerSearchForm.controls.query.value);
  }

  onAdd(): void {
    if (this.saving) {
      return;
    }

    this.beginSaving(this.plainSavingKey('done'));
    this.addPlainRequests.next('done');
  }

  onAddAnother(): void {
    if (this.saving) {
      return;
    }

    this.beginSaving(this.plainSavingKey('another'));
    this.addPlainRequests.next('another');
  }

  onSelectSearchResult(result: EntitySearchResult, mode: AddMode): void {
    if (this.saving || result.alreadyOnList) {
      return;
    }

    this.beginSaving(this.searchSavingKey(result, mode));
    this.addSearchRequests.next({ result, mode });
  }

  isSavingPlain(mode: AddMode): boolean {
    return this.savingKey === this.plainSavingKey(mode);
  }

  isSavingResult(result: EntitySearchResult): boolean {
    return (
      this.savingKey === this.searchSavingKey(result, 'done')
      || this.savingKey === this.searchSavingKey(result, 'another')
    );
  }

  onCancel(): void {
    const listId = this.route.snapshot.paramMap.get('listId');
    if (listId) {
      void this.router.navigate(['/lists', listId]);
    }
  }

  searchPlaceholder(entityType: string | null | undefined): string {
    switch ((entityType || '').toLowerCase()) {
      case 'book':
        return 'Search by title or ISBN';
      case 'tv':
      case 'film':
        return 'Search by title or IMDb ID';
      default:
        return 'Search';
    }
  }

  private createEntry(
    listId: string,
    request: CreateEntryRequest,
    mode: AddMode,
    savingKey: string,
    addedItem: {
      name: string;
      identifierType?: string | null;
      identifierValue?: string | null;
    },
  ): Observable<ListEntry | null> {
    this.error = '';
    this.success = false;
    this.beginSaving(savingKey);

    return this.entryService.create(listId, request).pipe(
      tap(() => {
        this.markRecentlyAdded(addedItem);
        this.success = true;
        this.form.reset({ name: '', link: '' });
        this.entriesReload$.next();

        if (mode === 'done') {
          this.clearProviderSearch();
          void this.router.navigate(['/lists', listId]);
        }
      }),
      catchError((error) => {
        this.error = error?.error?.message || 'Unable to add item.';
        return of(null);
      }),
      finalize(() => {
        this.clearSaving();
      }),
    );
  }

  private beginSaving(savingKey: string): void {
    this.saving = true;
    this.savingKey = savingKey;
  }

  private clearSaving(): void {
    this.saving = false;
    this.savingKey = null;
  }

  private markRecentlyAdded(item: {
    name: string;
    identifierType?: string | null;
    identifierValue?: string | null;
  }): void {
    const next = new Set(this.recentlyAddedKeys$.value);
    const identifier = this.normaliseIdentifier(item.identifierType, item.identifierValue);
    if (identifier) {
      next.add(`id:${identifier}`);
    }

    const title = this.normaliseTitle(item.name);
    if (title) {
      next.add(`title:${title}`);
    }

    this.recentlyAddedKeys$.next(next);
  }

  private plainSavingKey(mode: AddMode): string {
    return `plain:${mode}`;
  }

  private searchSavingKey(result: EntitySearchResult, mode: AddMode): string {
    return `search:${result.identifierType}:${result.identifierValue}:${mode}`;
  }

  private clearProviderSearch(): void {
    this.providerSearchForm.reset({ query: '' });
    this.providerSearchQuery$.next('');
  }

  private isAlreadyOnList(
    result: EntitySearchResult,
    entries: ListEntry[],
    recentlyAddedKeys: ReadonlySet<string>,
  ): boolean {
    const resultTitle = this.normaliseTitle(result.name);
    const resultIdentifier = this.normaliseIdentifier(result.identifierType, result.identifierValue);

    if (resultIdentifier && recentlyAddedKeys.has(`id:${resultIdentifier}`)) {
      return true;
    }

    if (resultTitle && recentlyAddedKeys.has(`title:${resultTitle}`)) {
      return true;
    }

    return entries.some((entry) => {
      if (resultIdentifier) {
        const entryIdentifier = this.normaliseIdentifier(entry.identifierType, entry.identifierValue);
        if (entryIdentifier && entryIdentifier === resultIdentifier) {
          return true;
        }
      }

      return this.normaliseTitle(entry.name) === resultTitle;
    });
  }

  private normaliseTitle(value: string | null | undefined): string {
    return (value || '').trim().replace(/\s+/g, ' ').toLowerCase();
  }

  private normaliseIdentifier(
    type: string | null | undefined,
    value: string | null | undefined,
  ): string | null {
    const normalisedType = (type || '').trim().toLowerCase();
    let normalisedValue = (value || '').trim().toLowerCase();
    if (!normalisedType || !normalisedValue) {
      return null;
    }

    if (normalisedType === 'isbn') {
      normalisedValue = normalisedValue.replace(/[-\s]/g, '');
    }

    return `${normalisedType}:${normalisedValue}`;
  }
}
