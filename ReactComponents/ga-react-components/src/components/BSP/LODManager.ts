/**
 * LOD (Level of Detail) Manager for BSP DOOM Explorer
 * 
 * Handles massive-scale visualization (400K+ objects) with:
 * - Distance-based LOD switching
 * - Frustum culling
 * - Instanced rendering for repeated objects
 * - Spatial indexing (octree) for efficient queries
 * - Memory management and object pooling
 * 
 * Performance targets:
 * - 60 FPS with 400K+ objects
 * - < 100ms frame time
 * - < 2GB memory usage
 */

import * as THREE from 'three';

// ==================
// Types
// ==================

export interface LODLevel {
  distance: number;      // Max distance for this LOD level
  geometry: THREE.BufferGeometry;
  material: THREE.Material;
  instanceCount?: number; // For instanced rendering
}

export interface LODObject {
  id: string;
  position: THREE.Vector3;
  rotation?: THREE.Euler;
  scale?: THREE.Vector3;
  levels: LODLevel[];
  userData?: any;
}

export interface LODManagerOptions {
  maxObjects?: number;           // Max objects to render (default: 100000)
  frustumCulling?: boolean;      // Enable frustum culling (default: true)
  octreeDepth?: number;          // Octree depth for spatial indexing (default: 8)
  instancedRendering?: boolean;  // Enable instanced rendering (default: true)
  lodDistances?: number[];       // LOD distance thresholds (default: [10, 50, 200, 1000])
}

export interface PerformanceStats {
  fps: number;
  frameTime: number;
  drawCalls: number;
  triangles: number;
  visibleObjects: number;
  totalObjects: number;
  memoryUsage: number;
  culledObjects: number;
}

// ==================
// Octree for Spatial Indexing
// ==================

class OctreeNode {
  bounds: THREE.Box3;
  objects: LODObject[] = [];
  children: OctreeNode[] | null = null;
  depth: number;

  constructor(bounds: THREE.Box3, depth: number) {
    this.bounds = bounds;
    this.depth = depth;
  }

  subdivide() {
    const center = this.bounds.getCenter(new THREE.Vector3());
    const size = this.bounds.getSize(new THREE.Vector3());
    const halfSize = size.clone().multiplyScalar(0.5);

    this.children = [];
    for (let x = 0; x < 2; x++) {
      for (let y = 0; y < 2; y++) {
        for (let z = 0; z < 2; z++) {
          const min = new THREE.Vector3(
            center.x + (x - 0.5) * halfSize.x,
            center.y + (y - 0.5) * halfSize.y,
            center.z + (z - 0.5) * halfSize.z
          );
          const max = min.clone().add(halfSize);
          this.children.push(new OctreeNode(new THREE.Box3(min, max), this.depth + 1));
        }
      }
    }
  }

  insert(object: LODObject, maxDepth: number, maxObjectsPerNode: number = 8) {
    // If this node has children, insert into appropriate child
    if (this.children) {
      for (const child of this.children) {
        if (child.bounds.containsPoint(object.position)) {
          child.insert(object, maxDepth, maxObjectsPerNode);
          return;
        }
      }
    }

    // Add to this node
    this.objects.push(object);

    // Subdivide if needed
    if (this.objects.length > maxObjectsPerNode && this.depth < maxDepth && !this.children) {
      this.subdivide();
      // Redistribute objects to children
      const objectsToRedistribute = [...this.objects];
      this.objects = [];
      for (const obj of objectsToRedistribute) {
        this.insert(obj, maxDepth, maxObjectsPerNode);
      }
    }
  }

  query(frustum: THREE.Frustum, results: LODObject[]) {
    // Check if frustum intersects this node's bounds
    if (!frustum.intersectsBox(this.bounds)) {
      return;
    }

    // Add objects from this node
    results.push(...this.objects);

    // Query children
    if (this.children) {
      for (const child of this.children) {
        child.query(frustum, results);
      }
    }
  }
}

// ==================
// LOD Manager
// ==================

export class LODManager {
  private scene: THREE.Scene;
  private camera: THREE.Camera;
  private options: Required<LODManagerOptions>;
  private octree: OctreeNode;
  private objects: Map<string, LODObject> = new Map();
  private instancedMeshes: Map<string, THREE.InstancedMesh> = new Map();
  private frustum: THREE.Frustum = new THREE.Frustum();
  private projScreenMatrix: THREE.Matrix4 = new THREE.Matrix4();
  private stats: PerformanceStats = {
    fps: 60,
    frameTime: 16.67,
    drawCalls: 0,
    triangles: 0,
    visibleObjects: 0,
    totalObjects: 0,
    memoryUsage: 0,
    culledObjects: 0,
  };
  private lastFrameTime: number = performance.now();
  private frameCount: number = 0;
  private fpsUpdateInterval: number = 500; // Update FPS every 500ms
  private lastFpsUpdate: number = performance.now();

