export interface EntityType {
  key: string;
  name: string;
  visibility: 'system' | 'public' | 'private' | string;
  supportsLookup: boolean;
  added: string;
  edited: string | null;
}

export interface CreateEntityTypeRequest {
  name: string;
}
