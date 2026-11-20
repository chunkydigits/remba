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
  visibleFields?: string[];
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
  visibleFields?: string[];
}

export type ListFieldKey =
  | 'name'
  | 'author'
  | 'director'
  | 'actors'
  | 'link'
  | 'read'
  | 'added';

export interface ListFieldOption {
  key: ListFieldKey;
  label: string;
}

export const LIST_FIELD_OPTIONS: ListFieldOption[] = [
  { key: 'name', label: 'Name' },
  { key: 'author', label: 'Author' },
  { key: 'director', label: 'Director' },
  { key: 'actors', label: 'Actors' },
  { key: 'link', label: 'Link' },
  { key: 'read', label: 'Read' },
  { key: 'added', label: 'Date added' },
];

export function defaultVisibleFields(entityType: string | null | undefined): ListFieldKey[] {
  switch ((entityType || '').trim().toLowerCase()) {
    case 'book':
      return ['name', 'author', 'read'];
    case 'film':
    case 'tv':
      return ['name', 'director', 'actors'];
    case 'recipe':
      return ['name', 'link'];
    default:
      return ['name', 'link'];
  }
}

export function availableFieldsForEntityType(entityType: string | null | undefined): ListFieldOption[] {
  const type = (entityType || '').trim().toLowerCase();
  const keys = new Set<ListFieldKey>(['name', 'link', 'added']);

  if (type === 'book') {
    keys.add('author');
    keys.add('read');
  }
  if (type === 'film' || type === 'tv') {
    keys.add('director');
    keys.add('actors');
  }
  if (!type || type === 'recipe') {
    keys.add('link');
  }

  return LIST_FIELD_OPTIONS.filter((option) => keys.has(option.key));
}
