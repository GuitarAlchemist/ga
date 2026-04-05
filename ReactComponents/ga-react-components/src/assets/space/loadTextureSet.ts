import type { AssetQualityTier, CanonicalBodyId, TextureMapKind, TextureAssetSet } from './assetTypes';
import { SOLAR_SYSTEM_MANIFEST } from './solarSystemManifest';

const qualityPreference: AssetQualityTier[] = ['8k', '4k', '2k', '1k'];

export function getCanonicalBodyAsset(id: CanonicalBodyId): TextureAssetSet {
  return SOLAR_SYSTEM_MANIFEST[id];
}

export function resolveTexturePath(
  id: CanonicalBodyId,
  mapKind: TextureMapKind,
  preferredTier: AssetQualityTier = '2k',
): string | undefined {
  const asset = SOLAR_SYSTEM_MANIFEST[id];
  const tierSet = asset.textures[mapKind];

  if (!tierSet) return undefined;
  if (tierSet[preferredTier]) return tierSet[preferredTier];

  const preferredIndex = qualityPreference.indexOf(preferredTier);
  const fallbackOrder = [
    ...qualityPreference.slice(preferredIndex + 1),
    ...qualityPreference.slice(0, preferredIndex),
  ];

  for (const tier of fallbackOrder) {
    const candidate = tierSet[tier];
    if (candidate) return candidate;
  }

  return undefined;
}

export function resolveTextureSet(
  id: CanonicalBodyId,
  preferredTier: AssetQualityTier = '2k',
): Partial<Record<TextureMapKind, string>> {
  const asset = SOLAR_SYSTEM_MANIFEST[id];
  const result: Partial<Record<TextureMapKind, string>> = {};

  for (const mapKind of Object.keys(asset.textures) as TextureMapKind[]) {
    const resolved = resolveTexturePath(id, mapKind, preferredTier);
    if (resolved) {
      result[mapKind] = resolved;
    }
  }

  return result;
}

