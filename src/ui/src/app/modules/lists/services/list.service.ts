import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  CreateListRequest,
  ListDetail,
  ListSummary,
  UpdateListRequest,
  UpdateListViewSettingsRequest,
} from '../models/list.model';

@Injectable({
  providedIn: 'root',
})
export class ListService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/lists`;

  getOwnLists(): Observable<ListSummary[]> {
    return this.http.get<ListSummary[]>(this.baseUrl);
  }

  getOwnList(listId: string): Observable<ListDetail> {
    return this.http.get<ListDetail>(`${this.baseUrl}/${listId}`);
  }

  create(request: CreateListRequest): Observable<ListDetail> {
    return this.http.post<ListDetail>(this.baseUrl, request);
  }

  update(listId: string, request: UpdateListRequest): Observable<ListDetail> {
    return this.http.put<ListDetail>(`${this.baseUrl}/${listId}`, request);
  }

  updateViewSettings(
    listId: string,
    request: UpdateListViewSettingsRequest,
  ): Observable<ListDetail> {
    return this.http.put<ListDetail>(`${this.baseUrl}/${listId}/view-settings`, request);
  }

  delete(listId: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/${listId}`);
  }
}
