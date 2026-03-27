// src/components/EcosystemRoadmap/IcicleView.ts

import * as THREE from 'three';
import * as d3 from 'd3';
import type { RoadmapNode, RoadmapView, ViewCallbacks } from './types';
import { LOD_THRESHOLDS } from './types';
import { createTextTexture } from './textureUtils';

// ---------------------------------------------------------------------------
// Types for d3 partition layout nodes
// ---------------------------------------------------------------------------
type PartitionNode = d3.HierarchyRectangularNode<RoadmapNode>;

// ---------------------------------------------------------------------------
// Camera animation state
// ---------------------------------------------------------------------------
interface CameraAnimation {
  startLeft: number;
  startRight: number;
  startTop: number;
  startBottom: number;
  endLeft: number;
  endRight: number;
  endTop: number;
  endBottom: number;
  frame: number;
  totalFrames: number;
}

// ---------------------------------------------------------------------------
// Darken a hex colour for borders
// ---------------------------------------------------------------------------
function darkenColor(hex: string, factor = 0.7): string {
  const c = parseInt(hex.replace('#', ''), 16);
  const r = Math.max(0, Math.floor(((c >> 16) & 0xff) * factor));
  const g = Math.max(0, Math.floor(((c >> 8) & 0xff) * factor));
  const b = Math.max(0, Math.floor((c & 0xff) * factor));
  return `#${((r << 16) | (g << 8) | b).toString(16).padStart(6, '0')}`;
}

