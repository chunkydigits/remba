export type ListShareRole = 'owner' | 'editor' | 'viewer' | string;

export interface ListShareMember {
  userId: string;
  username: string;
  displayName: string;
  role: ListShareRole;
  shareLabel?: string | null;
  added: string;
}

export interface ShareListRequest {
  usernames?: string[];
  shareGroupId?: string;
  role?: 'editor' | 'viewer' | string;
  shareLabel?: string;
}

export interface ShareGroupMember {
  userId: string;
  username: string;
  displayName: string;
}

export interface ShareGroup {
  id: string;
  name: string;
  members: ShareGroupMember[];
  added: string;
  edited: string | null;
}

export interface CreateShareGroupRequest {
  name: string;
  usernames?: string[];
}

export interface ListSummary {
  id: string;
  name: string;
  description: string;
  visibility: 'private' | 'public' | string;
  entityType?: string | null;
  referralLink?: string | null;
  myRole?: ListShareRole;
  isOwned?: boolean;
  shareLabel?: string | null;
  added: string;
  edited: string | null;
}

export interface ListViewSettings {
  sortBy: string;
  sortDirection: 'ascending' | 'descending' | string;
  viewMode: 'list' | 'grid' | string;
  visibleFields?: string[];
}

export type ListAttributeType = 'text' | 'number' | 'boolean' | 'url' | 'list';

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
  referralLink?: string | null;
  attributes?: ListAttributeDefinition[];
  viewSettings: ListViewSettings;
  myRole?: ListShareRole;
  isOwned?: boolean;
  shareLabel?: string | null;
  members?: ListShareMember[];
  added: string;
  edited: string | null;
}

export interface CreateListRequest {
  name: string;
  description?: string;
  entityType?: string;
  visibility?: string;
  referralLink?: string;
}

export interface UpdateListRequest {
  name?: string;
  description?: string;
  entityType?: string;
  visibility?: string;
  referralLink?: string;
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
  { value: 'list', label: 'List (comma-separated)' },
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
  | 'added'
  | 'addedBy';

export interface ListInvite {
  id: string;
  listId: string;
  listName: string;
  code: string;
  role: ListShareRole;
  added: string;
  isUsed: boolean;
  usedAt?: string | null;
  usedByUserId?: string | null;
  usedByUsername?: string | null;
  usedByDisplayName?: string | null;
}

export interface CreateListInviteRequest {
  role?: 'editor' | 'viewer' | string;
}

export interface RedeemListInviteRequest {
  code: string;
}

export interface RedeemListInviteResponse {
  listId: string;
  listName: string;
  role: ListShareRole;
}

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
  { key: 'addedBy', label: 'Added by' },
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
  const keys = new Set<ListFieldKey>(['name', 'link', 'added', 'addedBy']);

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
