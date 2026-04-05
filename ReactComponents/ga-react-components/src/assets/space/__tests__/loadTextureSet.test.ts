import { describe, expect, it } from 'vitest';
import { getCanonicalBodyAsset, resolveTexturePath, resolveTextureSet } from '../loadTextureSet';
import { getApprovedModelAssets } from '../loadModelAsset';

describe('space asset manifest', () => {
  it('resolves Earth canonical maps', () => {
    const earth = getCanonicalBodyAsset('earth');
    expect(earth.license).toBe('CC BY 4.0');
    expect(resolveTexturePath('earth', 'albedo', '2k')).toBe('/textures/planets/2k_earth_daymap.jpg');
    expect(resolveTexturePath('earth', 'night', '2k')).toBe('/textures/planets/2k_earth_nightmap.jpg');
  });

  it('resolves Saturn ring alpha map', () => {
    expect(resolveTexturePath('saturn', 'alpha', '2k')).toBe('/textures/planets/2k_saturn_ring_alpha.png');
  });

  it('returns a compact texture set for stars', () => {
    expect(resolveTextureSet('stars', '2k')).toEqual({
      stars: '/textures/planets/2k_stars.jpg',
    });
  });

  it('prefers the highest available Milky Way panorama tier and falls back cleanly', () => {
    expect(resolveTexturePath('milky-way', 'stars', '8k')).toBe('/textures/milky-way-8k.jpg');
    expect(resolveTexturePath('milky-way', 'stars', '4k')).toBe('/textures/milky-way-2k.jpg');
  });

  it('keeps BlenderKit runtime empty until an asset is explicitly approved', () => {
    expect(getApprovedModelAssets()).toEqual([]);
  });
});
