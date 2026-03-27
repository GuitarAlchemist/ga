// src/components/EcosystemRoadmap/PoincareBallView.ts

import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import type { RoadmapNode, RoadmapView, ViewCallbacks } from './types';
import { LOD_THRESHOLDS } from './types';
import { depthToRadius3D, fibonacciSphere, gyrationTransform3D, tileScale } from './hyperbolicMath';
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
interface LayoutItem3D {
  node: RoadmapNode;
  ring: number;
  pos: THREE.Vector3;
  size: number;
}

// ---------------------------------------------------------------------------
// Animation state for gyration re-centering
// ---------------------------------------------------------------------------
interface PlungeAnimation3D {
  items: Array<{
    mesh: THREE.Mesh;
    glowSprite: THREE.Sprite;
    label: THREE.Sprite;
    startPos: THREE.Vector3;
    endPos: THREE.Vector3;
    startScale: number;
    endScale: number;
  }>;
  edges: Array<{
    line: THREE.Line;
    startFrom: THREE.Vector3;
    startTo: THREE.Vector3;
    endFrom: THREE.Vector3;
    endTo: THREE.Vector3;
  }>;
  frame: number;
  totalFrames: number;
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------
export function createPoincareBallView(
  scene: THREE.Scene,
  camera: THREE.PerspectiveCamera,
  renderer: THREE.WebGLRenderer,
  root: RoadmapNode,
  callbacks: ViewCallbacks,
): RoadmapView & { controls: OrbitControls } {
  const BASE_NODE_SIZE = 0.04;
  const _MAX_VISIBLE_DEPTH = 3;

  // OrbitControls
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableRotate = true;
  controls.enableZoom = true;
  controls.enablePan = true;
  controls.enableDamping = true;
  controls.dampingFactor = 0.12;
  controls.minDistance = 0.5;
  controls.maxDistance = 5.0;

  // Scene groups
  const bgGroup = new THREE.Group();
  bgGroup.name = 'ball-bg';
  scene.add(bgGroup);

  const edgeGroup = new THREE.Group();
  edgeGroup.name = 'ball-edges';
  scene.add(edgeGroup);

  const nodeGroup = new THREE.Group();
  nodeGroup.name = 'ball-nodes';
  scene.add(nodeGroup);

  // Tracking for disposal
  const allGeometries: THREE.BufferGeometry[] = [];
  const allMaterials: THREE.Material[] = [];
  const allTextures: THREE.Texture[] = [];

  // Current state
  let focusNode: RoadmapNode = root;
  let layoutItems: LayoutItem3D[] = [];
  let nodeMeshes: THREE.Mesh[] = [];
  let glowSprites: THREE.Sprite[] = [];
  let edgeLines: THREE.Line[] = [];
  let labelSprites: THREE.Sprite[] = [];

  // Hover state
  let hoveredMesh: THREE.Mesh | null = null;
  let hoveredOriginalColor: string | null = null;

  // Animation state
  let plungeAnim: PlungeAnimation3D | null = null;

  // Lighting
  const ambientLight = new THREE.AmbientLight('#ffffff', 0.6);
  scene.add(ambientLight);

  const pointLight = new THREE.PointLight('#ffffff', 0.8, 10);
  pointLight.position.copy(camera.position);
  scene.add(pointLight);

  // ---- Glow texture (shared radial gradient) ----
  const glowTexture = createGlowTexture();
  allTextures.push(glowTexture);

  // ---- Build background (translucent sphere shell) ----
  buildBackground();

  // ---- Initial layout ----
  rebuildLayout(focusNode);

  // ========================================================================
  // Glow texture — a soft radial gradient on canvas
  // ========================================================================
  function createGlowTexture(): THREE.CanvasTexture {
    const size = 128;
    const canvas = document.createElement('canvas');
    canvas.width = size;
    canvas.height = size;
    const ctx = canvas.getContext('2d')!;
    const grd = ctx.createRadialGradient(
      size / 2, size / 2, 0,
      size / 2, size / 2, size / 2,
    );
    grd.addColorStop(0, 'rgba(255,255,255,1)');
    grd.addColorStop(0.3, 'rgba(255,255,255,0.5)');
    grd.addColorStop(1, 'rgba(255,255,255,0)');
    ctx.fillStyle = grd;
    ctx.fillRect(0, 0, size, size);
    const tex = new THREE.CanvasTexture(canvas);
    tex.minFilter = THREE.LinearFilter;
    tex.magFilter = THREE.LinearFilter;
    return tex;
  }

  // ========================================================================
  // Background: translucent sphere shell + wireframe overlay
  // ========================================================================
  function buildBackground(): void {
    // Translucent sphere shell
    const shellGeo = new THREE.SphereGeometry(1.0, 64, 64);
    const shellMat = new THREE.MeshPhysicalMaterial({
      transparent: true,
      opacity: 0.08,
      color: '#30363d',
      side: THREE.BackSide,
      depthWrite: false,
    });
    const shellMesh = new THREE.Mesh(shellGeo, shellMat);
    bgGroup.add(shellMesh);
    allGeometries.push(shellGeo);
    allMaterials.push(shellMat);

    // Wireframe overlay
    const wireGeo = new THREE.SphereGeometry(1.0, 24, 24);
    const wireMat = new THREE.MeshBasicMaterial({
      wireframe: true,
      color: '#1c2333',
      opacity: 0.15,
      transparent: true,
      depthWrite: false,
    });
    const wireMesh = new THREE.Mesh(wireGeo, wireMat);
    bgGroup.add(wireMesh);
    allGeometries.push(wireGeo);
    allMaterials.push(wireMat);
  }

  // ========================================================================
  // Layout computation — 3 levels from focus inside Poincaré ball
  // ========================================================================
  function computeLayout(focus: RoadmapNode): LayoutItem3D[] {
    const items: LayoutItem3D[] = [];

    // Ring 0: focus at center
    items.push({
      node: focus,
      ring: 0,
      pos: new THREE.Vector3(0, 0, 0),
      size: BASE_NODE_SIZE * 2,
    });

    const kids = focus.children ?? [];
    if (kids.length === 0) return items;

    // Ring 1: children distributed via fibonacci sphere
    const r1 = depthToRadius3D(1);
    const childPositions = fibonacciSphere(kids.length, r1);

    kids.forEach((child, i) => {
      const pos = childPositions[i];
      items.push({
        node: child,
        ring: 1,
        pos: pos.clone(),
        size: BASE_NODE_SIZE * 1.4,
      });

      // Ring 2: grandchildren
      const grandkids = child.children ?? [];
      if (grandkids.length === 0) return;

      const r2 = depthToRadius3D(2);
      // Distribute grandchildren in a cone around the parent direction
      const parentDir = pos.clone().normalize();
      const gkPositions = distributeAroundDirection(parentDir, grandkids.length, r2, 0.4);

      grandkids.forEach((gk, j) => {
        const gkPos = gkPositions[j];
        items.push({
          node: gk,
          ring: 2,
          pos: gkPos.clone(),
          size: BASE_NODE_SIZE,
        });

        // Ring 3: great-grandchildren
        const greatGrandkids = gk.children ?? [];
        if (greatGrandkids.length === 0) return;

        const r3 = depthToRadius3D(3);
        const gkDir = gkPos.clone().normalize();
        const ggkPositions = distributeAroundDirection(gkDir, greatGrandkids.length, r3, 0.25);

        greatGrandkids.forEach((ggk, k) => {
          items.push({
            node: ggk,
            ring: 3,
            pos: ggkPositions[k].clone(),
            size: BASE_NODE_SIZE * 0.7,
          });
        });
      });
    });

    return items;
  }

  // ========================================================================
  // Distribute N points in a cone around a given direction at given radius
  // ========================================================================
  function distributeAroundDirection(
    dir: THREE.Vector3,
    count: number,
    radius: number,
    coneAngle: number,
  ): THREE.Vector3[] {
    if (count === 0) return [];
    if (count === 1) {
      return [dir.clone().multiplyScalar(radius)];
    }

    const results: THREE.Vector3[] = [];

    // Build a local basis around `dir`
    const up = Math.abs(dir.y) < 0.99
      ? new THREE.Vector3(0, 1, 0)
      : new THREE.Vector3(1, 0, 0);
    const tangent = new THREE.Vector3().crossVectors(dir, up).normalize();
    const bitangent = new THREE.Vector3().crossVectors(dir, tangent).normalize();

    for (let i = 0; i < count; i++) {
      const angle = (i / count) * Math.PI * 2;
      const spread = coneAngle * (0.5 + 0.5 * ((i % 3) / 3)); // slight variation
      const offset = new THREE.Vector3()
        .addScaledVector(tangent, Math.cos(angle) * spread)
        .addScaledVector(bitangent, Math.sin(angle) * spread);
      const pos = dir.clone().add(offset).normalize().multiplyScalar(radius);
      // Clamp inside unit ball
      if (pos.length() >= 0.98) {
        pos.normalize().multiplyScalar(0.97);
      }
      results.push(pos);
    }

    return results;
  }

  // ========================================================================
  // Build/rebuild the node and edge scene objects
  // ========================================================================
  function rebuildLayout(focus: RoadmapNode): void {
    clearNodes();
    layoutItems = computeLayout(focus);

    // Build edges first (behind nodes)
    for (const item of layoutItems) {
      if (item.ring === 0) continue;

      let parentItem: LayoutItem3D | undefined;
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
        const points = [parentItem.pos.clone(), item.pos.clone()];
        const geo = new THREE.BufferGeometry().setFromPoints(points);
        const mat = new THREE.LineBasicMaterial({
          color: '#30363d',
          transparent: true,
          opacity: item.ring === 1 ? 0.4 : 0.2,
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
      const r = item.pos.length();
      const nodeRadius = tileScale(item.size, r);

      // Node sphere
      const nodeGeo = new THREE.SphereGeometry(nodeRadius, 16, 16);
      const nodeMat = new THREE.MeshBasicMaterial({
        color: item.node.color,
        transparent: true,
        opacity: Math.max(0.5, 1.0 - item.ring * 0.15),
      });
      const mesh = new THREE.Mesh(nodeGeo, nodeMat);
      mesh.position.copy(item.pos);
      mesh.userData.node = item.node;
      mesh.userData.ring = item.ring;
      mesh.userData.originalColor = item.node.color;
      mesh.userData.layoutItem = item;
      nodeGroup.add(mesh);
      nodeMeshes.push(mesh);
      allGeometries.push(nodeGeo);
      allMaterials.push(nodeMat);

      // Glow sprite behind each node
      const glowMat = new THREE.SpriteMaterial({
        map: glowTexture,
        color: item.node.color,
        transparent: true,
        opacity: 0.3,
        depthWrite: false,
      });
      const glow = new THREE.Sprite(glowMat);
      const glowScale = nodeRadius * 3;
      glow.scale.set(glowScale, glowScale, 1);
      glow.position.copy(item.pos);
      nodeGroup.add(glow);
      glowSprites.push(glow);
      allMaterials.push(glowMat);

      // Text label (billboard sprite)
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
      const labelScale = tileScale(
        item.ring === 0 ? 0.18 : item.ring === 1 ? 0.12 : 0.08,
        r,
      );
      sprite.scale.set(labelScale * aspect, labelScale, 1);
      // Position label slightly below node
      sprite.position.set(
        item.pos.x,
        item.pos.y - nodeRadius * 2.0,
        item.pos.z,
      );
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
    for (const sprite of glowSprites) nodeGroup.remove(sprite);
    for (const line of edgeLines) edgeGroup.remove(line);
    for (const sprite of labelSprites) nodeGroup.remove(sprite);
    nodeMeshes = [];
    glowSprites = [];
    edgeLines = [];
    labelSprites = [];
  }

  // ========================================================================
  // Gyration re-centering animation — plunge into a child node
  // ========================================================================
  function startPlunge(targetNode: RoadmapNode): void {
    const targetItem = layoutItems.find(li => li.node === targetNode);
    if (!targetItem || targetItem.pos.lengthSq() === 0) return;

    const a = targetItem.pos.clone();

    // Compute new positions via gyration transform for all items
    const newPositions: THREE.Vector3[] = layoutItems.map(item => {
      return gyrationTransform3D(item.pos, a);
    });

    // Build animation items
    const animItems: PlungeAnimation3D['items'] = [];
    for (let i = 0; i < layoutItems.length; i++) {
      if (i < nodeMeshes.length) {
        const np = newPositions[i];
        const newR = np.length();
        const currentItem = layoutItems[i];
        const currentR = currentItem.pos.length();
        animItems.push({
          mesh: nodeMeshes[i],
          glowSprite: glowSprites[i],
          label: labelSprites[i],
          startPos: currentItem.pos.clone(),
          endPos: np.clone(),
          startScale: tileScale(currentItem.size, currentR),
          endScale: tileScale(currentItem.size, newR),
        });
      }
    }

    // Edge animation
    const animEdges: PlungeAnimation3D['edges'] = [];
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
          startFrom: layoutItems[parentIdx].pos.clone(),
          startTo: item.pos.clone(),
          endFrom: newPositions[parentIdx].clone(),
          endTo: newPositions[itemIdx].clone(),
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
  // Easing (smooth-step)
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
      const pos = new THREE.Vector3().lerpVectors(ai.startPos, ai.endPos, t);
      ai.mesh.position.copy(pos);
      ai.glowSprite.position.copy(pos);

      // Scale interpolation
      const scale = ai.startScale + (ai.endScale - ai.startScale) * t;
      const nodeRadius = scale;
      ai.label.position.set(pos.x, pos.y - nodeRadius * 2.0, pos.z);
    }

    for (const ae of plungeAnim.edges) {
      const from = new THREE.Vector3().lerpVectors(ae.startFrom, ae.endFrom, t);
      const to = new THREE.Vector3().lerpVectors(ae.startTo, ae.endTo, t);
      const positions = ae.line.geometry.getAttribute('position') as THREE.BufferAttribute;
      positions.setXYZ(0, from.x, from.y, from.z);
      positions.setXYZ(1, to.x, to.y, to.z);
      positions.needsUpdate = true;
    }

    if (plungeAnim.frame >= plungeAnim.totalFrames) {
      plungeAnim = null;
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
    controls.update();
    // Keep point light tracking camera
    pointLight.position.copy(camera.position);
  }

  function handleClick(raycaster: THREE.Raycaster): void {
    const intersects = raycaster.intersectObjects(nodeMeshes, false);
    if (intersects.length === 0) return;

    const hit = intersects[0].object as THREE.Mesh;
    const node = hit.userData.node as RoadmapNode | undefined;
    if (!node) return;

    callbacks.onNodeClick(node);

    // If clicked node has children and is not the focus: plunge into it
    if (node.children && node.children.length > 0 && node !== focusNode) {
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
    (hit.material as THREE.MeshBasicMaterial).color.set(
      brightenColor(hoveredOriginalColor),
    );
    hit.scale.set(1.2, 1.2, 1.2);

    callbacks.onNodeHover(node);
  }

  function dispose(): void {
    controls.dispose();
    for (const geo of allGeometries) geo.dispose();
    for (const mat of allMaterials) mat.dispose();
    for (const tex of allTextures) tex.dispose();
    scene.remove(bgGroup);
    scene.remove(edgeGroup);
    scene.remove(nodeGroup);
    scene.remove(ambientLight);
    scene.remove(pointLight);
  }

  return { update, handleClick, handleHover, dispose, controls };
}
