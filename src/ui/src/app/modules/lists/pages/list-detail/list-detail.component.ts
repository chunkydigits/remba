import { DragDropModule, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  BehaviorSubject,
  Observable,
  Subject,
  catchError,
  combineLatest,
  filter,
  map,
  of,
  shareReplay,
  startWith,
  switchMap,
  tap,
} from 'rxjs';
import { ConfirmDialogComponent } from '../../../../components/confirm-dialog/confirm-dialog.component';
import { PageTemplateComponent } from '../../../../components/page-template/page-template.component';
import { MaterialModule } from '../../../../core/material.module';
import { ListSettingsComponent } from '../../components/list-settings/list-settings.component';
import { ListService } from '../../services/list.service';
import { EntryService } from '../../services/entry.service';
import {
  ListDetail,
  ListFieldKey,
  UpdateListRequest,
  UpdateListViewSettingsRequest,
  availableFieldsForEntityType,
  defaultVisibleFields,
} from '../../models/list.model';
import { EntryListQuery, ListEntry } from '../../models/entry.model';

type ToolbarMode = 'closed' | 'menu' | 'search' | 'filter' | 'sort' | 'settings';

@Component({
  selector: 'app-list-detail',
  imports: [
    AsyncPipe,
    ReactiveFormsModule,
    RouterLink,
    DragDropModule,
    MaterialModule,
    PageTemplateComponent,
    ListSettingsComponent,
  ],
  templateUrl: './list-detail.component.html',
  styleUrl: './list-detail.component.scss',
})
export class ListDetailComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly listService = inject(ListService);
  private readonly entryService = inject(EntryService);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly listReload$ = new BehaviorSubject<void>(undefined);
  private readonly saveDetailsRequests = new Subject<UpdateListRequest>();
  private readonly saveViewSettingsRequests = new Subject<UpdateListViewSettingsRequest>();
  private readonly deleteRequests = new Subject<void>();
  private readonly deleteEntryRequests = new Subject<string>();
  private readonly toggleReadRequests = new Subject<ListEntry>();
  private readonly toggleVisitedRequests = new Subject<ListEntry>();
  private readonly toggleSeenRequests = new Subject<ListEntry>();
  private readonly entriesReload$ = new BehaviorSubject<void>(undefined);
  private readonly listFilter$ = new BehaviorSubject<EntryListQuery>({
    sortBy: 'order',
    sortDirection: 'ascending',
  });

  toolbarMode: ToolbarMode = 'closed';
  saveError = '';
  saveSuccess = false;
  deleteError = '';
  entryError = '';
  displayedEntries: ListEntry[] = [];

  readonly searchPanelForm = this.formBuilder.nonNullable.group({
    q: [''],
  });

  readonly filterPanelForm = this.formBuilder.nonNullable.group({
    field: [''],
    value: [''],
  });

  readonly sortPanelForm = this.formBuilder.nonNullable.group({
    sortBy: ['order'],
    sortDirection: ['ascending'],
  });

  readonly listId$: Observable<string | null> = this.route.paramMap.pipe(
    map((params) => params.get('listId')),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly list$: Observable<ListDetail | null> = combineLatest([this.listId$, this.listReload$]).pipe(
    switchMap(([listId]) => {
      if (!listId) {
        return of(null);
      }

      return this.listService.getOwnList(listId).pipe(
        tap((list) => {
          this.sortPanelForm.patchValue(
            {
              sortBy: this.toRuntimeSortBy(list.viewSettings.sortBy),
              sortDirection: list.viewSettings.sortDirection || 'ascending',
            },
            { emitEvent: false },
          );
        }),
        catchError(() => of(null)),
      );
    }),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly entityType$: Observable<string> = this.list$.pipe(
    map((list) => (list?.entityType || '').trim().toLowerCase()),
  );

  readonly isBookList$: Observable<boolean> = this.entityType$.pipe(map((type) => type === 'book'));

  readonly isCountryList$: Observable<boolean> = this.entityType$.pipe(map((type) => type === 'country'));

  readonly isWatchableList$: Observable<boolean> = this.entityType$.pipe(
    map((type) => type === 'film' || type === 'tv'),
  );

  readonly isManualOrder$: Observable<boolean> = this.listFilter$.pipe(
    map((filter) => (filter.sortBy || 'order') === 'order'),
  );

  readonly hasActiveListTools$: Observable<boolean> = this.listFilter$.pipe(
    map((filter) => this.hasActiveListTools(filter)),
  );

  readonly viewMode$: Observable<string> = this.list$.pipe(
    map((list) => (list?.viewSettings.viewMode || 'list').trim().toLowerCase()),
  );

  readonly visibleFields$: Observable<ListFieldKey[]> = this.list$.pipe(
    map((list) => {
      const saved = list?.viewSettings.visibleFields?.filter(Boolean) as ListFieldKey[] | undefined;
      if (saved && saved.length > 0) {
        return saved.includes('name') ? saved : (['name', ...saved] as ListFieldKey[]);
      }
      return defaultVisibleFields(list?.entityType);
    }),
  );

  readonly sortFields$ = this.entityType$.pipe(
    map((type) => {
      const fields = availableFieldsForEntityType(type).filter((field) => field.key !== 'link');
      return [{ key: 'order', label: 'Manual order' }, ...fields];
    }),
  );

  readonly filterFields$ = this.entityType$.pipe(
    map((type) => availableFieldsForEntityType(type).filter((field) => field.key !== 'added')),
  );

  readonly unfilteredEntries$: Observable<ListEntry[]> = combineLatest([
    this.listId$,
    this.entriesReload$,
  ]).pipe(
    switchMap(([listId]) => {
      if (!listId) {
        return of([]);
      }

      return this.entryService
        .getByList(listId, { sortBy: 'order', sortDirection: 'ascending' })
        .pipe(catchError(() => of([])));
    }),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly filterValues$: Observable<string[]> = combineLatest([
    this.filterPanelForm.controls.field.valueChanges.pipe(
      startWith(this.filterPanelForm.controls.field.value),
    ),
    this.unfilteredEntries$,
  ]).pipe(
    map(([field, entries]) => this.deriveFilterValues(field || '', entries)),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly entries$: Observable<ListEntry[]> = combineLatest([
    this.listId$,
    this.listFilter$,
    this.entriesReload$,
  ]).pipe(
    switchMap(([listId, filter]) => {
      if (!listId) {
        return of([]);
      }

      return this.entryService.getByList(listId, filter).pipe(
        tap((entries) => {
          this.displayedEntries = [...entries];
        }),
        catchError(() => {
          this.displayedEntries = [];
          return of([]);
        }),
      );
    }),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly saveDetailsResult$: Observable<ListDetail | null> = this.saveDetailsRequests.pipe(
    switchMap((request) => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId) {
        return of(null);
      }

      this.saveError = '';
      this.saveSuccess = false;
      return this.listService.update(listId, request).pipe(
        tap(() => {
          this.saveSuccess = true;
          this.listReload$.next();
        }),
        catchError((error) => {
          this.saveError = error?.error?.message || 'Unable to update list.';
          return of(null);
        }),
      );
    }),
  );

  readonly saveViewSettingsResult$: Observable<ListDetail | null> = this.saveViewSettingsRequests.pipe(
    switchMap((request) => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId) {
        return of(null);
      }

      this.saveError = '';
      this.saveSuccess = false;
      return this.listService.updateViewSettings(listId, request).pipe(
        tap(() => {
          this.saveSuccess = true;
          this.listReload$.next();
        }),
        catchError((error) => {
          this.saveError = error?.error?.message || 'Unable to update display settings.';
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

  readonly deleteEntryResult$: Observable<boolean | null> = this.deleteEntryRequests.pipe(
    switchMap((entryId) => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId || !entryId) {
        return of(null);
      }

      this.entryError = '';
      return this.entryService.delete(listId, entryId).pipe(
        tap(() => {
          this.entriesReload$.next();
        }),
        map(() => true),
        catchError((error) => {
          this.entryError = error?.error?.message || 'Unable to remove item.';
          return of(false);
        }),
      );
    }),
  );

  readonly toggleReadResult$: Observable<ListEntry | null> = this.toggleReadRequests.pipe(
    switchMap((entry) => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId) {
        return of(null);
      }

      return this.entryService.update(listId, entry.id, { read: !entry.read }).pipe(
        tap(() => {
          this.entriesReload$.next();
        }),
        catchError((error) => {
          this.entryError = error?.error?.message || 'Unable to update read status.';
          return of(null);
        }),
      );
    }),
  );

  readonly toggleVisitedResult$: Observable<ListEntry | null> = this.toggleVisitedRequests.pipe(
    switchMap((entry) => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId) {
        return of(null);
      }

      return this.entryService.update(listId, entry.id, { visited: !entry.visited }).pipe(
        tap(() => {
          this.entriesReload$.next();
        }),
        catchError((error) => {
          this.entryError = error?.error?.message || 'Unable to update visited status.';
          return of(null);
        }),
      );
    }),
  );

  readonly toggleSeenResult$: Observable<ListEntry | null> = this.toggleSeenRequests.pipe(
    switchMap((entry) => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId) {
        return of(null);
      }

      return this.entryService.update(listId, entry.id, { seen: !entry.seen }).pipe(
        tap(() => {
          this.entriesReload$.next();
        }),
        catchError((error) => {
          this.entryError = error?.error?.message || 'Unable to update seen status.';
          return of(null);
        }),
      );
    }),
  );

  toggleToolbar(): void {
    this.toolbarMode = this.toolbarMode === 'closed' ? 'menu' : 'closed';
  }

  openMode(mode: Exclude<ToolbarMode, 'closed'>): void {
    this.toolbarMode = mode;
  }

  backToMenu(): void {
    this.toolbarMode = 'menu';
  }

  onSearchSubmit(): void {
    this.applyFiltersFromPanels();
  }

  onFilterFieldChange(): void {
    this.filterPanelForm.controls.value.setValue('');
  }

  onFilterApply(): void {
    this.applyFiltersFromPanels();
  }

  onFilterClear(): void {
    this.filterPanelForm.reset({ field: '', value: '' });
    this.applyFiltersFromPanels();
  }

  onClearListTools(): void {
    this.searchPanelForm.reset({ q: '' });
    this.filterPanelForm.reset({ field: '', value: '' });
    this.sortPanelForm.reset({ sortBy: 'order', sortDirection: 'ascending' });
    this.applyFiltersFromPanels();
  }

  onSortApply(): void {
    this.applyFiltersFromPanels();
  }

  onSaveDetails(request: UpdateListRequest): void {
    this.saveDetailsRequests.next(request);
  }

  onSaveViewSettings(request: UpdateListViewSettingsRequest): void {
    this.saveViewSettingsRequests.next(request);
  }

  onDeleteList(): void {
    this.deleteRequests.next();
  }

  onDeleteEntry(entry: ListEntry): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        width: '22rem',
        data: {
          title: 'Remove item?',
          message: `Remove “${entry.name}” from this list? This can’t be undone.`,
          confirmLabel: 'Remove',
          cancelLabel: 'Keep',
        },
      })
      .afterClosed()
      .pipe(filter((confirmed): confirmed is true => confirmed === true))
      .subscribe(() => {
        this.deleteEntryRequests.next(entry.id);
      });
  }

  onToggleVisited(entry: ListEntry): void {
    this.toggleVisitedRequests.next(entry);
  }

  onToggleSeen(entry: ListEntry): void {
    this.toggleSeenRequests.next(entry);
  }

  onToggleRead(entry: ListEntry): void {
    this.toggleReadRequests.next(entry);
  }

  formatSeenAt(value: string | null | undefined): string {
    if (!value) {
      return '';
    }

    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
  }

  onDrop(event: CdkDragDrop<ListEntry[]>): void {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const listId = this.route.snapshot.paramMap.get('listId');
    if (!listId) {
      return;
    }

    moveItemInArray(this.displayedEntries, event.previousIndex, event.currentIndex);
    this.entryService
      .reorder(listId, { entryIds: this.displayedEntries.map((entry) => entry.id) })
      .pipe(
        catchError((error) => {
          this.entryError = error?.error?.message || 'Unable to reorder items.';
          this.entriesReload$.next();
          return of(null);
        }),
      )
      .subscribe();
  }

  showsField(fields: ListFieldKey[] | null, key: ListFieldKey): boolean {
    return !!fields?.includes(key);
  }

  titleInitials(name: string | null | undefined): string {
    const parts = (name || '')
      .trim()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2);
    if (parts.length === 0) {
      return '?';
    }

    return parts.map((part) => part[0]?.toUpperCase() || '').join('');
  }

  entryFieldValue(entry: ListEntry, key: ListFieldKey): string {
    switch (key) {
      case 'name':
        return entry.name;
      case 'author':
        return entry.author || '';
      case 'director':
        return entry.director || '';
      case 'actors':
        return entry.actors || '';
      case 'genre':
        return entry.genre || '';
      case 'themes':
        return entry.themes || '';
      case 'link':
        return entry.link || '';
      case 'read':
        return entry.read ? 'Read' : 'Unread';
      case 'visited':
        return entry.visited ? 'Visited' : 'Not visited';
      case 'seen':
        if (!entry.seen) {
          return 'Unseen';
        }
        return entry.seenAt ? `Seen · ${this.formatSeenAt(entry.seenAt)}` : 'Seen';
      case 'added':
        return entry.added ? new Date(entry.added).toLocaleDateString() : '';
      default:
        return '';
    }
  }

  private applyFiltersFromPanels(): void {
    const search = this.searchPanelForm.getRawValue();
    const filter = this.filterPanelForm.getRawValue();
    const sort = this.sortPanelForm.getRawValue();

    const query: EntryListQuery = {
      q: search.q.trim() || undefined,
      sortBy: sort.sortBy || 'order',
      sortDirection: sort.sortDirection || 'ascending',
    };

    const field = (filter.field || '').trim();
    const value = (filter.value || '').trim();
    if (field && value) {
      if (field === 'author') {
        query.author = value;
      } else if (field === 'director') {
        query.director = value;
      } else if (field === 'actors') {
        query.actors = value;
      } else if (field === 'genre') {
        query.genre = value;
      } else if (field === 'themes') {
        query.themes = value;
      } else if (field === 'read') {
        query.read = value.toLowerCase() === 'read';
      } else if (field === 'visited') {
        query.visited = value.toLowerCase() === 'visited';
      } else if (field === 'seen') {
        query.seen = value.toLowerCase() === 'seen';
      } else if (field === 'name' || field === 'link') {
        query.q = value;
      }
    }

    this.listFilter$.next(query);
  }

  private hasActiveListTools(filter: EntryListQuery): boolean {
    const sortBy = filter.sortBy || 'order';
    const sortDirection = filter.sortDirection || 'ascending';
    const nonDefaultSort = sortBy !== 'order' || sortDirection !== 'ascending';

    return (
      !!filter.q?.trim() ||
      !!filter.author?.trim() ||
      !!filter.director?.trim() ||
      !!filter.actors?.trim() ||
      !!filter.genre?.trim() ||
      !!filter.themes?.trim() ||
      filter.read === true ||
      filter.read === false ||
      filter.visited === true ||
      filter.visited === false ||
      filter.seen === true ||
      filter.seen === false ||
      nonDefaultSort
    );
  }

  private deriveFilterValues(field: string, entries: ListEntry[]): string[] {
    const key = field as ListFieldKey;
    if (!key) {
      return [];
    }

    if (key === 'read') {
      return ['Read', 'Unread'];
    }

    if (key === 'visited') {
      return ['Visited', 'Not visited'];
    }

    if (key === 'seen') {
      return ['Seen', 'Unseen'];
    }

    const values = new Set<string>();
    for (const entry of entries) {
      const raw = this.entryFieldValue(entry, key).trim();
      if (!raw) {
        continue;
      }

      if (key === 'actors' || key === 'director' || key === 'author' || key === 'genre' || key === 'themes') {
        for (const part of raw.split(',')) {
          const trimmed = part.trim();
          if (trimmed) {
            values.add(trimmed);
          }
        }
      } else {
        values.add(raw);
      }
    }

    return [...values].sort((a, b) => a.localeCompare(b));
  }

  private toRuntimeSortBy(sortBy: string): string {
    return sortBy === 'manual' ? 'order' : sortBy || 'order';
  }
}
