export interface EntitySearchResult {
  provider: string;
  entityId?: string | null;
  identifierType: string;
  identifierValue: string;
  name: string;
  year?: string | null;
  subtitle?: string | null;
  author?: string | null;
  director?: string | null;
  actors?: string | null;
  genre?: string | null;
  themes?: string | null;
  imageUrl?: string | null;
  link?: string | null;
  alreadyOnList?: boolean;
}

export interface EntityIdentifier {
  type: string;
  value: string;
}