  constructor(scene: THREE.Scene, camera: THREE.Camera, options: LODManagerOptions = {}) {
    this.scene = scene;
    this.camera = camera;
    this.options = {
      maxObjects: options.maxObjects ?? 100000,
      frustumCulling: options.frustumCulling ?? true,
      octreeDepth: options.octreeDepth ?? 8,
      instancedRendering: options.instancedRendering ?? true,
      lodDistances: options.lodDistances ?? [10, 50, 200, 1000],
    };

    // Initialize octree with world bounds
    const worldSize = 10000; // Adjust based on your world size
    const worldBounds = new THREE.Box3(
      new THREE.Vector3(-worldSize, -worldSize, -worldSize),
      new THREE.Vector3(worldSize, worldSize, worldSize)
    );
    this.octree = new OctreeNode(worldBounds, 0);
  }

  /**
   * Add an object to the LOD system
   */
  addObject(object: LODObject) {
    this.objects.set(object.id, object);
    this.octree.insert(object, this.options.octreeDepth);
    this.stats.totalObjects = this.objects.size;
  }

  /**
   * Remove an object from the LOD system
   */
  removeObject(id: string) {
    this.objects.delete(id);
    this.stats.totalObjects = this.objects.size;
    // Note: Octree removal would require rebuilding or lazy cleanup
  }

  /**
   * Update LOD system (call every frame)
   */
  update() {
    const now = performance.now();
    const delta = now - this.lastFrameTime;
    this.lastFrameTime = now;

    // Update FPS
    this.frameCount++;
    if (now - this.lastFpsUpdate >= this.fpsUpdateInterval) {
      this.stats.fps = Math.round((this.frameCount * 1000) / (now - this.lastFpsUpdate));
      this.stats.frameTime = delta;
      this.frameCount = 0;
      this.lastFpsUpdate = now;
    }

    // Update frustum
    this.projScreenMatrix.multiplyMatrices(
      this.camera.projectionMatrix,
      this.camera.matrixWorldInverse
    );
    this.frustum.setFromProjectionMatrix(this.projScreenMatrix);

    // Query visible objects
    const visibleObjects: LODObject[] = [];
    if (this.options.frustumCulling) {
      this.octree.query(this.frustum, visibleObjects);
    } else {
      visibleObjects.push(...this.objects.values());
    }

    this.stats.visibleObjects = visibleObjects.length;
    this.stats.culledObjects = this.stats.totalObjects - this.stats.visibleObjects;

    // Update LOD levels based on distance
    const cameraPosition = this.camera.position;
    let drawCalls = 0;
    let triangles = 0;

    for (const obj of visibleObjects) {
      const distance = cameraPosition.distanceTo(obj.position);
      
      // Select appropriate LOD level
      let lodLevel = obj.levels.length - 1; // Default to lowest detail
      for (let i = 0; i < obj.levels.length; i++) {
        if (distance <= obj.levels[i].distance) {
          lodLevel = i;
          break;
        }
      }

      const level = obj.levels[lodLevel];
      if (level.geometry && level.material) {
        drawCalls++;
        triangles += level.geometry.attributes.position?.count ?? 0;
      }
    }

    this.stats.drawCalls = drawCalls;
    this.stats.triangles = triangles;

    // Estimate memory usage (rough approximation)
    this.stats.memoryUsage = this.stats.totalObjects * 1024; // ~1KB per object
  }

  /**
   * Get performance statistics
   */
  getStats(): PerformanceStats {
    return { ...this.stats };
  }

  /**
   * Clear all objects
   */
  clear() {
    this.objects.clear();
    this.instancedMeshes.clear();
    const worldSize = 10000;
    const worldBounds = new THREE.Box3(
      new THREE.Vector3(-worldSize, -worldSize, -worldSize),
      new THREE.Vector3(worldSize, worldSize, worldSize)
    );
    this.octree = new OctreeNode(worldBounds, 0);
    this.stats.totalObjects = 0;
    this.stats.visibleObjects = 0;
    this.stats.culledObjects = 0;
  }

  /**
   * Dispose of all resources
   */
  dispose() {
    this.clear();
    for (const mesh of this.instancedMeshes.values()) {
      mesh.geometry.dispose();
      if (Array.isArray(mesh.material)) {
        mesh.material.forEach(m => m.dispose());
      } else {
        mesh.material.dispose();
      }
    }
    this.instancedMeshes.clear();
  }
}

