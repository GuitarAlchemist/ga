/**
 * Procedural Room Generation for BSP DOOM Explorer
 * 
 * This module generates navigable 3D rooms for exploring music theory hierarchies:
 * - Floor 0: Set Classes (93 rooms)
 * - Floor 1: Forte Codes (115 rooms)
 * - Floor 2: Prime Forms (200 rooms)
 * - Floor 3: Chords (350 rooms)
 * - Floor 4: Chord Inversions (4,096 rooms)
 * - Floor 5: Chord Voicings (100,000+ rooms)
 * 
 * Uses BSP (Binary Space Partitioning) algorithm for room layout
 */

import * as THREE from 'three';

// ==================
// Types
// ==================

export interface Room {
  id: string;
  name: string;
  bounds: RoomBounds;
  center: THREE.Vector3;
  connections: string[]; // IDs of connected rooms
  type: RoomType;
  musicData?: MusicRoomData;
}

export interface RoomBounds {
  minX: number;
  maxX: number;
  minZ: number;
  maxZ: number;
}

export type RoomType = 
  | 'hub'           // Central room with multiple connections
  | 'corridor'      // Connecting passage
  | 'chamber'       // Standard room
  | 'gallery'       // Large display room
  | 'sanctuary';    // Special room for important items

export interface MusicRoomData {
  floor: number;
  category: string;
  items: string[];
  color: number;
  description?: string;
}

export interface BSPNode {
  bounds: RoomBounds;
  room?: Room;
  left?: BSPNode;
  right?: BSPNode;
  splitAxis?: 'x' | 'z';
  splitPosition?: number;
}

export interface RoomGenerationConfig {
  floorSize: number;
  minRoomSize: number;
  maxRoomSize: number;
  roomCount: number;
  floor: number;
  musicCategories: string[];
}

// ==================
// BSP Room Generation
// ==================

export class RoomGenerator {
  private config: RoomGenerationConfig;
  private rooms: Room[] = [];
  private bspTree?: BSPNode;

  constructor(config: RoomGenerationConfig) {
    this.config = config;
  }

  /**
   * Generate rooms using BSP algorithm
   */
  generate(): Room[] {
    this.rooms = [];
    
    // Create root BSP node covering entire floor
    const halfSize = this.config.floorSize / 2;
    this.bspTree = {
      bounds: {
        minX: -halfSize,
        maxX: halfSize,
        minZ: -halfSize,
        maxZ: halfSize,
      }
    };

    // Recursively split space until we have enough rooms
    this.splitNode(this.bspTree, 0);

    // Create rooms from leaf nodes
    this.createRoomsFromLeaves(this.bspTree);

    // Connect rooms with corridors
    this.connectRooms();

    // Assign music data to rooms
    this.assignMusicData();

    return this.rooms;
  }

  /**
   * Recursively split BSP node
   */
  private splitNode(node: BSPNode, depth: number): void {
    const { bounds } = node;
    const width = bounds.maxX - bounds.minX;
    const height = bounds.maxZ - bounds.minZ;

    // Stop splitting if:
    // 1. We have enough rooms
    // 2. Room is too small
    // 3. We've reached max depth
    const maxDepth = Math.ceil(Math.log2(this.config.roomCount));
    if (
      this.countLeaves(this.bspTree!) >= this.config.roomCount ||
      width < this.config.minRoomSize * 2 ||
      height < this.config.minRoomSize * 2 ||
      depth >= maxDepth
    ) {
      return;
    }

    // Decide split axis (prefer splitting along longer dimension)
    const splitAxis: 'x' | 'z' = width > height ? 'x' : 'z';
    node.splitAxis = splitAxis;

    // Calculate split position (with some randomness)
    const minSplit = splitAxis === 'x' ? bounds.minX + this.config.minRoomSize : bounds.minZ + this.config.minRoomSize;
    const maxSplit = splitAxis === 'x' ? bounds.maxX - this.config.minRoomSize : bounds.maxZ - this.config.minRoomSize;
    const splitRange = maxSplit - minSplit;
    const splitPosition = minSplit + splitRange * (0.3 + Math.random() * 0.4); // 30-70% split
    node.splitPosition = splitPosition;

    // Create child nodes
    if (splitAxis === 'x') {
      node.left = {
        bounds: {
          minX: bounds.minX,
          maxX: splitPosition,
          minZ: bounds.minZ,
          maxZ: bounds.maxZ,
        }
      };
      node.right = {
        bounds: {
          minX: splitPosition,
          maxX: bounds.maxX,
          minZ: bounds.minZ,
          maxZ: bounds.maxZ,
        }
      };
    } else {
      node.left = {
        bounds: {
          minX: bounds.minX,
          maxX: bounds.maxX,
          minZ: bounds.minZ,
          maxZ: splitPosition,
        }
      };
      node.right = {
        bounds: {
          minX: bounds.minX,
          maxX: bounds.maxX,
          minZ: splitPosition,
          maxZ: bounds.maxZ,
        }
      };
    }

    // Recursively split children
    this.splitNode(node.left, depth + 1);
    this.splitNode(node.right, depth + 1);
  }

