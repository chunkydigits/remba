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

export type ListAttributeType = 'text' | 'number' | 'boolean' | 'url';

export interface ListAttributeDefinition {
  key: string;
  name: string;
  description?: string | null;
  type: ListAttributeType | string;
  scope?: string;
}

export interface ListDetail {
  id: string;
  ownerId: string;
  name: string;
  description: string;
  visibility: 'private' | 'public' | string;
  entityType?: string | null;
  attributes?: ListAttributeDefinition[];
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
  attributes?: Array<{
    key?: string;
    name: string;
    description?: string;
    type: ListAttributeType | string;
  }>;
}

export const LIST_ATTRIBUTE_TYPE_OPTIONS: Array<{ value: ListAttributeType; label: string }> = [
  { value: 'text', label: 'Text' },
  { value: 'number', label: 'Number' },
  { value: 'boolean', label: 'True / false' },
  { value: 'url', label: 'URL' },
];

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
  | 'genre'
  | 'themes'
  | 'link'
  | 'read'
  | 'visited'
  | 'seen'
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
  { key: 'genre', label: 'Genre' },
  { key: 'themes', label: 'Themes' },
  { key: 'link', label: 'Link' },
  { key: 'read', label: 'Read' },
  { key: 'visited', label: 'Visited' },
  { key: 'seen', label: 'Seen' },
  { key: 'added', label: 'Date added' },
];

export function defaultVisibleFields(entityType: string | null | undefined): ListFieldKey[] {
  switch ((entityType || '').trim().toLowerCase()) {
    case 'book':
      return ['name', 'author', 'genre', 'read'];
    case 'film':
    case 'tv':
      return ['name', 'director', 'genre', 'seen'];
    case 'country':
      return ['name', 'visited'];
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
    keys.add('genre');
    keys.add('themes');
    keys.add('read');
  }
  if (type === 'film' || type === 'tv') {
    keys.add('director');
    keys.add('actors');
    keys.add('genre');
    keys.add('seen');
  }
  if (type === 'country') {
    keys.add('visited');
  }
  if (!type || type === 'recipe') {
    keys.add('link');
  }

  return LIST_FIELD_OPTIONS.filter((option) => keys.has(option.key));
}
