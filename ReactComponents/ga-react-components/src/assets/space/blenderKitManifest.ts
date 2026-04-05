import type { ModelAsset } from './assetTypes';

export const BLENDERKIT_MODEL_MANIFEST: ModelAsset[] = [
  {
    id: 'moon-terrain-hero',
    displayName: 'Moon Terrain',
    source: 'BlenderKit',
    sourceUrl: 'https://www.blenderkit.com/asset-gallery-detail/35fc6b0c-b1eb-4c32-a2aa-89bec975e3a1/',
    license: 'Royalty Free',
    author: 'TBD',
    runtimeFormat: 'glb',
    category: 'terrain',
    approvedForRuntime: false,
    usedIn: ['prime-radiant'],
    path: '/models/blenderkit/moon-terrain-hero.glb',
    placement: {
      anchor: 'planet',
      targetName: 'moon',
      position: [0.35, 0.08, 0.2],
      rotation: [0, Math.PI * 0.35, 0],
      scale: 0.12,
    },
    notes: 'Chosen first BlenderKit hero asset. Keep approval off until the reviewed GLB is exported locally and provenance is confirmed.',
  },
];
