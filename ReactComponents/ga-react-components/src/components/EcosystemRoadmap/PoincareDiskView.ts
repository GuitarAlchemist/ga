// src/components/EcosystemRoadmap/PoincareDiskView.ts

import * as THREE from 'three';
import type { RoadmapNode, RoadmapView, ViewCallbacks } from './types';
import { LOD_THRESHOLDS } from './types';
import { depthToRadius, mobiusTransform2D, tileScale, layoutChildren2D } from './hyperbolicMath';
import { createTextTexture } from './textureUtils';
import { parentMap } from './roadmapData';

// ---------------------------------------------------------------------------
// Color helpers
// ---------------------------------------------------------------------------
function brightenColor(hex: string, factor = 1.4): string {
  const c = parseInt(hex.replace('#', ''), 16);
  const r = Math.min(255, Math.floor(((c >> 16) & 0xff) * factor));
  const g = Math.min(255, Math.floor(((c >> 8) & 0xff) * factor));
  const b = Math.min(255, Math.floor((c & 0xff) * factor));
  return `#${((r << 16) | (g << 8) | b).toString(16).padStart(6, '0')}`;
}

// ---------------------------------------------------------------------------
// Layout item produced by computeLayout
// ---------------------------------------------------------------------------
interface LayoutItem {
  node: RoadmapNode;
  ring: number;
  x: number;
  y: number;
  r: number; // distance from origin
  size: number;
}

