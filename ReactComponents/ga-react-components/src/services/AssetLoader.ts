/**
 * AssetLoader Service
 * 
 * Handles loading and caching of 3D assets (GLB files) from the GaApi backend.
 * Provides methods for fetching asset metadata and downloading GLB files.
 */

export enum AssetCategory {
  Architecture = 'Architecture',
  AlchemyProps = 'AlchemyProps',
  Gems = 'Gems',
  Jars = 'Jars',
  Torches = 'Torches',
  Artifacts = 'Artifacts',
  Decorative = 'Decorative'
}

export interface BoundingBox {
  min: { x: number; y: number; z: number };
  max: { x: number; y: number; z: number };
  center: { x: number; y: number; z: number };
  size: { x: number; y: number; z: number };
  volume: number;
}

export interface AssetMetadata {
  id: string;
  name: string;
  category: AssetCategory;
  glbPath: string;
  polyCount: number;
  license: string;
  source: string;
  author?: string;
  tags: Record<string, string>;
  bounds?: BoundingBox;
  fileSizeBytes: number;
  isOptimized: boolean;
  thumbnailPath?: string;
  importedBy?: string;
}

export interface AssetLoaderConfig {
  baseUrl: string;
  enableCaching?: boolean;
  maxCacheSize?: number; // in MB
  timeout?: number; // in ms
}

/**
 * AssetLoader class for managing 3D asset loading and caching
 */
export class AssetLoader {
  private config: Required<AssetLoaderConfig>;
  private metadataCache: Map<string, AssetMetadata> = new Map();
  private glbCache: Map<string, ArrayBuffer> = new Map();
  private cacheSize: number = 0; // in bytes
  private loadingPromises: Map<string, Promise<ArrayBuffer>> = new Map();

  constructor(config: AssetLoaderConfig) {
    this.config = {
      baseUrl: config.baseUrl,
      enableCaching: config.enableCaching ?? true,
      maxCacheSize: (config.maxCacheSize ?? 100) * 1024 * 1024, // Convert MB to bytes
      timeout: config.timeout ?? 30000
    };
  }

  /**
   * Get all assets
   */
  async getAllAssets(): Promise<AssetMetadata[]> {
    const response = await this.fetchWithTimeout(`${this.config.baseUrl}/api/assets`);
    if (!response.ok) {
      throw new Error(`Failed to fetch assets: ${response.statusText}`);
    }
    const assets = await response.json() as AssetMetadata[];
    
    // Cache metadata
    if (this.config.enableCaching) {
      assets.forEach(asset => this.metadataCache.set(asset.id, asset));
    }
    
    return assets;
  }

  /**
   * Get assets by category
   */
  async getAssetsByCategory(category: AssetCategory): Promise<AssetMetadata[]> {
    const response = await this.fetchWithTimeout(
      `${this.config.baseUrl}/api/assets/category/${category}`
    );
    if (!response.ok) {
      throw new Error(`Failed to fetch assets for category ${category}: ${response.statusText}`);
    }
    const assets = await response.json() as AssetMetadata[];
    
    // Cache metadata
    if (this.config.enableCaching) {
      assets.forEach(asset => this.metadataCache.set(asset.id, asset));
    }
    
    return assets;
  }

  /**
   * Get asset by ID
   */
  async getAssetById(id: string): Promise<AssetMetadata | null> {
    // Check cache first
    if (this.config.enableCaching && this.metadataCache.has(id)) {
      return this.metadataCache.get(id)!;
    }

    const response = await this.fetchWithTimeout(`${this.config.baseUrl}/api/assets/${id}`);
    if (response.status === 404) {
      return null;
    }
    if (!response.ok) {
      throw new Error(`Failed to fetch asset ${id}: ${response.statusText}`);
    }
    const asset = await response.json() as AssetMetadata;
    
    // Cache metadata
    if (this.config.enableCaching) {
      this.metadataCache.set(id, asset);
    }
    
    return asset;
  }

  /**
   * Search assets by tags
   */
  async searchAssetsByTags(tags: Record<string, string>): Promise<AssetMetadata[]> {
    const queryParams = new URLSearchParams();
    Object.entries(tags).forEach(([key, value]) => {
      queryParams.append('tags', `${key}:${value}`);
    });
    
    const response = await this.fetchWithTimeout(
      `${this.config.baseUrl}/api/assets/search?${queryParams.toString()}`
    );
    if (!response.ok) {
      throw new Error(`Failed to search assets: ${response.statusText}`);
    }
    const assets = await response.json() as AssetMetadata[];
    
    // Cache metadata
    if (this.config.enableCaching) {
      assets.forEach(asset => this.metadataCache.set(asset.id, asset));
    }
    
    return assets;
  }

