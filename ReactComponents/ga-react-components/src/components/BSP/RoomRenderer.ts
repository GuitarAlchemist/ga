/**
 * Room Renderer for BSP DOOM Explorer
 * 
 * Creates 3D geometry for procedurally generated rooms
 */

import * as THREE from 'three';
import { Room, RoomType } from './RoomGenerator';

// ==================
// Room Rendering
// ==================

export interface RoomMeshes {
  floor: THREE.Mesh;
  walls: THREE.Mesh[];
  ceiling?: THREE.Mesh;
  doors: THREE.Mesh[];
  decorations: THREE.Object3D[];
}

export class RoomRenderer {
  private scene: THREE.Group;
  private wallHeight: number;
  private wallThickness: number;

  constructor(scene: THREE.Group, wallHeight = 8, wallThickness = 0.5) {
    this.scene = scene;
    this.wallHeight = wallHeight;
    this.wallThickness = wallThickness;
  }

  /**
   * Render a room as 3D geometry
   */
  renderRoom(room: Room): RoomMeshes {
    const meshes: RoomMeshes = {
      floor: this.createFloor(room),
      walls: this.createWalls(room),
      doors: this.createDoors(room),
      decorations: this.createDecorations(room),
    };

    // Add ceiling for enclosed rooms
    if (room.type !== 'corridor') {
      meshes.ceiling = this.createCeiling(room);
    }

    // Add all meshes to scene
    this.scene.add(meshes.floor);
    meshes.walls.forEach(wall => this.scene.add(wall));
    meshes.doors.forEach(door => this.scene.add(door));
    meshes.decorations.forEach(deco => this.scene.add(deco));
    if (meshes.ceiling) this.scene.add(meshes.ceiling);

    return meshes;
  }

  /**
   * Create floor mesh
   */
  private createFloor(room: Room): THREE.Mesh {
    const { bounds } = room;
    const width = bounds.maxX - bounds.minX;
    const depth = bounds.maxZ - bounds.minZ;

    const geometry = new THREE.PlaneGeometry(width, depth, 10, 10);
    const material = this.getFloorMaterial(room);

    const mesh = new THREE.Mesh(geometry, material);
    mesh.rotation.x = -Math.PI / 2;
    mesh.position.set(
      (bounds.minX + bounds.maxX) / 2,
      0,
      (bounds.minZ + bounds.maxZ) / 2
    );
    mesh.receiveShadow = true;
    mesh.userData = { type: 'floor', room: room.id };

    return mesh;
  }

  /**
   * Create wall meshes
   */
  private createWalls(room: Room): THREE.Mesh[] {
    const { bounds } = room;
    const walls: THREE.Mesh[] = [];
    const material = this.getWallMaterial(room);

    // North wall (maxZ)
    walls.push(this.createWall(
      bounds.minX, bounds.maxX,
      bounds.maxZ,
      'z',
      material,
      room
    ));

    // South wall (minZ)
    walls.push(this.createWall(
      bounds.minX, bounds.maxX,
      bounds.minZ,
      'z',
      material,
      room
    ));

    // East wall (maxX)
    walls.push(this.createWall(
      bounds.minZ, bounds.maxZ,
      bounds.maxX,
      'x',
      material,
      room
    ));

    // West wall (minX)
    walls.push(this.createWall(
      bounds.minZ, bounds.maxZ,
      bounds.minX,
      'x',
      material,
      room
    ));

    return walls;
  }

  /**
   * Create a single wall
   */
  private createWall(
    start: number,
    end: number,
    position: number,
    axis: 'x' | 'z',
    material: THREE.Material,
    room: Room
  ): THREE.Mesh {
    const length = end - start;
    const geometry = new THREE.BoxGeometry(
      axis === 'x' ? this.wallThickness : length,
      this.wallHeight,
      axis === 'z' ? this.wallThickness : length
    );

    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.set(
      axis === 'x' ? position : (start + end) / 2,
      this.wallHeight / 2,
      axis === 'z' ? position : (start + end) / 2
    );
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    mesh.userData = { type: 'wall', room: room.id };

    return mesh;
  }

  /**
   * Create ceiling mesh
   */
  private createCeiling(room: Room): THREE.Mesh {
    const { bounds } = room;
    const width = bounds.maxX - bounds.minX;
    const depth = bounds.maxZ - bounds.minZ;

    const geometry = new THREE.PlaneGeometry(width, depth);
    const material = new THREE.MeshStandardMaterial({
      color: 0x1a1a1a,
      roughness: 0.9,
      metalness: 0.1,
      side: THREE.DoubleSide,
    });

    const mesh = new THREE.Mesh(geometry, material);
    mesh.rotation.x = Math.PI / 2;
    mesh.position.set(
      (bounds.minX + bounds.maxX) / 2,
      this.wallHeight,
      (bounds.minZ + bounds.maxZ) / 2
    );
    mesh.receiveShadow = true;
    mesh.userData = { type: 'ceiling', room: room.id };

    return mesh;
  }

