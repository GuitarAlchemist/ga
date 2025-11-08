/**
 * Example: Room Generation Integration for BSP DOOM Explorer
 * 
 * This file demonstrates how to integrate the room generation system
 * into the BSP DOOM Explorer component.
 */

import { useEffect, useRef } from 'react';
import * as THREE from 'three';
import { RoomGenerator, RoomGenerationConfig, Room } from './RoomGenerator';
import { RoomRenderer } from './RoomRenderer';

// ==================
// Music Categories by Floor
// ==================

const MUSIC_CATEGORIES = {
  0: [ // Set Classes (93 total)
    'Chromatic', 'Diatonic', 'Pentatonic', 'Hexatonic',
    'Octatonic', 'Whole Tone', 'Augmented', 'Diminished',
    'Quartal', 'Quintal', 'Cluster', 'Symmetric'
  ],
  1: [ // Forte Codes (115 total)
    '3-11 (Major/Minor)', '4-23 (Quartal)', '5-35 (Pentatonic)',
    '6-32 (Hexatonic)', '7-35 (Diatonic)', '8-28 (Octatonic)',
    '3-12 (Augmented)', '4-28 (Diminished)', '5-33 (Whole Tone)',
    '6-20 (Hexatonic)', '7-34 (Melodic Minor)', '8-23 (Octatonic)'
  ],
  2: [ // Prime Forms (200 total)
    '[0,4,7] Major', '[0,3,7] Minor', '[0,3,6] Diminished',
    '[0,4,8] Augmented', '[0,2,4,7,9,11] Whole Tone',
    '[0,2,3,5,7,8,10] Diatonic', '[0,1,3,4,6,7,9,10] Octatonic',
    '[0,2,4,5,7,9,11] Major Scale', '[0,2,3,5,7,8,10] Natural Minor'
  ],
  3: [ // Chords (350 total)
    'Major', 'Minor', 'Dominant 7th', 'Major 7th', 'Minor 7th',
    'Diminished 7th', 'Half-Diminished', 'Augmented', 'Sus2', 'Sus4',
    'Add9', 'Add11', 'Add13', '6th', 'Minor 6th', 'Major 9th',
    'Minor 9th', 'Dominant 9th', 'Major 11th', 'Minor 11th'
  ],
  4: [ // Chord Inversions (4096 total - sampled)
    'Root Position', '1st Inversion', '2nd Inversion', '3rd Inversion',
    'Drop 2', 'Drop 3', 'Drop 2+4', 'Spread Voicing',
    'Close Voicing', 'Open Voicing', 'Rootless', 'Shell'
  ],
  5: [ // Chord Voicings (100k+ total - sampled)
    'Jazz Voicings', 'Classical Voicings', 'Rock Voicings',
    'CAGED System', 'Position-Based', 'String Sets',
    'Drop Voicings', 'Quartal Voicings', 'Cluster Voicings',
    'Spread Voicings', 'Close Voicings', 'Open Voicings'
  ]
};

// ==================
// Room Generation Configs
// ==================

const ROOM_CONFIGS: Record<number, Omit<RoomGenerationConfig, 'musicCategories'>> = {
  0: { floorSize: 200, minRoomSize: 12, maxRoomSize: 24, roomCount: 93, floor: 0 },
  1: { floorSize: 180, minRoomSize: 10, maxRoomSize: 20, roomCount: 115, floor: 1 },
  2: { floorSize: 160, minRoomSize: 8, maxRoomSize: 18, roomCount: 200, floor: 2 },
  3: { floorSize: 140, minRoomSize: 8, maxRoomSize: 16, roomCount: 350, floor: 3 },
  4: { floorSize: 120, minRoomSize: 6, maxRoomSize: 12, roomCount: 100, floor: 4 }, // Sample
  5: { floorSize: 100, minRoomSize: 4, maxRoomSize: 10, roomCount: 200, floor: 5 }, // Sample
};

// ==================
// Room Generation Functions
// ==================

/**
 * Generate rooms for a specific floor
 */
export function generateFloorRooms(floor: number): Room[] {
  const config = ROOM_CONFIGS[floor];
  const categories = MUSIC_CATEGORIES[floor as keyof typeof MUSIC_CATEGORIES];

  if (!config || !categories) {
    console.error(`Invalid floor: ${floor}`);
    return [];
  }

  const generator = new RoomGenerator({
    ...config,
    musicCategories: categories,
  });

  const rooms = generator.generate();
  
  console.log(`✅ Generated ${rooms.length} rooms for Floor ${floor}`);
  return rooms;
}

/**
 * Render rooms to a Three.js scene
 */
export function renderFloorRooms(
  rooms: Room[],
  floorGroup: THREE.Group,
  wallHeight = 8,
  wallThickness = 0.5
): void {
  const renderer = new RoomRenderer(floorGroup, wallHeight, wallThickness);

  rooms.forEach(room => {
    renderer.renderRoom(room);
  });

  console.log(`✅ Rendered ${rooms.length} rooms`);
}

/**
 * Generate and render all floors
 */
export function generateAllFloors(parent: THREE.Group): Map<number, Room[]> {
  const floorRooms = new Map<number, Room[]>();

  for (let floor = 0; floor < 6; floor++) {
    // Create floor group
    const floorGroup = new THREE.Group();
    floorGroup.position.y = floor * 20; // 20 units between floors
    floorGroup.visible = floor === 0; // Only show first floor initially
    parent.add(floorGroup);

    // Generate rooms
    const rooms = generateFloorRooms(floor);
    floorRooms.set(floor, rooms);

    // Render rooms
    renderFloorRooms(rooms, floorGroup);

    console.log(`✅ Floor ${floor}: ${rooms.length} rooms created`);
  }

  return floorRooms;
}

