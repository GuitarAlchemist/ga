export type MusicHierarchyLevel =
  | 'SetClass'
  | 'ForteNumber'
  | 'PrimeForm'
  | 'Chord'
  | 'ChordVoicing'
  | 'Scale';

export interface MusicHierarchyLevelInfo {
  level: MusicHierarchyLevel;
  displayName: string;
  description: string;
  totalItems: number;
  primaryMetric: string;
  highlights: string[];
}

export interface MusicHierarchyItem {
  id: string;
  name: string;
  level: MusicHierarchyLevel;
  category: string;
  description?: string | null;
  tags: string[];
  metadata: Record<string, string>;
}
