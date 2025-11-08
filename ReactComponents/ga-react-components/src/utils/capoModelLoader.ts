import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

// Cache for loaded capo models to avoid reloading
const capoModelCache = new Map<string, THREE.Group>();

// GLTF Loader instance (reusable)
const gltfLoader = new GLTFLoader();

export interface CapoModelConfig {
  modelPath: string;
  scale?: number;
  position?: THREE.Vector3;
  rotation?: THREE.Euler;
  color?: number;
  metalness?: number;
  roughness?: number;
}

/**
 * Load a 3D capo model from GLB/GLTF file
 */
export const loadCapoModel = async (config: CapoModelConfig): Promise<THREE.Group> => {
  const { modelPath, scale = 1, position, rotation, color, metalness, roughness } = config;
  
  // Check cache first
  const cacheKey = `${modelPath}_${scale}_${color || 'default'}`;
  if (capoModelCache.has(cacheKey)) {
    const cachedModel = capoModelCache.get(cacheKey)!.clone();
    if (position) cachedModel.position.copy(position);
    if (rotation) cachedModel.rotation.copy(rotation);
    return cachedModel;
  }

  // Load GLTF/GLB model
  return new Promise((resolve, reject) => {
    gltfLoader.load(
      modelPath,
      (gltf) => {
        const model = gltf.scene;

        // Center and scale the model
        const box = new THREE.Box3().setFromObject(model);
        const center = box.getCenter(new THREE.Vector3());
        const size = box.getSize(new THREE.Vector3());

        // Center the model
        model.position.sub(center);

        // Scale the model
        model.scale.multiplyScalar(scale);

        // Apply material modifications if specified
        if (color !== undefined || metalness !== undefined || roughness !== undefined) {
          model.traverse((child) => {
            if ((child as THREE.Mesh).isMesh) {
              const mesh = child as THREE.Mesh;
              if (mesh.material) {
                // Clone material to avoid affecting other instances
                if (Array.isArray(mesh.material)) {
                  mesh.material = mesh.material.map(mat => {
                    const clonedMat = mat.clone();
                    if (clonedMat instanceof THREE.MeshStandardMaterial) {
                      if (color !== undefined) clonedMat.color.setHex(color);
                      if (metalness !== undefined) clonedMat.metalness = metalness;
                      if (roughness !== undefined) clonedMat.roughness = roughness;
                    }
                    return clonedMat;
                  });
                } else {
                  const clonedMat = mesh.material.clone();
                  if (clonedMat instanceof THREE.MeshStandardMaterial) {
                    if (color !== undefined) clonedMat.color.setHex(color);
                    if (metalness !== undefined) clonedMat.metalness = metalness;
                    if (roughness !== undefined) clonedMat.roughness = roughness;
                  }
                  mesh.material = clonedMat;
                }
              }
            }
          });
        }

        // Enable shadows
        model.traverse((child) => {
          if ((child as THREE.Mesh).isMesh) {
            child.castShadow = true;
            child.receiveShadow = true;
          }
        });

        // Apply position and rotation
        if (position) model.position.copy(position);
        if (rotation) model.rotation.copy(rotation);

        // Cache the model
        capoModelCache.set(cacheKey, model.clone());

        resolve(model);
      },
      (progress) => {
        console.log('Loading capo model progress:', (progress.loaded / progress.total) * 100 + '%');
      },
      (error) => {
        console.error('Error loading capo model:', error);
        reject(error);
      }
    );
  });
};

/**
 * Create a fallback geometric capo if 3D model fails to load
 */
export const createFallbackCapo = (
  nutWidth: number,
  position: THREE.Vector3,
  color: number = 0x1a1a1a
): THREE.Group => {
  const capoGroup = new THREE.Group();

  // Capo body (rubber/silicone pad)
  const capoBodyGeometry = new THREE.BoxGeometry(0.8, 0.4, nutWidth * 0.95);
  const capoBodyMaterial = new THREE.MeshStandardMaterial({
    color: color,
    roughness: 0.8,
    metalness: 0.1,
  });
  const capoBody = new THREE.Mesh(capoBodyGeometry, capoBodyMaterial);
  capoBody.position.set(0, 1.0, 0);
  capoBody.castShadow = true;
  capoBody.receiveShadow = true;
  capoGroup.add(capoBody);

  // Capo bar (metal bar that presses strings)
  const capoBarGeometry = new THREE.CylinderGeometry(0.12, 0.12, nutWidth * 0.9, 16);
  const capoBarMaterial = new THREE.MeshStandardMaterial({
    color: 0x888888,
    roughness: 0.3,
    metalness: 0.9,
    emissive: 0x222222,
  });
  const capoBar = new THREE.Mesh(capoBarGeometry, capoBarMaterial);
  capoBar.position.set(0, 0.8, 0);
  capoBar.rotation.z = Math.PI / 2;
  capoBar.castShadow = true;
  capoGroup.add(capoBar);

  // Capo clamp (spring mechanism on back)
  const capoClampGeometry = new THREE.BoxGeometry(0.6, 0.3, nutWidth * 0.4);
  const capoClampMaterial = new THREE.MeshStandardMaterial({
    color: 0x444444,
    roughness: 0.4,
    metalness: 0.8,
  });
  const capoClamp = new THREE.Mesh(capoClampGeometry, capoClampMaterial);
  capoClamp.position.set(0, 1.2, -nutWidth * 0.3);
  capoClamp.castShadow = true;
  capoGroup.add(capoClamp);

  capoGroup.position.copy(position);
  return capoGroup;
};

/**
 * Clear the capo model cache (useful for development/testing)
 */
export const clearCapoModelCache = (): void => {
  capoModelCache.clear();
};

/**
 * Get cache statistics
 */
export const getCapoModelCacheStats = () => {
  return {
    size: capoModelCache.size,
    keys: Array.from(capoModelCache.keys())
  };
};