// ---------------------------------------------------------------------------
// Brighten a hex colour for hover highlight
// ---------------------------------------------------------------------------
function brightenColor(hex: string, factor = 1.3): string {
  const c = parseInt(hex.replace('#', ''), 16);
  const r = Math.min(255, Math.floor(((c >> 16) & 0xff) * factor));
  const g = Math.min(255, Math.floor(((c >> 8) & 0xff) * factor));
  const b = Math.min(255, Math.floor((c & 0xff) * factor));
  return `#${((r << 16) | (g << 8) | b).toString(16).padStart(6, '0')}`;
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------
export function createIcicleView(
  scene: THREE.Scene,
  camera: THREE.OrthographicCamera,
  root: RoadmapNode,
  callbacks: ViewCallbacks,
): RoadmapView {
  const PARTITION_WIDTH = 20;
  const PARTITION_HEIGHT = 12;
  const OFFSET_X = -PARTITION_WIDTH / 2;  // -10
  const OFFSET_Y = -PARTITION_HEIGHT / 2; // -6
  const BORDER_PAD = 0.04; // border thickness in world units

  // ---- Build d3 partition layout ----
  const hierarchy = d3.hierarchy<RoadmapNode>(root, (d) => d.children ?? []);
  const partition = d3
    .partition<RoadmapNode>()
    .size([PARTITION_WIDTH, PARTITION_HEIGHT]);
  partition(hierarchy);

  // Map from partition node → original RoadmapNode (for userData)
  const partitionNodes: PartitionNode[] = hierarchy.descendants() as PartitionNode[];

  // ---- Scene group ----
  const tilesGroup = new THREE.Group();
  tilesGroup.name = 'icicle-tiles';
  scene.add(tilesGroup);

  // Track meshes for raycasting and disposal
  const tileMeshes: THREE.Mesh[] = [];
  const borderMeshes: THREE.Mesh[] = [];
  const labelSprites: THREE.Sprite[] = [];
  const allGeometries: THREE.BufferGeometry[] = [];
  const allMaterials: THREE.Material[] = [];
  const allTextures: THREE.Texture[] = [];

  // Hover state
  let hoveredMesh: THREE.Mesh | null = null;
  let hoveredOriginalColor: string | null = null;

  // Camera animation state
  let cameraAnim: CameraAnimation | null = null;

  // ---- Build tiles ----
  for (const pNode of partitionNodes) {
    const depth = pNode.depth;
    const nodeData = pNode.data;
    const x0 = pNode.x0 + OFFSET_X;
    const x1 = pNode.x1 + OFFSET_X;
    const y0 = pNode.y0 + OFFSET_Y;
    const y1 = pNode.y1 + OFFSET_Y;
    const w = x1 - x0;
    const h = y1 - y0;
    if (w <= 0 || h <= 0) continue;

    const cx = (x0 + x1) / 2;
    const cy = (y0 + y1) / 2;
    const zTile = 0.01 * depth;

    // -- Border (slightly larger plane behind) --
    const borderGeo = new THREE.PlaneGeometry(w + BORDER_PAD, h + BORDER_PAD);
    const borderMat = new THREE.MeshBasicMaterial({
      color: darkenColor(nodeData.color),
      transparent: true,
      opacity: Math.max(0.3, 1.0 - depth * 0.1),
      depthWrite: false,
    });
    const borderMesh = new THREE.Mesh(borderGeo, borderMat);
    borderMesh.position.set(cx, cy, zTile - 0.001);
    tilesGroup.add(borderMesh);
    borderMeshes.push(borderMesh);
    allGeometries.push(borderGeo);
    allMaterials.push(borderMat);

    // -- Tile face --
    const tileGeo = new THREE.PlaneGeometry(w, h);
    const tileMat = new THREE.MeshBasicMaterial({
      color: nodeData.color,
      transparent: true,
      opacity: Math.max(0.3, 1.0 - depth * 0.1),
      depthWrite: false,
    });
    const tileMesh = new THREE.Mesh(tileGeo, tileMat);
    tileMesh.position.set(cx, cy, zTile);
    tileMesh.userData.node = nodeData;
    tileMesh.userData.depth = depth;
    tileMesh.userData.originalColor = nodeData.color;
    tilesGroup.add(tileMesh);
    tileMeshes.push(tileMesh);
    allGeometries.push(tileGeo);
    allMaterials.push(tileMat);

    // -- Label sprite --
    const fontSize = depth === 0 ? 28 : depth === 1 ? 22 : 16;
    const texture = createTextTexture(nodeData.name, {
      fontSize,
      color: '#ffffff',
      subtitle: nodeData.sub,
      subtitleColor: '#d0d0d0',
    });
    allTextures.push(texture);

    const spriteMat = new THREE.SpriteMaterial({
      map: texture,
      transparent: true,
      depthWrite: false,
    });
    const sprite = new THREE.Sprite(spriteMat);
    // Scale sprite to fit tile (clamp to tile dimensions)
    const aspect = texture.image
      ? (texture.image as HTMLCanvasElement).width /
        (texture.image as HTMLCanvasElement).height
      : 3;
    const spriteH = Math.min(h * 0.7, 0.8);
    const spriteW = Math.min(spriteH * aspect, w * 0.9);
    sprite.scale.set(spriteW, spriteH, 1);
    sprite.position.set(cx, cy, 0.02 * depth + 0.005);
    sprite.userData.depth = depth;
    tilesGroup.add(sprite);
    labelSprites.push(sprite);
    allMaterials.push(spriteMat);
  }

  // ---- LOD visibility helper ----
  function updateLOD(zoom: number): void {
    for (const sprite of labelSprites) {
      const d = sprite.userData.depth as number;
      if (zoom < LOD_THRESHOLDS.LABELS_DEPTH_01) {
        sprite.visible = d <= 1;
      } else if (zoom < LOD_THRESHOLDS.LABELS_DEPTH_02) {
        sprite.visible = d <= 2;
      } else {
        sprite.visible = true;
      }
    }
  }

  // Initial LOD
  updateLOD(1.0);

  // ---- Easing (smooth-step) ----
  function ease(t: number): number {
    return t * t * (3 - 2 * t);
  }

  // ---- Camera animation interpolation ----
  function stepCameraAnimation(): void {
    if (!cameraAnim) return;
    cameraAnim.frame++;
    const t = ease(Math.min(cameraAnim.frame / cameraAnim.totalFrames, 1));
    camera.left =
      cameraAnim.startLeft + (cameraAnim.endLeft - cameraAnim.startLeft) * t;
    camera.right =
      cameraAnim.startRight +
      (cameraAnim.endRight - cameraAnim.startRight) * t;
    camera.top =
      cameraAnim.startTop + (cameraAnim.endTop - cameraAnim.startTop) * t;
    camera.bottom =
      cameraAnim.startBottom +
      (cameraAnim.endBottom - cameraAnim.startBottom) * t;
    camera.updateProjectionMatrix();
    if (cameraAnim.frame >= cameraAnim.totalFrames) {
      cameraAnim = null;
    }
  }

  // ---- RoadmapView implementation ----

  function update(selected: RoadmapNode | null, zoom: number): void {
    updateLOD(zoom);
    stepCameraAnimation();
  }

  function handleClick(raycaster: THREE.Raycaster): void {
    const intersects = raycaster.intersectObjects(tileMeshes, false);
    if (intersects.length === 0) return;
    const hit = intersects[0].object as THREE.Mesh;
    const node = hit.userData.node as RoadmapNode | undefined;
    if (!node) return;

    callbacks.onNodeClick(node);

    // Find the partition node for this clicked node to get bounds
    const pNode = partitionNodes.find((pn) => pn.data === node);
    if (!pNode) return;

    const x0 = pNode.x0 + OFFSET_X;
    const x1 = pNode.x1 + OFFSET_X;
    const y0 = pNode.y0 + OFFSET_Y;
    const y1 = pNode.y1 + OFFSET_Y;

    // Animate camera to fill clicked node's children area
    // Add a small margin
    const margin = 0.5;
    cameraAnim = {
      startLeft: camera.left,
      startRight: camera.right,
      startTop: camera.top,
      startBottom: camera.bottom,
      endLeft: x0 - margin,
      endRight: x1 + margin,
      endTop: y1 + margin,
      endBottom: y0 - margin,
      frame: 0,
      totalFrames: 30,
    };
  }

  function handleHover(raycaster: THREE.Raycaster): void {
    const intersects = raycaster.intersectObjects(tileMeshes, false);

    // Unhighlight previous
    if (hoveredMesh && hoveredOriginalColor) {
      (hoveredMesh.material as THREE.MeshBasicMaterial).color.set(
        hoveredOriginalColor,
      );
      hoveredMesh = null;
      hoveredOriginalColor = null;
    }

    if (intersects.length === 0) {
      callbacks.onNodeHover(null);
      return;
    }

    const hit = intersects[0].object as THREE.Mesh;
    const node = hit.userData.node as RoadmapNode | undefined;
    if (!node) return;

    hoveredMesh = hit;
    hoveredOriginalColor = hit.userData.originalColor as string;
    (hit.material as THREE.MeshBasicMaterial).color.set(
      brightenColor(hoveredOriginalColor),
    );

    callbacks.onNodeHover(node);
  }

  function dispose(): void {
    for (const geo of allGeometries) geo.dispose();
    for (const mat of allMaterials) mat.dispose();
    for (const tex of allTextures) tex.dispose();
    scene.remove(tilesGroup);
  }

  return { update, handleClick, handleHover, dispose };
}
