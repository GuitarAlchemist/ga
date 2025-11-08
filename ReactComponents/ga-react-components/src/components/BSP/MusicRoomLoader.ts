/**
 * Music Room Loader - Fetches music theory rooms from backend API
 *
 * This module loads room data from the GaApi backend instead of generating
 * rooms locally. The backend uses BSP algorithm with real music theory data.
 */

import * as THREE from 'three';

// ==================
// API Types
// ==================

export interface MusicRoomDto {
  id: string;
  x: number;
  y: number;
  width: number;
  height: number;
  centerX: number;
  centerY: number;
  floor: number;
  category: string;
  items: string[];
  color: string;
  description: string;
}

export interface CorridorDto {
  points: Array<{ x: number; y: number }>;
  width: number;
}

export interface MusicFloorResponse {
  floor: number;
  floorName: string;
  floorSize: number;
  totalItems: number;
  categories: string[];
  rooms: MusicRoomDto[];
  corridors: CorridorDto[];
  seed?: number;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
  message?: string;
}

// ==================
// 3D Room Types
// ==================

export interface Room3D {
  id: string;
  name: string;
  bounds: {
    minX: number;
    maxX: number;
    minZ: number;
    maxZ: number;
  };
  center: THREE.Vector3;
  connections: string[];
  floor: number;
  category: string;
  items: string[];
  color: number;
  description: string;
}

export interface Corridor3D {
  points: THREE.Vector3[];
  width: number;
}

export interface FloorLayout {
  floor: number;
  floorName: string;
  rooms: Room3D[];
  corridors: Corridor3D[];
  totalItems: number;
  categories: string[];
  seed?: number;
}

// ==================
// Music Room Loader
// ==================

export class MusicRoomLoader {
  private baseUrl: string;
  private cache: Map<number, FloorLayout> = new Map();

  constructor(baseUrl: string = 'https://localhost:7001') {
    this.baseUrl = baseUrl;
  }

  /**
   * Load rooms for a specific floor from the backend API
   */
  async loadFloor(floor: number, floorSize: number = 100, seed?: number): Promise<FloorLayout> {
    // Check cache first
    const cacheKey = floor;
    if (this.cache.has(cacheKey)) {
      console.log(`[MusicRoomLoader] Using cached data for floor ${floor}`);
      return this.cache.get(cacheKey)!;
    }

    try {
      console.log(`[MusicRoomLoader] Fetching floor ${floor} from API...`);

      // Build URL with query parameters
      const params = new URLSearchParams({
        floorSize: floorSize.toString(),
      });
      if (seed !== undefined) {
        params.append('seed', seed.toString());
      }

      const url = `${this.baseUrl}/api/music-rooms/floor/${floor}?${params}`;
      console.log(`[MusicRoomLoader] URL: ${url}`);

      const response = await fetch(url);

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const apiResponse: ApiResponse<MusicFloorResponse> = await response.json();

      if (!apiResponse.success || !apiResponse.data) {
        throw new Error(apiResponse.error || 'Failed to load floor data');
      }

      // Convert API response to 3D layout
      const layout = this.convertToFloorLayout(apiResponse.data);

      // Cache the result
      this.cache.set(cacheKey, layout);

      console.log(`[MusicRoomLoader] Loaded ${layout.rooms.length} rooms for floor ${floor}`);
      return layout;

    } catch (error) {
      console.error(`[MusicRoomLoader] Error loading floor ${floor}:`, error);
      throw error;
    }
  }

