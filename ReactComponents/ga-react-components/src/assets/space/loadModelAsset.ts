import type { ModelAsset } from './assetTypes';
import { BLENDERKIT_MODEL_MANIFEST } from './blenderKitManifest';

export function getModelAsset(id: string): ModelAsset | undefined {
  return BLENDERKIT_MODEL_MANIFEST.find(asset => asset.id === id);
}

export function getApprovedModelAssets(): ModelAsset[] {
  return BLENDERKIT_MODEL_MANIFEST.filter(asset => asset.approvedForRuntime && !!asset.path);
}

export function getApprovedModelAsset(id: string): ModelAsset {
  const asset = getModelAsset(id);
  if (!asset) {
    throw new Error(`Unknown model asset: ${id}`);
  }
  if (!asset.approvedForRuntime || !asset.path) {
    throw new Error(`Model asset is not approved for runtime: ${id}`);
  }
  return asset;
}
