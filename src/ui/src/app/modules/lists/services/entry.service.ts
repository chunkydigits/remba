import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  CreateEntryRequest,
  EntryListQuery,
  ListEntry,
  ReorderEntriesRequest,
  UpdateEntryRequest,
} from '../models/entry.model';

@Injectable({
  providedIn: 'root',
})
export class EntryService {
  constructor(private readonly http: HttpClient) {}

  private baseUrl(listId: string): string {
    return `${environment.apiBaseUrl}/api/lists/${listId}/entries`;
  }

  getByList(listId: string, query?: EntryListQuery): Observable<ListEntry[]> {
    let params = new HttpParams();
    if (query?.q) {
      params = params.set('q', query.q);
    }
    if (query?.sortBy) {
      params = params.set('sortBy', query.sortBy);
    }
    if (query?.sortDirection) {
      params = params.set('sortDirection', query.sortDirection);
    }
    if (query?.author) {
      params = params.set('author', query.author);
    }
    if (query?.director) {
      params = params.set('director', query.director);
    }
    if (query?.actors) {
      params = params.set('actors', query.actors);
    }
    if (query?.genre) {
      params = params.set('genre', query.genre);
    }
    if (query?.themes) {
      params = params.set('themes', query.themes);
    }
    if (query?.read === true || query?.read === false) {
      params = params.set('read', String(query.read));
    }
    if (query?.visited === true || query?.visited === false) {
      params = params.set('visited', String(query.visited));
    }
    if (query?.seen === true || query?.seen === false) {
      params = params.set('seen', String(query.seen));
    }

    return this.http.get<ListEntry[]>(this.baseUrl(listId), { params });
  }

  get(listId: string, entryId: string): Observable<ListEntry> {
    return this.http.get<ListEntry>(`${this.baseUrl(listId)}/${entryId}`);
  }

  create(listId: string, request: CreateEntryRequest): Observable<ListEntry> {
    return this.http.post<ListEntry>(this.baseUrl(listId), request);
  }

  update(listId: string, entryId: string, request: UpdateEntryRequest): Observable<ListEntry> {
    return this.http.put<ListEntry>(`${this.baseUrl(listId)}/${entryId}`, request);
  }

  reorder(listId: string, request: ReorderEntriesRequest): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`${this.baseUrl(listId)}/reorder`, request);
  }

  delete(listId: string, entryId: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl(listId)}/${entryId}`);
  }
}
