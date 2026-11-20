import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { EntitySearchResult } from '../models/entity.model';

@Injectable({
  providedIn: 'root',
})
export class EntityService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/entities`;

  search(entityType: string, query: string): Observable<EntitySearchResult[]> {
    const params = new HttpParams().set('entityType', entityType).set('q', query);
    return this.http.get<EntitySearchResult[]>(`${this.baseUrl}/search`, { params });
  }
}