  /**
   * Download GLB file for an asset
   */
  async downloadGlb(id: string): Promise<ArrayBuffer> {
    // Check cache first
    if (this.config.enableCaching && this.glbCache.has(id)) {
      return this.glbCache.get(id)!;
    }

    // Check if already loading
    if (this.loadingPromises.has(id)) {
      return this.loadingPromises.get(id)!;
    }

    // Start loading
    const loadingPromise = this.fetchGlbData(id);
    this.loadingPromises.set(id, loadingPromise);

    try {
      const data = await loadingPromise;
      
      // Cache GLB data
      if (this.config.enableCaching) {
        this.addToCache(id, data);
      }
      
      return data;
    } finally {
      this.loadingPromises.delete(id);
    }
  }

  /**
   * Preload multiple assets
   */
  async preloadAssets(ids: string[]): Promise<void> {
    const promises = ids.map(id => this.downloadGlb(id).catch(err => {
      console.warn(`Failed to preload asset ${id}:`, err);
    }));
    await Promise.all(promises);
  }

  /**
   * Get available categories
   */
  async getCategories(): Promise<AssetCategory[]> {
    const response = await this.fetchWithTimeout(`${this.config.baseUrl}/api/assets/categories`);
    if (!response.ok) {
      throw new Error(`Failed to fetch categories: ${response.statusText}`);
    }
    return await response.json() as AssetCategory[];
  }

  /**
   * Stream all assets (NDJSON)
   */
  async *streamAllAssets(): AsyncGenerator<AssetMetadata, void, unknown> {
    const response = await this.fetchWithTimeout(`${this.config.baseUrl}/api/assets/stream`);
    if (!response.ok) {
      throw new Error(`Failed to stream assets: ${response.statusText}`);
    }

    const reader = response.body?.getReader();
    if (!reader) {
      throw new Error('Response body is not readable');
    }

    const decoder = new TextDecoder();
    let buffer = '';

    try {
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.trim()) {
            const asset = JSON.parse(line) as AssetMetadata;
            if (this.config.enableCaching) {
              this.metadataCache.set(asset.id, asset);
            }
            yield asset;
          }
        }
      }
    } finally {
      reader.releaseLock();
    }
  }

  /**
   * Clear all caches
   */
  clearCache(): void {
    this.metadataCache.clear();
    this.glbCache.clear();
    this.cacheSize = 0;
  }

  /**
   * Get cache statistics
   */
  getCacheStats(): { metadataCount: number; glbCount: number; sizeBytes: number; sizeMB: number } {
    return {
      metadataCount: this.metadataCache.size,
      glbCount: this.glbCache.size,
      sizeBytes: this.cacheSize,
      sizeMB: this.cacheSize / (1024 * 1024)
    };
  }

  // Private methods

  private async fetchGlbData(id: string): Promise<ArrayBuffer> {
    const response = await this.fetchWithTimeout(
      `${this.config.baseUrl}/api/assets/${id}/download`
    );
    if (!response.ok) {
      throw new Error(`Failed to download GLB for asset ${id}: ${response.statusText}`);
    }
    return await response.arrayBuffer();
  }

  private addToCache(id: string, data: ArrayBuffer): void {
    const dataSize = data.byteLength;
    
    // Check if we need to evict items
    while (this.cacheSize + dataSize > this.config.maxCacheSize && this.glbCache.size > 0) {
      // Evict oldest item (first in map)
      const iterator = this.glbCache.keys().next();
      if (iterator.done || !iterator.value) {
        break;
      }
      const firstKey = iterator.value as string;
      const firstValue = this.glbCache.get(firstKey);
      if (!firstValue) {
        this.glbCache.delete(firstKey);
        continue;
      }
      this.glbCache.delete(firstKey);
      this.cacheSize -= firstValue.byteLength;
    }

    // Add to cache if it fits
    if (dataSize <= this.config.maxCacheSize) {
      this.glbCache.set(id, data);
      this.cacheSize += dataSize;
    }
  }

  private async fetchWithTimeout(url: string, options?: RequestInit): Promise<Response> {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this.config.timeout);

    try {
      const response = await fetch(url, {
        ...options,
        signal: controller.signal
      });
      return response;
    } catch (error) {
      if (error instanceof Error && error.name === 'AbortError') {
        throw new Error(`Request timeout after ${this.config.timeout}ms`);
      }
      throw error;
    } finally {
      clearTimeout(timeoutId);
    }
  }
}

/**
 * Create a singleton instance of AssetLoader
 */
let assetLoaderInstance: AssetLoader | null = null;

export function createAssetLoader(config: AssetLoaderConfig): AssetLoader {
  assetLoaderInstance = new AssetLoader(config);
  return assetLoaderInstance;
}

export function getAssetLoader(): AssetLoader {
  if (!assetLoaderInstance) {
    throw new Error('AssetLoader not initialized. Call createAssetLoader() first.');
  }
  return assetLoaderInstance;
}