  /**
   * Create door meshes for room connections
   */
  private createDoors(room: Room): THREE.Mesh[] {
    const doors: THREE.Mesh[] = [];
    const { bounds } = room;

    // For each connection, create a door
    room.connections.forEach((connectionId, index) => {
      // Determine door position based on connection
      // This is simplified - in practice, you'd calculate based on adjacent room position
      const doorWidth = 3;
      const doorHeight = 6;

      const geometry = new THREE.BoxGeometry(doorWidth, doorHeight, 0.2);
      const material = new THREE.MeshPhysicalMaterial({
        color: room.musicData?.color || 0x00ff00,
        emissive: room.musicData?.color || 0x00ff00,
        emissiveIntensity: 0.3,
        metalness: 0.7,
        roughness: 0.3,
        transparent: true,
        opacity: 0.8,
      });

      const mesh = new THREE.Mesh(geometry, material);
      
      // Position door on one of the walls
      const wallIndex = index % 4;
      switch (wallIndex) {
        case 0: // North
          mesh.position.set((bounds.minX + bounds.maxX) / 2, doorHeight / 2, bounds.maxZ);
          break;
        case 1: // South
          mesh.position.set((bounds.minX + bounds.maxX) / 2, doorHeight / 2, bounds.minZ);
          break;
        case 2: // East
          mesh.position.set(bounds.maxX, doorHeight / 2, (bounds.minZ + bounds.maxZ) / 2);
          mesh.rotation.y = Math.PI / 2;
          break;
        case 3: // West
          mesh.position.set(bounds.minX, doorHeight / 2, (bounds.minZ + bounds.maxZ) / 2);
          mesh.rotation.y = Math.PI / 2;
          break;
      }

      mesh.userData = { 
        type: 'door', 
        room: room.id, 
        targetRoom: connectionId,
        interactive: true,
      };

      doors.push(mesh);
    });

    return doors;
  }

  /**
   * Create decorative elements for the room
   */
  private createDecorations(room: Room): THREE.Object3D[] {
    const decorations: THREE.Object3D[] = [];

    // Add room label
    const label = this.createRoomLabel(room);
    if (label) decorations.push(label);

    // Add music items as 3D objects
    if (room.musicData) {
      const items = this.createMusicItems(room);
      decorations.push(...items);
    }

    // Add lighting
    const lights = this.createRoomLighting(room);
    decorations.push(...lights);

    return decorations;
  }

  /**
   * Create room label
   */
  private createRoomLabel(room: Room): THREE.Sprite | null {
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    if (!context) return null;

    canvas.width = 512;
    canvas.height = 128;

    // Draw text
    context.fillStyle = 'rgba(0, 0, 0, 0.8)';
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.font = 'bold 48px Arial';
    context.fillStyle = room.musicData?.color 
      ? `#${room.musicData.color.toString(16).padStart(6, '0')}`
      : '#00ffff';
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.fillText(room.name, canvas.width / 2, canvas.height / 2);

    const texture = new THREE.CanvasTexture(canvas);
    const material = new THREE.SpriteMaterial({ map: texture });
    const sprite = new THREE.Sprite(material);
    
    sprite.position.copy(room.center);
    sprite.position.y = this.wallHeight - 1;
    sprite.scale.set(8, 2, 1);
    sprite.userData = { type: 'label', room: room.id };

    return sprite;
  }

  /**
   * Create 3D representations of music items
   */
  private createMusicItems(room: Room): THREE.Object3D[] {
    if (!room.musicData) return [];

    const items: THREE.Object3D[] = [];
    const { bounds } = room;
    const itemCount = Math.min(room.musicData.items.length, 20);

    for (let i = 0; i < itemCount; i++) {
      // Create a simple geometric shape for each item
      const geometry = new THREE.SphereGeometry(0.5, 16, 16);
      const material = new THREE.MeshPhysicalMaterial({
        color: room.musicData.color,
        emissive: room.musicData.color,
        emissiveIntensity: 0.2,
        metalness: 0.5,
        roughness: 0.5,
      });

      const mesh = new THREE.Mesh(geometry, material);
      
      // Position randomly within room
      mesh.position.set(
        bounds.minX + Math.random() * (bounds.maxX - bounds.minX),
        1 + Math.random() * 2,
        bounds.minZ + Math.random() * (bounds.maxZ - bounds.minZ)
      );

      mesh.userData = {
        type: 'music-item',
        room: room.id,
        itemName: room.musicData.items[i],
        interactive: true,
      };

      items.push(mesh);
    }

    return items;
  }

  /**
   * Create lighting for the room
   */
  private createRoomLighting(room: Room): THREE.Light[] {
    const lights: THREE.Light[] = [];

    // Add point light in center
    const pointLight = new THREE.PointLight(
      room.musicData?.color || 0xffffff,
      1,
      20
    );
    pointLight.position.copy(room.center);
    pointLight.position.y = this.wallHeight - 2;
    pointLight.castShadow = true;
    lights.push(pointLight);

    return lights;
  }

  /**
   * Get floor material based on room type
   */
  private getFloorMaterial(room: Room): THREE.Material {
    const baseColor = room.musicData?.color || 0x2a2a2a;
    
    return new THREE.MeshStandardMaterial({
      color: baseColor,
      roughness: 0.8,
      metalness: 0.2,
      emissive: baseColor,
      emissiveIntensity: 0.1,
    });
  }

  /**
   * Get wall material based on room type
   */
  private getWallMaterial(room: Room): THREE.Material {
    const color = room.type === 'sanctuary' ? 0x4a4a2a : 0x3a3a3a;
    
    return new THREE.MeshStandardMaterial({
      color,
      roughness: 0.9,
      metalness: 0.1,
    });
  }
}