  /**
   * Convert API response to 3D floor layout
   */
  private convertToFloorLayout(data: MusicFloorResponse): FloorLayout {
    // Convert rooms from 2D to 3D
    const rooms: Room3D[] = data.rooms.map(room => {
      // Convert 2D coordinates to 3D (Y becomes Z in 3D space)
      const minX = room.x;
      const maxX = room.x + room.width;
      const minZ = room.y;
      const maxZ = room.y + room.height;

      // Parse color from CSS string (e.g., "hsl(120, 70%, 50%)")
      const color = this.parseColor(room.color);

      return {
        id: room.id,
        name: room.category,
        bounds: { minX, maxX, minZ, maxZ },
        center: new THREE.Vector3(room.centerX, 0, room.centerY),
        connections: [], // Will be populated by corridor analysis
        floor: room.floor,
        category: room.category,
        items: room.items,
        color,
        description: room.description,
      };
    });

    // Convert corridors from 2D to 3D
    const corridors: Corridor3D[] = data.corridors.map(corridor => ({
      points: corridor.points.map(p => new THREE.Vector3(p.x, 0, p.y)),
      width: corridor.width,
    }));

    // Analyze corridors to populate room connections
    this.populateConnections(rooms, corridors);

    return {
      floor: data.floor,
      floorName: data.floorName,
      rooms,
      corridors,
      totalItems: data.totalItems,
      categories: data.categories,
      seed: data.seed,
    };
  }

  /**
   * Parse CSS color string to THREE.js color number
   */
  private parseColor(colorString: string): number {
    // Handle HSL format: "hsl(120, 70%, 50%)"
    const hslMatch = colorString.match(/hsl\((\d+),\s*(\d+)%,\s*(\d+)%\)/);
    if (hslMatch) {
      const h = parseInt(hslMatch[1]) / 360;
      const s = parseInt(hslMatch[2]) / 100;
      const l = parseInt(hslMatch[3]) / 100;
      const color = new THREE.Color();
      color.setHSL(h, s, l);
      return color.getHex();
    }

    // Handle hex format: "#ff0000"
    if (colorString.startsWith('#')) {
      return parseInt(colorString.substring(1), 16);
    }

    // Default to white
    return 0xffffff;
  }

  /**
   * Populate room connections based on corridor endpoints
   */
  private populateConnections(rooms: Room3D[], corridors: Corridor3D[]): void {
    for (const corridor of corridors) {
      if (corridor.points.length < 2) continue;

      const start = corridor.points[0];
      const end = corridor.points[corridor.points.length - 1];

      // Find rooms near corridor endpoints
      const startRoom = this.findRoomNearPoint(rooms, start);
      const endRoom = this.findRoomNearPoint(rooms, end);

      if (startRoom && endRoom && startRoom !== endRoom) {
        if (!startRoom.connections.includes(endRoom.id)) {
          startRoom.connections.push(endRoom.id);
        }
        if (!endRoom.connections.includes(startRoom.id)) {
          endRoom.connections.push(startRoom.id);
        }
      }
    }
  }

  /**
   * Find room containing or near a point
   */
  private findRoomNearPoint(rooms: Room3D[], point: THREE.Vector3, tolerance: number = 5): Room3D | null {
    for (const room of rooms) {
      const { bounds } = room;
      if (
        point.x >= bounds.minX - tolerance &&
        point.x <= bounds.maxX + tolerance &&
        point.z >= bounds.minZ - tolerance &&
        point.z <= bounds.maxZ + tolerance
      ) {
        return room;
      }
    }
    return null;
  }

  /**
   * Clear cache
   */
  clearCache(): void {
    this.cache.clear();
  }

  /**
   * Preload all floors
   */
  async preloadAllFloors(floorSize: number = 100, seed?: number): Promise<void> {
    console.log('[MusicRoomLoader] Preloading all floors...');
    const promises: Promise<FloorLayout>[] = [];
    for (let floor = 0; floor <= 5; floor++) {
      promises.push(this.loadFloor(floor, floorSize, seed));
    }
    await Promise.all(promises);
    console.log('[MusicRoomLoader] All floors preloaded');
  }
}

// ==================
// Helper Functions
// ==================

/**
 * Get default API base URL based on environment
 */
export function getDefaultApiUrl(): string {
  // In development, use localhost
  if (import.meta.env.DEV) {
    return 'http://localhost:5232';
  }

  // In production, use relative URL
  return window.location.origin;
}

/**
 * Create a singleton instance
 */
let loaderInstance: MusicRoomLoader | null = null;

export function getMusicRoomLoader(): MusicRoomLoader {
  if (!loaderInstance) {
    loaderInstance = new MusicRoomLoader(getDefaultApiUrl());
  }
  return loaderInstance;
}

