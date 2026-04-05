import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import type { ModelAsset } from './assetTypes';
import { BLENDERKIT_MODEL_MANIFEST } from './blenderKitManifest';

const gltfLoader = new GLTFLoader();
const runtimeAssetCache = new Map<string, THREE.Group>();

function cloneMaterial<T extends THREE.Material | THREE.Material[]>(material: T): T {
  if (Array.isArray(material)) {
    return material.map(item => item.clone()) as T;
  }
  return material.clone() as T;
}

function prepareModel(instance: THREE.Group, asset: ModelAsset): THREE.Group {
  const box = new THREE.Box3().setFromObject(instance);
  const center = box.getCenter(new THREE.Vector3());
  instance.position.sub(center);

  instance.traverse((child) => {
    if (!(child instanceof THREE.Mesh)) return;
    child.castShadow = true;
    child.receiveShadow = true;
    child.material = cloneMaterial(child.material);
  });

  const scale = asset.placement?.scale ?? 1;
  instance.scale.multiplyScalar(scale);

  const position = asset.placement?.position ?? [0, 0, 0];
  instance.position.add(new THREE.Vector3(...position));

  const rotation = asset.placement?.rotation ?? [0, 0, 0];
  instance.rotation.set(rotation[0], rotation[1], rotation[2]);

  instance.name = `runtime-asset-${asset.id}`;
  instance.userData.runtimeAsset = {
    id: asset.id,
    source: asset.source,
    license: asset.license,
  };

  return instance;
}

async function loadModel(path: string): Promise<THREE.Group> {
  const cached = runtimeAssetCache.get(path);
  if (cached) {
    return cached.clone(true);
  }

  const scene = await new Promise<THREE.Group>((resolve, reject) => {
    gltfLoader.load(
      path,
      (gltf) => resolve(gltf.scene),
      undefined,
      reject,
    );
  });

  runtimeAssetCache.set(path, scene.clone(true));
  return scene;
}

function resolveAnchor(
  asset: ModelAsset,
  scene: THREE.Scene,
  solarSystem?: THREE.Group,
): THREE.Object3D | null {
  switch (asset.placement?.anchor) {
    case 'scene':
      return scene;
    case 'solarSystem':
      return solarSystem ?? null;
    case 'planet':
      if (!solarSystem || !asset.placement.targetName) return null;
      return solarSystem.getObjectByName(asset.placement.targetName)?.parent ?? null;
    default:
      return scene;
  }
}

export function getApprovedRuntimeAssets(): ModelAsset[] {
  return BLENDERKIT_MODEL_MANIFEST.filter(
    asset => asset.approvedForRuntime && !!asset.path && !!asset.placement,
  );
}

export async function mountApprovedRuntimeAssets(
  scene: THREE.Scene,
  solarSystem?: THREE.Group,
): Promise<() => void> {
  const approvedAssets = getApprovedRuntimeAssets();
  if (approvedAssets.length === 0) {
    return () => {};
  }

  const mounted: THREE.Object3D[] = [];

  await Promise.allSettled(
    approvedAssets.map(async (asset) => {
      const anchor = resolveAnchor(asset, scene, solarSystem);
      if (!anchor || !asset.path) {
        console.warn(`[SpaceAssets] Missing anchor or path for runtime asset ${asset.id}`);
        return;
      }

      const loaded = await loadModel(asset.path);
      const instance = prepareModel(loaded, asset);
      anchor.add(instance);
      mounted.push(instance);
    }),
  );

  return () => {
    for (const object of mounted) {
      object.parent?.remove(object);
      object.traverse((child) => {
        if (!(child instanceof THREE.Mesh)) return;
        child.geometry.dispose();
        if (Array.isArray(child.material)) {
          for (const material of child.material) material.dispose();
        } else {
          child.material.dispose();
        }
      });
    }
  };
}
