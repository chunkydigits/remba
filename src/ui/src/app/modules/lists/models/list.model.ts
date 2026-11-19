export interface ListSummary {
  id: string;
  name: string;
  description: string;
  visibility: 'private' | 'public' | string;
  entityType?: string | null;
  added: string;
  edited: string | null;
}

export interface ListViewSettings {
  sortBy: string;
  sortDirection: 'ascending' | 'descending' | string;
  viewMode: 'list' | 'grid' | string;
}

export interface ListDetail {
  id: string;
  ownerId: string;
  name: string;
  description: string;
  visibility: 'private' | 'public' | string;
  entityType?: string | null;
  viewSettings: ListViewSettings;
  added: string;
  edited: string | null;
}

export interface CreateListRequest {
  name: string;
  description?: string;
  entityType?: string;
  visibility?: string;
}

export interface UpdateListRequest {
  name?: string;
  description?: string;
  entityType?: string;
  visibility?: string;
}

export interface UpdateListViewSettingsRequest {
  sortBy?: string;
  sortDirection?: string;
  viewMode?: string;
}
