import { EntityIdentifier } from './entity.model';

export interface ListEntry {
  id: string;
  listId: string;
  entityId: string;
  identifierType?: string | null;
  identifierValue?: string | null;
  name: string;
  link?: string | null;
  author?: string | null;
  director?: string | null;
  actors?: string | null;
  read?: boolean | null;
  order: number;
  attributes: Record<string, unknown>;
  added: string;
  edited: string | null;
}

export interface CreateEntryRequest {
  name: string;
  identifier?: EntityIdentifier;
  link?: string;
  author?: string;
  director?: string;
  actors?: string;
  read?: boolean;
  metadata?: Record<string, string>;
  attributes?: Record<string, unknown>;
}

export interface UpdateEntryRequest {
  name?: string;
  link?: string;
  author?: string;
  director?: string;
  actors?: string;
  read?: boolean;
  attributes?: Record<string, unknown>;
}

export interface EntryListQuery {
  q?: string;
  sortBy?: string;
  sortDirection?: 'ascending' | 'descending' | string;
  author?: string;
  director?: string;
  actors?: string;
  read?: boolean;
}

export interface ReorderEntriesRequest {
  entryIds: string[];
}