// ==================
// React Hook for Room Generation
// ==================

/**
 * Hook to generate and manage rooms
 */
export function useRoomGeneration(sceneRef: React.RefObject<THREE.Group>) {
  const roomsRef = useRef<Map<number, Room[]>>(new Map());

  useEffect(() => {
    if (!sceneRef.current) return;

    // Generate all floors
    const rooms = generateAllFloors(sceneRef.current);
    roomsRef.current = rooms;

    // Cleanup
    return () => {
      roomsRef.current.clear();
    };
  }, [sceneRef]);

  return {
    rooms: roomsRef.current,
    getRoomsForFloor: (floor: number) => roomsRef.current.get(floor) || [],
    getTotalRoomCount: () => {
      let total = 0;
      roomsRef.current.forEach(rooms => total += rooms.length);
      return total;
    },
  };
}

// ==================
// Example Usage in BSPDoomExplorer
// ==================

/**
 * Example integration into BSPDoomExplorer.tsx
 * 
 * Add this to the buildFloors function:
 * 
 * ```typescript
 * import { generateFloorRooms, renderFloorRooms } from './RoomGenerationExample';
 * 
 * const buildFloors = (parent: THREE.Group) => {
 *   for (let i = 0; i < 6; i++) {
 *     const floorGroup = new THREE.Group();
 *     floorGroup.position.y = i * 20;
 *     floorGroup.visible = i === 0;
 *     parent.add(floorGroup);
 *     floorGroupsRef.current.push(floorGroup);
 * 
 *     // ROOM GENERATION: Generate and render rooms
 *     const rooms = generateFloorRooms(i);
 *     renderFloorRooms(rooms, floorGroup);
 *     
 *     // Store rooms for later use
 *     floorRoomsRef.current.set(i, rooms);
 *   }
 * };
 * ```
 */

// ==================
// Room Navigation Helper
// ==================

/**
 * Find a room by ID
 */
export function findRoom(rooms: Map<number, Room[]>, roomId: string): Room | undefined {
  for (const floorRooms of rooms.values()) {
    const room = floorRooms.find(r => r.id === roomId);
    if (room) return room;
  }
  return undefined;
}

/**
 * Find path between two rooms using A* algorithm
 */
export function findPath(
  rooms: Map<number, Room[]>,
  startRoomId: string,
  endRoomId: string
): Room[] {
  const startRoom = findRoom(rooms, startRoomId);
  const endRoom = findRoom(rooms, endRoomId);

  if (!startRoom || !endRoom) return [];

  // Simple BFS for now (can be upgraded to A*)
  const queue: Room[][] = [[startRoom]];
  const visited = new Set<string>([startRoom.id]);

  while (queue.length > 0) {
    const path = queue.shift()!;
    const current = path[path.length - 1];

    if (current.id === endRoom.id) {
      return path;
    }

    for (const connectionId of current.connections) {
      if (!visited.has(connectionId)) {
        visited.add(connectionId);
        const nextRoom = findRoom(rooms, connectionId);
        if (nextRoom) {
          queue.push([...path, nextRoom]);
        }
      }
    }
  }

  return []; // No path found
}

/**
 * Get all rooms within a radius
 */
export function getRoomsInRadius(
  rooms: Room[],
  center: THREE.Vector3,
  radius: number
): Room[] {
  return rooms.filter(room => {
    const distance = room.center.distanceTo(center);
    return distance <= radius;
  });
}

/**
 * Get room at position
 */
export function getRoomAtPosition(
  rooms: Room[],
  position: THREE.Vector3
): Room | undefined {
  return rooms.find(room => {
    const { bounds } = room;
    return (
      position.x >= bounds.minX &&
      position.x <= bounds.maxX &&
      position.z >= bounds.minZ &&
      position.z <= bounds.maxZ
    );
  });
}

// ==================
// Room Interaction Helpers
// ==================

/**
 * Handle door click
 */
export function handleDoorClick(
  doorMesh: THREE.Mesh,
  rooms: Map<number, Room[]>,
  onRoomChange: (room: Room) => void
): void {
  const targetRoomId = doorMesh.userData.targetRoom;
  const targetRoom = findRoom(rooms, targetRoomId);

  if (targetRoom) {
    console.log(`🚪 Entering room: ${targetRoom.name}`);
    onRoomChange(targetRoom);
  }
}

/**
 * Handle music item click
 */
export function handleMusicItemClick(
  itemMesh: THREE.Mesh,
  onItemSelect: (itemName: string, roomId: string) => void
): void {
  const itemName = itemMesh.userData.itemName;
  const roomId = itemMesh.userData.room;

  console.log(`🎵 Selected: ${itemName} in room ${roomId}`);
  onItemSelect(itemName, roomId);
}

/**
 * Update room visibility based on camera position
 */
export function updateRoomVisibility(
  rooms: Room[],
  cameraPosition: THREE.Vector3,
  renderDistance: number
): void {
  rooms.forEach(room => {
    const distance = room.center.distanceTo(cameraPosition);
    // Room visibility would be controlled through the meshes
    // This is a placeholder for the logic
    const shouldBeVisible = distance < renderDistance;
    console.debug(`Room ${room.id} visibility: ${shouldBeVisible}`);
  });
}

// ==================
// Export All
// ==================

export {
  MUSIC_CATEGORIES,
  ROOM_CONFIGS,
  type Room,
  type RoomGenerationConfig,
};

