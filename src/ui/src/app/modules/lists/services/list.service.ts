import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';
import {
  CreateListInviteRequest,
  CreateListRequest,
  CreateShareGroupRequest,
  ListDetail,
  ListInvite,
  ListShareMember,
  ListSummary,
  RedeemListInviteRequest,
  RedeemListInviteResponse,
  ShareGroup,
  ShareListRequest,
  UpdateListRequest,
  UpdateListViewSettingsRequest,
} from '../models/list.model';

@Injectable({
  providedIn: 'root',
})
export class ListService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/lists`;
  private readonly shareGroupsUrl = `${environment.apiBaseUrl}/api/share-groups`;
  private readonly invitesUrl = `${environment.apiBaseUrl}/api/list-invites`;

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

  getShares(listId: string): Observable<ListShareMember[]> {
    return this.http.get<ListShareMember[]>(`${this.baseUrl}/${listId}/shares`);
  }

  share(listId: string, request: ShareListRequest): Observable<ListShareMember[]> {
    return this.http.post<ListShareMember[]>(`${this.baseUrl}/${listId}/shares`, request);
  }

  unshare(listId: string, memberUserId: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(
      `${this.baseUrl}/${listId}/shares/${memberUserId}`,
    );
  }

  leave(listId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/${listId}/shares/leave`, {});
  }

  getShareGroups(): Observable<ShareGroup[]> {
    return this.http.get<ShareGroup[]>(this.shareGroupsUrl);
  }

  createShareGroup(request: CreateShareGroupRequest): Observable<ShareGroup> {
    return this.http.post<ShareGroup>(this.shareGroupsUrl, request);
  }

  deleteShareGroup(groupId: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.shareGroupsUrl}/${groupId}`);
  }

  getInvites(listId: string): Observable<ListInvite[]> {
    return this.http.get<ListInvite[]>(`${this.baseUrl}/${listId}/invites`);
  }

  createInvite(listId: string, request: CreateListInviteRequest = {}): Observable<ListInvite> {
    return this.http.post<ListInvite>(`${this.baseUrl}/${listId}/invites`, request);
  }

  deleteInvite(listId: string, inviteId: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${this.baseUrl}/${listId}/invites/${inviteId}`);
  }

  redeemInvite(request: RedeemListInviteRequest): Observable<RedeemListInviteResponse> {
    return this.http.post<RedeemListInviteResponse>(`${this.invitesUrl}/redeem`, request);
  }
}
