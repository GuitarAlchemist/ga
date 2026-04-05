import type { AssetQualityTier, CanonicalBodyId } from './assetTypes';
import { resolveTextureSet } from './loadTextureSet';

export interface PlanetTextureMaterialConfig {
  bodyId: CanonicalBodyId;
  quality: AssetQualityTier;
  albedo?: string;
  night?: string;
  clouds?: string;
  specular?: string;
  displacement?: string;
  alpha?: string;
  stars?: string;
}

export function createPlanetTextureMaterialConfig(
  bodyId: CanonicalBodyId,
  quality: AssetQualityTier = '2k',
): PlanetTextureMaterialConfig {
  const textures = resolveTextureSet(bodyId, quality);

  return {
    bodyId,
    quality,
    albedo: textures.albedo,
    night: textures.night,
    clouds: textures.clouds,
    specular: textures.specular,
    displacement: textures.displacement,
    alpha: textures.alpha,
    stars: textures.stars,
  };
}

