import { EntityIdentifier } from './entity.model';

export interface WatchProvider {
  providerId: number;
  name: string;
  logoUrl?: string | null;
  offerType: string;
  displayPriority: number;
}

export interface ListEntry {
  id: string;
  listId: string;
  entityId: string;
  identifierType?: string | null;
  identifierValue?: string | null;
  name: string;
  link?: string | null;
  imageUrl?: string | null;
  author?: string | null;
  director?: string | null;
  actors?: string | null;
  genre?: string | null;
  themes?: string | null;
  read?: boolean | null;
  visited?: boolean | null;
  seen?: boolean | null;
  seenAt?: string | null;
  watchProviders?: WatchProvider[];
  watchProvidersRegion?: string | null;
  watchProvidersLink?: string | null;
  watchProvidersFetchedAt?: string | null;
  order: number;
  attributes: Record<string, unknown>;
  added: string;
  addedByUserId?: string;
  addedByUsername?: string | null;
  addedByDisplayName?: string | null;
  edited: string | null;
}

export interface CreateEntryRequest {
  name: string;
  identifier?: EntityIdentifier;
  entityId?: string;
  link?: string;
  imageUrl?: string;
  author?: string;
  director?: string;
  actors?: string;
  genre?: string;
  themes?: string;
  read?: boolean;
  visited?: boolean;
  seen?: boolean;
  seenAt?: string;
  metadata?: Record<string, string>;
  attributes?: Record<string, unknown>;
}

export interface UpdateEntryRequest {
  name?: string;
  link?: string;
  imageUrl?: string;
  author?: string;
  director?: string;
  actors?: string;
  genre?: string;
  themes?: string;
  read?: boolean;
  visited?: boolean;
  seen?: boolean;
  seenAt?: string | null;
  attributes?: Record<string, unknown>;
}

export interface EntryListQuery {
  q?: string;
  sortBy?: string;
  sortDirection?: 'ascending' | 'descending' | string;
  author?: string;
  director?: string;
  actors?: string;
  genre?: string;
  themes?: string;
  read?: boolean;
  visited?: boolean;
  seen?: boolean;
}

export interface ReorderEntriesRequest {
  entryIds: string[];
}