// ---------------------------------------------------------------------------
// Animation state for Möbius re-centering
// ---------------------------------------------------------------------------
interface PlungeAnimation {
  items: Array<{
    mesh: THREE.Mesh;
    glowMesh: THREE.Mesh;
    label: THREE.Sprite;
    startX: number;
    startY: number;
    endX: number;
    endY: number;
    startScale: number;
    endScale: number;
  }>;
  edges: Array<{
    line: THREE.Line;
    startFrom: [number, number];
    startTo: [number, number];
    endFrom: [number, number];
    endTo: [number, number];
  }>;
  frame: number;
  totalFrames: number;
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------
export function createPoincareDiskView(
  scene: THREE.Scene,
  camera: THREE.PerspectiveCamera,
  root: RoadmapNode,
  callbacks: ViewCallbacks,
): RoadmapView {
  const KAPPA = 0.6;
  const BASE_NODE_SIZE = 0.04;
  const MAX_VISIBLE_DEPTH = 3;

  // Scene groups
  const bgGroup = new THREE.Group();
  bgGroup.name = 'poincare-bg';
  scene.add(bgGroup);

  const edgeGroup = new THREE.Group();
  edgeGroup.name = 'poincare-edges';
  scene.add(edgeGroup);

  const nodeGroup = new THREE.Group();
  nodeGroup.name = 'poincare-nodes';
  scene.add(nodeGroup);

  // Tracking for disposal
  const allGeometries: THREE.BufferGeometry[] = [];
  const allMaterials: THREE.Material[] = [];
  const allTextures: THREE.Texture[] = [];

  // Current state
  let focusNode: RoadmapNode = root;
  let layoutItems: LayoutItem[] = [];
  let nodeMeshes: THREE.Mesh[] = [];
  let glowMeshes: THREE.Mesh[] = [];
  let edgeLines: THREE.Line[] = [];
  let labelSprites: THREE.Sprite[] = [];

  // Hover state
  let hoveredMesh: THREE.Mesh | null = null;
  let hoveredOriginalColor: string | null = null;
  let hoveredOriginalScale: number = 1;

  // Animation state
  let plungeAnim: PlungeAnimation | null = null;

  // ---- Build background (boundary, grid, sphere effect) ----
  buildBackground();

  // ---- Initial layout ----
  rebuildLayout(focusNode);

  // ========================================================================
  // Background: boundary circle, grid lines, 3D sphere illusion
  // ========================================================================
  function buildBackground(): void {
    // Boundary ring
    const ringGeo = new THREE.RingGeometry(0.98, 1.0, 128);
    const ringMat = new THREE.MeshBasicMaterial({
      color: '#30363d',
      side: THREE.DoubleSide,
      transparent: true,
      opacity: 0.8,
      depthWrite: false,
    });
    const ringMesh = new THREE.Mesh(ringGeo, ringMat);
    ringMesh.position.set(0, 0, -0.01);
    bgGroup.add(ringMesh);
    allGeometries.push(ringGeo);
    allMaterials.push(ringMat);

    // 3D sphere effect — radial gradient using a canvas texture
    buildSphereOverlay();

    // Latitude lines (7 concentric ellipses)
    for (let i = 1; i <= 7; i++) {
      const f = i / 8;
      const rr = f;
      const squash = 1 - f * f * 0.22;
      const points: THREE.Vector3[] = [];
      for (let a = 0; a <= 128; a++) {
        const angle = (a / 128) * Math.PI * 2;
        points.push(new THREE.Vector3(
          rr * Math.cos(angle),
          rr * squash * Math.sin(angle),
          -0.005,
        ));
      }
      const geo = new THREE.BufferGeometry().setFromPoints(points);
      const mat = new THREE.LineDashedMaterial({
        color: '#1c2333',
        dashSize: 0.02,
        gapSize: 0.04,
        transparent: true,
        opacity: 0.4 - i * 0.04,
        depthWrite: false,
      });
      const line = new THREE.Line(geo, mat);
      line.computeLineDistances();
      bgGroup.add(line);
      allGeometries.push(geo);
      allMaterials.push(mat);
    }

    // Longitude great circles (12 rotated ellipses)
    for (let i = 0; i < 12; i++) {
      const rotAngle = (i / 12) * Math.PI;
      const points: THREE.Vector3[] = [];
      for (let a = 0; a <= 64; a++) {
        const angle = (a / 64) * Math.PI * 2;
        const x = Math.cos(angle);
        const y = 0.32 * Math.sin(angle);
        // Rotate by rotAngle
        const rx = x * Math.cos(rotAngle) - y * Math.sin(rotAngle);
        const ry = x * Math.sin(rotAngle) + y * Math.cos(rotAngle);
        points.push(new THREE.Vector3(rx, ry, -0.005));
      }
      const geo = new THREE.BufferGeometry().setFromPoints(points);
      const mat = new THREE.LineDashedMaterial({
        color: '#1c2333',
        dashSize: 0.02,
        gapSize: 0.05,
        transparent: true,
        opacity: 0.18,
        depthWrite: false,
      });
      const line = new THREE.Line(geo, mat);
      line.computeLineDistances();
      bgGroup.add(line);
      allGeometries.push(geo);
      allMaterials.push(mat);
    }

    // Depth rings (at Poincaré radii matching existing viz)
    const depthRingRadii = [0.28, 0.58, 0.86];
    for (const dr of depthRingRadii) {
      const points: THREE.Vector3[] = [];
      for (let a = 0; a <= 128; a++) {
        const angle = (a / 128) * Math.PI * 2;
        points.push(new THREE.Vector3(
          dr * Math.cos(angle),
          dr * Math.sin(angle),
          -0.004,
        ));
      }
      const geo = new THREE.BufferGeometry().setFromPoints(points);
      const mat = new THREE.LineDashedMaterial({
        color: '#21262d',
        dashSize: 0.03,
        gapSize: 0.05,
        transparent: true,
        opacity: 0.5,
        depthWrite: false,
      });
      const line = new THREE.Line(geo, mat);
      line.computeLineDistances();
      bgGroup.add(line);
      allGeometries.push(geo);
      allMaterials.push(mat);
    }
  }

  // ========================================================================
  // 3D sphere illusion via radial gradient canvas texture
  // ========================================================================
  function buildSphereOverlay(): void {
    const size = 512;
    const canvas = document.createElement('canvas');
    canvas.width = size;
    canvas.height = size;
    const ctx = canvas.getContext('2d')!;

    // Main sphere shading (off-center highlight like the SVG)
    const grd = ctx.createRadialGradient(
      size * 0.38, size * 0.35, 0,
      size * 0.5, size * 0.5, size * 0.5,
    );
    grd.addColorStop(0, 'rgba(30,42,58,0.2)');
    grd.addColorStop(0.55, 'rgba(17,24,32,0.12)');
    grd.addColorStop(0.85, 'rgba(8,12,18,0.5)');
    grd.addColorStop(1, 'rgba(0,0,0,0.85)');
    ctx.fillStyle = grd;
    ctx.beginPath();
    ctx.arc(size / 2, size / 2, size / 2, 0, Math.PI * 2);
    ctx.fill();

    // Specular highlight
    const spec = ctx.createRadialGradient(
      size * 0.32, size * 0.30, 0,
      size * 0.32, size * 0.30, size * 0.35,
    );
    spec.addColorStop(0, 'rgba(176,196,222,0.05)');
    spec.addColorStop(1, 'rgba(176,196,222,0)');
    ctx.fillStyle = spec;
    ctx.beginPath();
    ctx.arc(size / 2, size / 2, size / 2, 0, Math.PI * 2);
    ctx.fill();

    const texture = new THREE.CanvasTexture(canvas);
    texture.minFilter = THREE.LinearFilter;
    texture.magFilter = THREE.LinearFilter;
    allTextures.push(texture);

    const geo = new THREE.CircleGeometry(1.0, 128);
    const mat = new THREE.MeshBasicMaterial({
      map: texture,
      transparent: true,
      depthWrite: false,
    });
    const mesh = new THREE.Mesh(geo, mat);
    mesh.position.set(0, 0, -0.008);
    bgGroup.add(mesh);
    allGeometries.push(geo);
    allMaterials.push(mat);
  }

  // ========================================================================
  // Layout computation — 3 levels from focus
  // ========================================================================
  function computeLayout(focus: RoadmapNode): LayoutItem[] {
    const items: LayoutItem[] = [];

    // Ring 0: focus at center
    items.push({ node: focus, ring: 0, x: 0, y: 0, r: 0, size: BASE_NODE_SIZE * 2 });

    const kids = focus.children ?? [];
    kids.forEach((child, i) => {
      const theta = (i / kids.length) * 2 * Math.PI - Math.PI / 2;
      const r1 = depthToRadius(1, KAPPA);
      const x = r1 * Math.cos(theta);
      const y = r1 * Math.sin(theta);
      items.push({ node: child, ring: 1, x, y, r: r1, size: BASE_NODE_SIZE * 1.4 });

      const grandkids = child.children ?? [];
      grandkids.forEach((gk, j) => {
        const spread = Math.min(0.5, 1.2 / kids.length);
        const gkTheta = theta + (j - (grandkids.length - 1) / 2)
          * spread / Math.max(grandkids.length - 1, 1) * 2;
        const r2 = depthToRadius(2, KAPPA);
        const gkR = Math.min(r2 + (grandkids.length > 5 ? 0.04 : 0) + (j % 2) * 0.02, 0.92);
        const gx = gkR * Math.cos(gkTheta);
        const gy = gkR * Math.sin(gkTheta);
        items.push({ node: gk, ring: 2, x: gx, y: gy, r: gkR, size: BASE_NODE_SIZE });

        // Great-grandchildren (ring 3)
        const greatGrandkids = gk.children ?? [];
        greatGrandkids.forEach((ggk, k) => {
          const ggSpread = Math.min(0.3, 0.8 / Math.max(grandkids.length, 1));
          const ggTheta = gkTheta + (k - (greatGrandkids.length - 1) / 2)
            * ggSpread / Math.max(greatGrandkids.length - 1, 1) * 2;
          const r3 = depthToRadius(3, KAPPA);
          const ggR = Math.min(r3 + (k % 2) * 0.01, 0.96);
          const ggx = ggR * Math.cos(ggTheta);
          const ggy = ggR * Math.sin(ggTheta);
          items.push({ node: ggk, ring: 3, x: ggx, y: ggy, r: ggR, size: BASE_NODE_SIZE * 0.7 });
        });
      });
    });

    return items;
  }

  // ========================================================================
  // Build/rebuild the node and edge scene objects
  // ========================================================================
  function rebuildLayout(focus: RoadmapNode): void {
    // Clear previous
    clearNodes();

    layoutItems = computeLayout(focus);

    // Build edges first (behind nodes)
    for (const item of layoutItems) {
      if (item.ring === 0) continue;

      // Find parent in layout
      let parentItem: LayoutItem | undefined;
      if (item.ring === 1) {
        parentItem = layoutItems.find(li => li.ring === 0);
      } else if (item.ring === 2) {
        parentItem = layoutItems.find(li =>
          li.ring === 1 && li.node.children?.includes(item.node));
      } else if (item.ring === 3) {
        parentItem = layoutItems.find(li =>
          li.ring === 2 && li.node.children?.includes(item.node));
      }

      if (parentItem) {
        const points = [
          new THREE.Vector3(parentItem.x, parentItem.y, 0.001),
          new THREE.Vector3(item.x, item.y, 0.001),
        ];
        const geo = new THREE.BufferGeometry().setFromPoints(points);
        const mat = new THREE.LineBasicMaterial({
          color: '#30363d',
          transparent: true,
          opacity: item.ring === 1 ? 0.25 : 0.12,
          depthWrite: false,
        });
        const line = new THREE.Line(geo, mat);
        edgeGroup.add(line);
        edgeLines.push(line);
        allGeometries.push(geo);
        allMaterials.push(mat);
      }
    }

    // Build nodes
    for (const item of layoutItems) {
      const nodeRadius = tileScale(item.size, item.r);

      // Glow circle (slightly larger, semi-transparent)
      const glowGeo = new THREE.CircleGeometry(nodeRadius * 1.6, 32);
      const glowMat = new THREE.MeshBasicMaterial({
        color: item.node.color,
        transparent: true,
        opacity: 0.15,
        depthWrite: false,
      });
      const glowMesh = new THREE.Mesh(glowGeo, glowMat);
      glowMesh.position.set(item.x, item.y, 0.002);
      nodeGroup.add(glowMesh);
      glowMeshes.push(glowMesh);
      allGeometries.push(glowGeo);
      allMaterials.push(glowMat);

      // Node circle
      const nodeGeo = new THREE.CircleGeometry(nodeRadius, 32);
      const nodeMat = new THREE.MeshBasicMaterial({
        color: item.node.color,
        transparent: true,
        opacity: Math.max(0.5, 1.0 - item.ring * 0.15),
        depthWrite: false,
      });
      const mesh = new THREE.Mesh(nodeGeo, nodeMat);
      mesh.position.set(item.x, item.y, 0.003);
      mesh.userData.node = item.node;
      mesh.userData.ring = item.ring;
      mesh.userData.originalColor = item.node.color;
      mesh.userData.layoutItem = item;
      nodeGroup.add(mesh);
      nodeMeshes.push(mesh);
      allGeometries.push(nodeGeo);
      allMaterials.push(nodeMat);

      // Text label
      const fontSize = item.ring === 0 ? 28 : item.ring === 1 ? 20 : 14;
      const tex = createTextTexture(item.node.name, {
        fontSize,
        color: '#ffffff',
        subtitle: item.node.sub,
        subtitleColor: '#d0d0d0',
      });
      allTextures.push(tex);

      const spriteMat = new THREE.SpriteMaterial({
        map: tex,
        transparent: true,
        depthWrite: false,
      });
      const sprite = new THREE.Sprite(spriteMat);
      const aspect = tex.image
        ? (tex.image as HTMLCanvasElement).width /
          (tex.image as HTMLCanvasElement).height
        : 3;
      const labelScale = tileScale(item.ring === 0 ? 0.18 : item.ring === 1 ? 0.12 : 0.08, item.r);
      sprite.scale.set(labelScale * aspect, labelScale, 1);
      // Position label below node
      sprite.position.set(item.x, item.y - nodeRadius * 1.8, 0.004);
      sprite.userData.ring = item.ring;
      nodeGroup.add(sprite);
      labelSprites.push(sprite);
      allMaterials.push(spriteMat);
    }
  }

  // ========================================================================
  // Clear current node/edge objects from scene
  // ========================================================================
  function clearNodes(): void {
    for (const mesh of nodeMeshes) nodeGroup.remove(mesh);
    for (const mesh of glowMeshes) nodeGroup.remove(mesh);
    for (const line of edgeLines) edgeGroup.remove(line);
    for (const sprite of labelSprites) nodeGroup.remove(sprite);
    nodeMeshes = [];
    glowMeshes = [];
    edgeLines = [];
    labelSprites = [];
  }

  // ========================================================================
  // Möbius re-centering animation — plunge into a child node
  // ========================================================================
  function startPlunge(targetNode: RoadmapNode): void {
    // Find target item in current layout
    const targetItem = layoutItems.find(li => li.node === targetNode);
    if (!targetItem || (targetItem.x === 0 && targetItem.y === 0)) return;

    const a: [number, number] = [targetItem.x, targetItem.y];

    // Compute new positions via Möbius transform for all current items
    const newPositions: Array<{ x: number; y: number }> = layoutItems.map(item => {
      const z: [number, number] = [item.x, item.y];
      const [nx, ny] = mobiusTransform2D(z, a);
      return { x: nx, y: ny };
    });

    // Build animation: tween current positions to Möbius-transformed positions
    const animItems: PlungeAnimation['items'] = [];
    for (let i = 0; i < layoutItems.length; i++) {
      if (i < nodeMeshes.length) {
        const np = newPositions[i];
        const newR = Math.sqrt(np.x * np.x + np.y * np.y);
        const currentItem = layoutItems[i];
        const currentR = currentItem.r;
        animItems.push({
          mesh: nodeMeshes[i],
          glowMesh: glowMeshes[i],
          label: labelSprites[i],
          startX: currentItem.x,
          startY: currentItem.y,
          endX: np.x,
          endY: np.y,
          startScale: tileScale(currentItem.size, currentR),
          endScale: tileScale(currentItem.size, newR),
        });
      }
    }

    // Edge animation
    const animEdges: PlungeAnimation['edges'] = [];
    let edgeIdx = 0;
    for (const item of layoutItems) {
      if (item.ring === 0) continue;
      let parentIdx = -1;
      if (item.ring === 1) {
        parentIdx = 0;
      } else if (item.ring === 2) {
        parentIdx = layoutItems.findIndex(li =>
          li.ring === 1 && li.node.children?.includes(item.node));
      } else if (item.ring === 3) {
        parentIdx = layoutItems.findIndex(li =>
          li.ring === 2 && li.node.children?.includes(item.node));
      }

      if (parentIdx >= 0 && edgeIdx < edgeLines.length) {
        const itemIdx = layoutItems.indexOf(item);
        animEdges.push({
          line: edgeLines[edgeIdx],
          startFrom: [layoutItems[parentIdx].x, layoutItems[parentIdx].y],
          startTo: [item.x, item.y],
          endFrom: [newPositions[parentIdx].x, newPositions[parentIdx].y],
          endTo: [newPositions[itemIdx].x, newPositions[itemIdx].y],
        });
        edgeIdx++;
      }
    }

    plungeAnim = {
      items: animItems,
      edges: animEdges,
      frame: 0,
      totalFrames: 40,
    };
  }

  // ========================================================================
  // Easing
  // ========================================================================
  function ease(t: number): number {
    return t * t * (3 - 2 * t);
  }

  // ========================================================================
  // Step plunge animation
  // ========================================================================
  function stepAnimation(): void {
    if (!plungeAnim) return;
    plungeAnim.frame++;
    const t = ease(Math.min(plungeAnim.frame / plungeAnim.totalFrames, 1));

    for (const ai of plungeAnim.items) {
      const x = ai.startX + (ai.endX - ai.startX) * t;
      const y = ai.startY + (ai.endY - ai.startY) * t;
      ai.mesh.position.set(x, y, 0.003);
      ai.glowMesh.position.set(x, y, 0.002);

      // Scale interpolation
      const scale = ai.startScale + (ai.endScale - ai.startScale) * t;
      const nodeRadius = scale;
      ai.label.position.set(x, y - nodeRadius * 1.8, 0.004);
    }

    for (const ae of plungeAnim.edges) {
      const fx = ae.startFrom[0] + (ae.endFrom[0] - ae.startFrom[0]) * t;
      const fy = ae.startFrom[1] + (ae.endFrom[1] - ae.startFrom[1]) * t;
      const tx = ae.startTo[0] + (ae.endTo[0] - ae.startTo[0]) * t;
      const ty = ae.startTo[1] + (ae.endTo[1] - ae.startTo[1]) * t;
      const positions = ae.line.geometry.getAttribute('position') as THREE.BufferAttribute;
      positions.setXYZ(0, fx, fy, 0.001);
      positions.setXYZ(1, tx, ty, 0.001);
      positions.needsUpdate = true;
    }

    if (plungeAnim.frame >= plungeAnim.totalFrames) {
      plungeAnim = null;
      // After animation completes, rebuild layout centered on new focus
      rebuildLayout(focusNode);
    }
  }

  // ========================================================================
  // LOD visibility
  // ========================================================================
  function updateLOD(zoom: number): void {
    for (const sprite of labelSprites) {
      const ring = sprite.userData.ring as number;
      if (zoom < LOD_THRESHOLDS.LABELS_DEPTH_01) {
        sprite.visible = ring <= 1;
      } else if (zoom < LOD_THRESHOLDS.LABELS_DEPTH_02) {
        sprite.visible = ring <= 2;
      } else {
        sprite.visible = true;
      }
    }
  }

  // Initial LOD
  updateLOD(1.0);

  // ========================================================================
  // RoadmapView interface
  // ========================================================================
  function update(selected: RoadmapNode | null, zoom: number): void {
    updateLOD(zoom);
    stepAnimation();
  }

  function handleClick(raycaster: THREE.Raycaster): void {
    const intersects = raycaster.intersectObjects(nodeMeshes, false);
    if (intersects.length === 0) return;

    const hit = intersects[0].object as THREE.Mesh;
    const node = hit.userData.node as RoadmapNode | undefined;
    if (!node) return;

    callbacks.onNodeClick(node);

    // If clicked node has children: plunge into it
    if (node.children && node.children.length > 0 && node !== focusNode) {
      // Animate Möbius transform, then rebuild
      startPlunge(node);
      focusNode = node;
    } else if (node === focusNode) {
      // Surface: go up to parent
      const parent = parentMap.get(node);
      if (parent) {
        focusNode = parent;
        rebuildLayout(focusNode);
      }
    }
  }

  function handleHover(raycaster: THREE.Raycaster): void {
    const intersects = raycaster.intersectObjects(nodeMeshes, false);

    // Unhighlight previous
    if (hoveredMesh && hoveredOriginalColor) {
      (hoveredMesh.material as THREE.MeshBasicMaterial).color.set(hoveredOriginalColor);
      hoveredMesh.scale.set(1, 1, 1);
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
    hoveredOriginalScale = hit.scale.x;
    (hit.material as THREE.MeshBasicMaterial).color.set(
      brightenColor(hoveredOriginalColor),
    );
    hit.scale.set(1.15, 1.15, 1);

    callbacks.onNodeHover(node);
  }

  function dispose(): void {
    for (const geo of allGeometries) geo.dispose();
    for (const mat of allMaterials) mat.dispose();
    for (const tex of allTextures) tex.dispose();
    scene.remove(bgGroup);
    scene.remove(edgeGroup);
    scene.remove(nodeGroup);
  }

  return { update, handleClick, handleHover, dispose };
}
