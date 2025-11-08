/**
 * AssetIntegration
 * 
 * Helper module for integrating 3D assets into the BSP DOOM Explorer.
 * Handles asset loading, placement, and rendering within the BSP tree structure.
 */

import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { getAssetLoader, AssetMetadata, AssetCategory } from '../../services/AssetLoader';

export interface AssetPlacement {
  assetId: string;
  position: THREE.Vector3;
  rotation: THREE.Euler;
  scale: THREE.Vector3;
}

export interface FloorAssetConfig {
  floor: number;
  categories: AssetCategory[];
  density: number; // Assets per unit area (0.0 - 1.0)
  minScale: number;
  maxScale: number;
}

/**
 * AssetManager for BSP DOOM Explorer
 */
export class BSPAssetManager {
  private loader: GLTFLoader;
  private loadedModels: Map<string, THREE.Group> = new Map();
  private instancedMeshes: Map<string, THREE.InstancedMesh> = new Map();
  private scene: THREE.Scene;

  constructor(scene: THREE.Scene) {
    this.loader = new GLTFLoader();
    this.scene = scene;
  }

  /**
   * Load an asset by ID
   */
  async loadAsset(assetId: string): Promise<THREE.Group> {
    // Check if already loaded
    if (this.loadedModels.has(assetId)) {
      return this.loadedModels.get(assetId)!.clone();
    }

    try {
      const assetLoader = getAssetLoader();
      const glbData = await assetLoader.downloadGlb(assetId);
      
      // Convert ArrayBuffer to Blob URL
      const blob = new Blob([glbData], { type: 'model/gltf-binary' });
      const url = URL.createObjectURL(blob);

      // Load GLB
      const gltf = await new Promise<any>((resolve, reject) => {
        this.loader.load(
          url,
          (gltf) => resolve(gltf),
          undefined,
          (error) => reject(error)
        );
      });

      // Clean up blob URL
      URL.revokeObjectURL(url);

      // Cache the model
      this.loadedModels.set(assetId, gltf.scene);

      return gltf.scene.clone();
    } catch (error) {
      console.error(`Failed to load asset ${assetId}:`, error);
      throw error;
    }
  }

  /**
   * Place an asset in the scene
   */
  async placeAsset(placement: AssetPlacement): Promise<THREE.Group> {
    const model = await this.loadAsset(placement.assetId);
    
    model.position.copy(placement.position);
    model.rotation.copy(placement.rotation);
    model.scale.copy(placement.scale);
    
    this.scene.add(model);
    
    return model;
  }

  /**
   * Place multiple assets efficiently using instancing
   */
  async placeAssetsInstanced(
    assetId: string,
    placements: AssetPlacement[]
  ): Promise<THREE.InstancedMesh | null> {
    if (placements.length === 0) return null;

    try {
      const model = await this.loadAsset(assetId);
      
      // Get the first mesh from the model
      let geometry: THREE.BufferGeometry | null = null;
      let material: THREE.Material | null = null;
      
      model.traverse((child) => {
        if (child instanceof THREE.Mesh && !geometry) {
          geometry = child.geometry;
          material = child.material;
        }
      });

      if (!geometry || !material) {
        console.warn(`No mesh found in asset ${assetId}`);
        return null;
      }

      // Create instanced mesh
      const instancedMesh = new THREE.InstancedMesh(
        geometry,
        material,
        placements.length
      );

      // Set transforms for each instance
      const matrix = new THREE.Matrix4();
      placements.forEach((placement, i) => {
        matrix.compose(
          placement.position,
          new THREE.Quaternion().setFromEuler(placement.rotation),
          placement.scale
        );
        instancedMesh.setMatrixAt(i, matrix);
      });

      instancedMesh.instanceMatrix.needsUpdate = true;
      
      this.scene.add(instancedMesh);
      this.instancedMeshes.set(assetId, instancedMesh);
      
      return instancedMesh;
    } catch (error) {
      console.error(`Failed to create instanced mesh for ${assetId}:`, error);
      return null;
    }
  }

