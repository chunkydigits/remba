import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { SuggestedListDetail } from '../models/suggested-list.model';

@Injectable({
  providedIn: 'root',
})
export class SuggestedListService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/suggested-lists`;

  getAll(): Observable<SuggestedListDetail[]> {
    return this.http.get<SuggestedListDetail[]>(this.baseUrl);
  }
}