  /**
   * Count leaf nodes in BSP tree
   */
  private countLeaves(node: BSPNode): number {
    if (!node.left && !node.right) return 1;
    return (node.left ? this.countLeaves(node.left) : 0) +
           (node.right ? this.countLeaves(node.right) : 0);
  }

  /**
   * Create rooms from BSP leaf nodes
   */
  private createRoomsFromLeaves(node: BSPNode): void {
    if (!node.left && !node.right) {
      // Leaf node - create a room
      const { bounds } = node;
      const width = bounds.maxX - bounds.minX;
      const height = bounds.maxZ - bounds.minZ;

      // Shrink room slightly to create walls
      const padding = 2;
      const roomBounds: RoomBounds = {
        minX: bounds.minX + padding,
        maxX: bounds.maxX - padding,
        minZ: bounds.minZ + padding,
        maxZ: bounds.maxZ - padding,
      };

      const room: Room = {
        id: `room_${this.rooms.length}`,
        name: `Room ${this.rooms.length + 1}`,
        bounds: roomBounds,
        center: new THREE.Vector3(
          (roomBounds.minX + roomBounds.maxX) / 2,
          0,
          (roomBounds.minZ + roomBounds.maxZ) / 2
        ),
        connections: [],
        type: this.determineRoomType(width, height),
      };

      node.room = room;
      this.rooms.push(room);
    } else {
      // Internal node - recurse
      if (node.left) this.createRoomsFromLeaves(node.left);
      if (node.right) this.createRoomsFromLeaves(node.right);
    }
  }

  /**
   * Determine room type based on size
   */
  private determineRoomType(width: number, height: number): RoomType {
    const area = width * height;
    const avgSize = (this.config.minRoomSize + this.config.maxRoomSize) / 2;
    const avgArea = avgSize * avgSize;

    if (area > avgArea * 2) return 'gallery';
    if (area < avgArea * 0.5) return 'corridor';
    if (Math.random() < 0.1) return 'sanctuary';
    if (Math.random() < 0.2) return 'hub';
    return 'chamber';
  }

  /**
   * Connect adjacent rooms with corridors
   */
  private connectRooms(): void {
    // For each room, find adjacent rooms and create connections
    for (let i = 0; i < this.rooms.length; i++) {
      const room1 = this.rooms[i];
      
      for (let j = i + 1; j < this.rooms.length; j++) {
        const room2 = this.rooms[j];
        
        if (this.areAdjacent(room1, room2)) {
          room1.connections.push(room2.id);
          room2.connections.push(room1.id);
        }
      }
    }
  }

  /**
   * Check if two rooms are adjacent (share a wall)
   */
  private areAdjacent(room1: Room, room2: Room): boolean {
    const b1 = room1.bounds;
    const b2 = room2.bounds;

    // Check if rooms share an edge (with small tolerance)
    const tolerance = 0.1;

    // Vertical adjacency (share X edge)
    const shareXEdge = 
      Math.abs(b1.maxX - b2.minX) < tolerance || 
      Math.abs(b1.minX - b2.maxX) < tolerance;
    
    // Horizontal adjacency (share Z edge)
    const shareZEdge = 
      Math.abs(b1.maxZ - b2.minZ) < tolerance || 
      Math.abs(b1.minZ - b2.maxZ) < tolerance;

    // Check if they overlap on the other axis
    const overlapX = !(b1.maxX < b2.minX || b1.minX > b2.maxX);
    const overlapZ = !(b1.maxZ < b2.minZ || b1.minZ > b2.maxZ);

    return (shareXEdge && overlapZ) || (shareZEdge && overlapX);
  }

  /**
   * Assign music theory data to rooms
   */
  private assignMusicData(): void {
    const { musicCategories, floor } = this.config;
    
    this.rooms.forEach((room, index) => {
      const categoryIndex = index % musicCategories.length;
      const category = musicCategories[categoryIndex];
      
      room.musicData = {
        floor,
        category,
        items: this.generateMusicItems(floor, category, index),
        color: this.getCategoryColor(categoryIndex, musicCategories.length),
        description: this.getMusicDescription(floor, category),
      };
      
      room.name = `${category} ${Math.floor(index / musicCategories.length) + 1}`;
    });
  }

  /**
   * Generate music items for a room based on floor and category
   */
  private generateMusicItems(floor: number, category: string, roomIndex: number): string[] {
    // This would be populated from actual music theory data
    // For now, generate placeholder items
    const itemCount = Math.min(10, Math.floor(Math.random() * 20) + 5);
    return Array.from({ length: itemCount }, (_, i) => 
      `${category} Item ${roomIndex * 10 + i + 1}`
    );
  }

  /**
   * Get color for music category
   */
  private getCategoryColor(index: number, total: number): number {
    const hue = (index / total) * 360;
    return new THREE.Color().setHSL(hue / 360, 0.7, 0.5).getHex();
  }

  /**
   * Get description for music category
   */
  private getMusicDescription(floor: number, category: string): string {
    const descriptions: Record<number, string> = {
      0: `Set Class: ${category}`,
      1: `Forte Code: ${category}`,
      2: `Prime Form: ${category}`,
      3: `Chord: ${category}`,
      4: `Chord Inversion: ${category}`,
      5: `Chord Voicing: ${category}`,
    };
    return descriptions[floor] || category;
  }
}

