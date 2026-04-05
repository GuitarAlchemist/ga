export type AssetQualityTier = '1k' | '2k' | '4k' | '8k';

export type TextureMapKind =
  | 'albedo'
  | 'night'
  | 'clouds'
  | 'specular'
  | 'displacement'
  | 'emissive'
  | 'alpha'
  | 'atmosphere'
  | 'stars'
  | 'overlay';

export type CanonicalBodyId =
  | 'sun'
  | 'mercury'
  | 'venus'
  | 'earth'
  | 'moon'
  | 'mars'
  | 'jupiter'
  | 'saturn'
  | 'uranus'
  | 'neptune'
  | 'milky-way'
  | 'stars';

export interface TextureTierSet {
  '1k'?: string;
  '2k'?: string;
  '4k'?: string;
  '8k'?: string;
}

export interface TextureAssetSet {
  id: CanonicalBodyId;
  displayName: string;
  source: 'Solar System Scope' | 'Internal';
  sourceUrl: string;
  license: string;
  textures: Partial<Record<TextureMapKind, TextureTierSet>>;
  notes?: string;
}

export interface ModelPlacement {
  anchor: 'scene' | 'solarSystem' | 'planet';
  targetName?: string;
  position?: [number, number, number];
  rotation?: [number, number, number];
  scale?: number;
}

export interface ModelAsset {
  id: string;
  displayName: string;
  source: 'BlenderKit';
  sourceUrl: string;
  license: string;
  author: string;
  runtimeFormat: 'glb';
  category: 'planet' | 'terrain' | 'station' | 'prop';
  approvedForRuntime: boolean;
  usedIn: string[];
  polyBudget?: number;
  path?: string;
  placement?: ModelPlacement;
  notes?: string;
}
