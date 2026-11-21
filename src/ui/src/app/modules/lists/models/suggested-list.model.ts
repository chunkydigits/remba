export interface SuggestedListSummary {
  seedKey: string;
  name: string;
  description: string;
  sortOrder: number;
  enabled: boolean;
  entityType?: string | null;
}

export interface SuggestedListDetail extends SuggestedListSummary {
  identifier: {
    type: string;
    label: string;
    urlTemplate: string;
  };
  enrichment: {
    provider: string;
    availableAttributes: Array<{ key: string; name: string; type: string }>;
    defaultSelectedAttributeKeys: string[];
  };
}
