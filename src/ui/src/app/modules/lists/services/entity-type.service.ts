import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CreateEntityTypeRequest, EntityType } from '../models/entity-type.model';

@Injectable({
  providedIn: 'root',
})
export class EntityTypeService {
  private readonly baseUrl = `${environment.apiBaseUrl}/api/entity-types`;

  constructor(private readonly http: HttpClient) {}

  getAvailable(): Observable<EntityType[]> {
    return this.http.get<EntityType[]>(this.baseUrl);
  }

  create(request: CreateEntityTypeRequest): Observable<EntityType> {
    return this.http.post<EntityType>(this.baseUrl, request);
  }
}
