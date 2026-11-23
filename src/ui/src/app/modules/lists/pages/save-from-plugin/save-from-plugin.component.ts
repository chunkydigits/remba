import { AsyncPipe } from '@angular/common';
import { Component, HostListener, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  Observable,
  catchError,
  finalize,
  map,
  of,
  shareReplay,
  switchMap,
} from 'rxjs';
import { MaterialModule } from '../../../../core/material.module';
import { EntryService } from '../../services/entry.service';
import { ListService } from '../../services/list.service';
import { ListSummary } from '../../models/list.model';

interface PluginPayload {
  name: string;
  link: string;
  imageUrl: string;
  author: string;
  referral: string;
  preferredListId: string;
}

@Component({
  selector: 'app-save-from-plugin',
  imports: [AsyncPipe, MaterialModule, RouterLink],
  templateUrl: './save-from-plugin.component.html',
  styleUrl: './save-from-plugin.component.scss',
})
export class SaveFromPluginComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly listService = inject(ListService);
  private readonly entryService = inject(EntryService);

  listsOpen = false;
  savingListId: string | null = null;
  savedListIds = new Set<string>();
  error = '';

  readonly payload$: Observable<PluginPayload> = this.route.queryParamMap.pipe(
    map((params) => ({
      name: (params.get('name') || '').trim(),
      link: (params.get('link') || '').trim(),
      imageUrl: (params.get('imageUrl') || '').trim(),
      author: (params.get('author') || '').trim(),
      referral: (params.get('referral') || '').trim(),
      preferredListId: (params.get('listId') || '').trim(),
    })),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly lists$: Observable<ListSummary[]> = this.payload$.pipe(
    switchMap((payload) =>
      this.listService.getOwnLists().pipe(
        map((lists) => {
          const sorted = [...lists].sort((a, b) => a.name.localeCompare(b.name));
          if (!payload.preferredListId) {
            return sorted;
          }

          return sorted.sort((a, b) => {
            if (a.id === payload.preferredListId) {
              return -1;
            }
            if (b.id === payload.preferredListId) {
              return 1;
            }
            return a.name.localeCompare(b.name);
          });
        }),
        catchError(() => of([])),
      ),
    ),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.listsOpen = false;
  }

  toggleLists(): void {
    this.listsOpen = !this.listsOpen;
    this.error = '';
  }

  isSaved(listId: string): boolean {
    return this.savedListIds.has(listId);
  }

  addToList(list: ListSummary, payload: PluginPayload): void {
    if (!payload.name || this.savingListId || this.savedListIds.has(list.id)) {
      return;
    }

    this.savingListId = list.id;
    this.error = '';

    this.entryService
      .create(list.id, {
        name: payload.name,
        link: payload.link || undefined,
        imageUrl: payload.imageUrl || undefined,
        author: payload.author || undefined,
      })
      .pipe(
        switchMap(() => {
          if (!payload.referral) {
            return of(null);
          }

          return this.listService.update(list.id, { referralLink: payload.referral }).pipe(
            catchError(() => of(null)),
          );
        }),
        finalize(() => {
          this.savingListId = null;
        }),
      )
      .subscribe({
        next: () => {
          this.savedListIds = new Set(this.savedListIds).add(list.id);
        },
        error: () => {
          this.error = `Could not add to “${list.name}”. Try again.`;
        },
      });
  }
}