  /**
   * Generate random asset placements for a floor
   */
  generateFloorPlacements(
    config: FloorAssetConfig,
    bounds: { min: THREE.Vector3; max: THREE.Vector3 },
    assetIds: string[]
  ): AssetPlacement[] {
    const placements: AssetPlacement[] = [];
    
    // Calculate area
    const width = bounds.max.x - bounds.min.x;
    const depth = bounds.max.z - bounds.min.z;
    const area = width * depth;
    
    // Calculate number of assets
    const numAssets = Math.floor(area * config.density);
    
    for (let i = 0; i < numAssets; i++) {
      // Random asset
      const assetId = assetIds[Math.floor(Math.random() * assetIds.length)];
      
      // Random position
      const x = bounds.min.x + Math.random() * width;
      const y = bounds.min.y;
      const z = bounds.min.z + Math.random() * depth;
      
      // Random rotation (only Y axis for most objects)
      const rotationY = Math.random() * Math.PI * 2;
      
      // Random scale
      const scale = config.minScale + Math.random() * (config.maxScale - config.minScale);
      
      placements.push({
        assetId,
        position: new THREE.Vector3(x, y, z),
        rotation: new THREE.Euler(0, rotationY, 0),
        scale: new THREE.Vector3(scale, scale, scale)
      });
    }
    
    return placements;
  }

  /**
   * Preload assets for a floor
   */
  async preloadFloorAssets(config: FloorAssetConfig): Promise<void> {
    try {
      const assetLoader = getAssetLoader();
      
      // Get assets for each category
      const assetPromises = config.categories.map(category =>
        assetLoader.getAssetsByCategory(category)
      );
      
      const assetArrays = await Promise.all(assetPromises);
      const assets = assetArrays.flat();
      
      // Preload GLB files
      const loadPromises = assets.map(asset =>
        this.loadAsset(asset.id).catch(err => {
          console.warn(`Failed to preload asset ${asset.id}:`, err);
        })
      );
      
      await Promise.all(loadPromises);
      
      console.log(`Preloaded ${assets.length} assets for floor ${config.floor}`);
    } catch (error) {
      console.error(`Failed to preload floor ${config.floor} assets:`, error);
    }
  }

  /**
   * Clear all assets from the scene
   */
  clearAssets(): void {
    // Remove instanced meshes
    this.instancedMeshes.forEach(mesh => {
      this.scene.remove(mesh);
      mesh.geometry.dispose();
      if (Array.isArray(mesh.material)) {
        mesh.material.forEach(m => m.dispose());
      } else {
        mesh.material.dispose();
      }
    });
    this.instancedMeshes.clear();
    
    // Clear loaded models cache
    this.loadedModels.forEach(model => {
      model.traverse(child => {
        if (child instanceof THREE.Mesh) {
          child.geometry.dispose();
          if (Array.isArray(child.material)) {
            child.material.forEach(m => m.dispose());
          } else {
            child.material.dispose();
          }
        }
      });
    });
    this.loadedModels.clear();
  }

  /**
   * Get asset metadata for a category
   */
  async getAssetsForCategory(category: AssetCategory): Promise<AssetMetadata[]> {
    const assetLoader = getAssetLoader();
    return await assetLoader.getAssetsByCategory(category);
  }
}

/**
 * Default floor asset configurations for BSP DOOM Explorer
 */
export const DEFAULT_FLOOR_CONFIGS: FloorAssetConfig[] = [
  // Floor 0: Pitch Class Sets - Decorative gems and artifacts
  {
    floor: 0,
    categories: [AssetCategory.Gems, AssetCategory.Artifacts],
    density: 0.05,
    minScale: 0.5,
    maxScale: 1.5
  },
  // Floor 1: Forte Codes - Jars and torches
  {
    floor: 1,
    categories: [AssetCategory.Jars, AssetCategory.Torches],
    density: 0.03,
    minScale: 0.8,
    maxScale: 1.2
  },
  // Floor 2: Prime Forms - Alchemy props
  {
    floor: 2,
    categories: [AssetCategory.AlchemyProps],
    density: 0.04,
    minScale: 0.6,
    maxScale: 1.4
  },
  // Floor 3: Chords - Architecture elements
  {
    floor: 3,
    categories: [AssetCategory.Architecture],
    density: 0.02,
    minScale: 1.0,
    maxScale: 2.0
  }
];

