// src/components/PrimeRadiant/RadiantEngine.ts
// Foundation TV holographic Prime Radiant — spherical particle visualization
// No solid surfaces — everything is particles, sprites, and light

import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/examples/jsm/postprocessing/OutputPass.js';

import type {
  GovernanceGraph,
  GovernanceNode,
  SceneNode,
  SceneEdge,
  GovernanceNodeType,
} from './types';
import {
  BACKGROUND_COLOR,
  BLOOM_STRENGTH,
  BLOOM_RADIUS,
  BLOOM_THRESHOLD,
  SPHERE_RADIUS_CORE,
  SPHERE_RADIUS_MID,
  SPHERE_RADIUS_OUTER,
} from './types';

import { buildGraphIndex, type GraphIndex } from './DataLoader';
import {
  createNodeMesh,
  createTextSprite,
  animateNodeParticles,
  animateTextSprite,
  createLolliParticles,
  animateLolliParticles,
  disposeNodeGeometries,
} from './NodeRenderer';
import {
  createEdgeLine,
  createEdgeParticles,
  animateEdgeParticles,
  updateEdgeLine,
  type EdgeParticleStream,
} from './EdgeRenderer';
import {
  createHealthAura,
  animateHealthAura,
  createContainmentSphere,
  animateContainmentSphere,
  createStarfield,
  animateStarfield,
} from './HealthOverlay';
import { InteractionHandler } from './InteractionHandler';
import { EquationField } from './EquationField';
import { euclideanToHyperbolic, moebiusTranslation } from './HyperbolicProjection';

// ---------------------------------------------------------------------------
// Spherical layout — hierarchy maps to depth within the sphere
// ---------------------------------------------------------------------------
const SHELL_RADIUS: Record<GovernanceNodeType, number> = {
  constitution: SPHERE_RADIUS_CORE,
  policy: SPHERE_RADIUS_MID,
  persona: SPHERE_RADIUS_MID,
  pipeline: SPHERE_RADIUS_MID,
  department: SPHERE_RADIUS_MID * 0.85,
  schema: SPHERE_RADIUS_OUTER,
  test: SPHERE_RADIUS_OUTER,
  ixql: SPHERE_RADIUS_OUTER,
};

// Angle sectors — cluster same types together on the sphere
const TYPE_SECTOR: Record<GovernanceNodeType, { thetaCenter: number; phiCenter: number; spread: number }> = {
  constitution: { thetaCenter: 0, phiCenter: Math.PI / 2, spread: 0.6 },
  policy: { thetaCenter: Math.PI * 0.4, phiCenter: Math.PI * 0.4, spread: 0.5 },
  persona: { thetaCenter: Math.PI * 0.8, phiCenter: Math.PI * 0.5, spread: 0.4 },
  pipeline: { thetaCenter: Math.PI * 1.2, phiCenter: Math.PI * 0.6, spread: 0.4 },
  department: { thetaCenter: Math.PI * 1.6, phiCenter: Math.PI * 0.45, spread: 0.5 },
  schema: { thetaCenter: Math.PI * 0.3, phiCenter: Math.PI * 0.7, spread: 0.5 },
  test: { thetaCenter: Math.PI * 1.0, phiCenter: Math.PI * 0.3, spread: 0.5 },
  ixql: { thetaCenter: Math.PI * 1.5, phiCenter: Math.PI * 0.7, spread: 0.3 },
};

function seedSphericalPosition(node: GovernanceNode, indexInType: number, totalInType: number): THREE.Vector3 {
  const r = SHELL_RADIUS[node.type];
  const sector = TYPE_SECTOR[node.type];

  // Distribute nodes within their sector using golden-angle spiral
  const goldenAngle = Math.PI * (3 - Math.sqrt(5));
  const t = (indexInType + 0.5) / Math.max(totalInType, 1);

  const theta = sector.thetaCenter + (t * 2 - 1) * sector.spread + goldenAngle * indexInType * 0.1;
  const phi = sector.phiCenter + (Math.cos(indexInType * goldenAngle) * sector.spread * 0.5);

  // Add small random jitter
  const jitter = r * 0.08;

  return new THREE.Vector3(
    r * Math.sin(phi) * Math.cos(theta) + (Math.random() - 0.5) * jitter,
    r * Math.cos(phi) + (Math.random() - 0.5) * jitter,
    r * Math.sin(phi) * Math.sin(theta) + (Math.random() - 0.5) * jitter,
  );
}

// ---------------------------------------------------------------------------
// RadiantEngine — Foundation TV holographic visualization
// ---------------------------------------------------------------------------
export class RadiantEngine {
  // Three.js core
  private renderer!: THREE.WebGLRenderer;
  private scene!: THREE.Scene;
  private camera!: THREE.PerspectiveCamera;
  private controls!: OrbitControls;
  private composer!: EffectComposer;
  private clock = new THREE.Clock();

  // Scene objects
  private sceneNodes = new Map<string, SceneNode>();
  private sceneEdges = new Map<string, SceneEdge>();
  private particleStreams: EdgeParticleStream[] = [];
  private lolliParticles: THREE.Points[] = [];
  private textSprites = new Map<string, THREE.Sprite>();
  private healthAura!: THREE.Mesh;
  private containmentSphere!: THREE.Mesh;
  private starfield!: THREE.Points;
  private equationField!: EquationField;

  // Data
  private graph!: GovernanceGraph;
  private graphIndex!: GraphIndex;

  // Interaction
  private interactionHandler!: InteractionHandler;

  // Hyperbolic (Poincaré ball) state
  private hyperbolicPositions = new Map<string, THREE.Vector3>(); // default positions
  private moebiusFocusId: string | null = null;
  private moebiusBlend = 0;         // 0 = resting, 1 = fully focused
  private moebiusTarget = 0;        // blend target (animate toward this)

  // State
  private animationId: number | null = null;
  private canvas: HTMLCanvasElement;
  private container: HTMLDivElement;
  private disposed = false;
  private onNodeSelect?: (node: GovernanceNode | null) => void;
  private onHoverChange?: (nodeId: string | null) => void;

  constructor(
    canvas: HTMLCanvasElement,
    container: HTMLDivElement,
    onNodeSelect?: (node: GovernanceNode | null) => void,
    onHoverChange?: (nodeId: string | null) => void,
  ) {
    this.canvas = canvas;
    this.container = container;
    this.onNodeSelect = onNodeSelect;
    this.onHoverChange = onHoverChange;
  }

  // ─── Initialize ───
  init(graph: GovernanceGraph): void {
    console.log('[RadiantEngine] init() — Foundation TV mode —', graph.nodes.length, 'nodes,', graph.edges.length, 'edges');
    this.graph = graph;
    this.graphIndex = buildGraphIndex(graph);

    try {
      this.initRenderer();
      this.initScene();
      this.initCamera();
      this.initControls();
      this.initPostProcessing();
      this.buildGraph();

      this.interactionHandler = new InteractionHandler(
        this.camera,
        this.canvas,
        this.sceneNodes,
        this.sceneEdges,
        this.graphIndex,
        (node) => {
          this.onNodeSelect?.(node);
          // Trigger Möbius focus on selection
          if (node) {
            this.moebiusFocusId = node.id;
            this.moebiusTarget = 1;
          } else {
            this.moebiusFocusId = null;
            this.moebiusTarget = 0;
          }
        },
      );

      this.animate();
      console.log('[RadiantEngine] Holographic visualization active');
    } catch (err) {
      console.error('[RadiantEngine] init() failed:', err);
      throw err;
    }
  }

  // ─── Renderer ───
  private initRenderer(): void {
    try {
      this.renderer = new THREE.WebGLRenderer({
        canvas: this.canvas,
        antialias: true,
        alpha: false,
        powerPreference: 'high-performance',
      });
    } catch (err) {
      console.error('[RadiantEngine] WebGLRenderer creation failed:', err);
      this.renderer = new THREE.WebGLRenderer({
        canvas: this.canvas,
        antialias: false,
        alpha: false,
      });
    }
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setSize(this.container.clientWidth, this.container.clientHeight);
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.0;
    // No shadow maps — holographic look has no shadows
    this.renderer.shadowMap.enabled = false;
  }

  // ─── Scene — pure black background, no fog, no skybox ───
  private initScene(): void {
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(BACKGROUND_COLOR);
    // No fog — we want particles to be visible at all distances
    // No environment map — everything emits its own light

    // Minimal ambient for depth cues only
    const ambient = new THREE.AmbientLight(0x101020, 0.1);
    this.scene.add(ambient);

    // Containment sphere (Fresnel holographic globe)
    this.containmentSphere = createContainmentSphere();
    this.scene.add(this.containmentSphere);

    // Starfield
    this.starfield = createStarfield(1200);
    this.scene.add(this.starfield);

    // Health aura
    this.healthAura = createHealthAura(this.graph.globalHealth);
    this.scene.add(this.healthAura);

    // Floating equations — real governance math from live data
    this.equationField = new EquationField(this.scene, this.graph);
  }

  // ─── Camera — zoomed out to see full sphere ───
  private initCamera(): void {
    const aspect = this.container.clientWidth / this.container.clientHeight;
    this.camera = new THREE.PerspectiveCamera(55, aspect, 0.001, 2000);
    this.camera.position.set(0, 15, 55);
    this.camera.lookAt(0, 0, 0);
  }

  // ─── Controls ───
  private initControls(): void {
    this.controls = new OrbitControls(this.camera, this.canvas);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.06;
    this.controls.minDistance = 8;
    this.controls.maxDistance = 120;
    this.controls.autoRotate = true;
    this.controls.autoRotateSpeed = 0.15;
  }

  // ─── Post-processing: high bloom for holographic glow ───
  private initPostProcessing(): void {
    const w = this.container.clientWidth;
    const h = this.container.clientHeight;

    this.composer = new EffectComposer(this.renderer);

    const renderPass = new RenderPass(this.scene, this.camera);
    this.composer.addPass(renderPass);

    // High bloom — the key to the holographic look
    const bloomPass = new UnrealBloomPass(
      new THREE.Vector2(w, h),
      BLOOM_STRENGTH,
      BLOOM_RADIUS,
      BLOOM_THRESHOLD,
    );
    this.composer.addPass(bloomPass);

    // Output pass for tone mapping
    const outputPass = new OutputPass();
    this.composer.addPass(outputPass);
  }

  // ─── Build graph objects — spherical layout ───
  private buildGraph(): void {
    // Count nodes per type for layout distribution
    const typeCounts = new Map<GovernanceNodeType, number>();
    const typeIndices = new Map<GovernanceNodeType, number>();
    for (const n of this.graph.nodes) {
      typeCounts.set(n.type, (typeCounts.get(n.type) ?? 0) + 1);
      typeIndices.set(n.type, 0);
    }

    // Create nodes — positions projected into Poincaré ball
    for (const nodeData of this.graph.nodes) {
      const idx = typeIndices.get(nodeData.type) ?? 0;
      const total = typeCounts.get(nodeData.type) ?? 1;
      typeIndices.set(nodeData.type, idx + 1);

      const mesh = createNodeMesh(nodeData);
      const euclidean = seedSphericalPosition(nodeData, idx, total);
      const position = euclideanToHyperbolic(euclidean);
      mesh.position.copy(position);
      this.scene.add(mesh);

      // Store hyperbolic rest position for Möbius transforms
      this.hyperbolicPositions.set(nodeData.id, position.clone());

      // Text label
      const label = createTextSprite(nodeData.name, nodeData.color);
      label.position.copy(position);
      label.position.y += 2.0;
      this.scene.add(label);
      this.textSprites.set(nodeData.id, label);

      const sceneNode: SceneNode = {
        id: nodeData.id,
        data: nodeData,
        mesh,
        position: position.clone(),
        velocity: new THREE.Vector3(),
        fixed: true, // Spherical layout is fixed — no force simulation
      };
      this.sceneNodes.set(nodeData.id, sceneNode);

      // LOLLI decay particles
      if (nodeData.health && nodeData.health.lolliCount > 0) {
        const lolliPts = createLolliParticles(position);
        this.scene.add(lolliPts);
        this.lolliParticles.push(lolliPts);
      }
    }

    // Create edges as particle streams
    for (const edgeData of this.graph.edges) {
      const sourceNode = this.sceneNodes.get(edgeData.source);
      const targetNode = this.sceneNodes.get(edgeData.target);
      if (!sourceNode || !targetNode) continue;

      const line = createEdgeLine(edgeData, sourceNode.position, targetNode.position);
      this.scene.add(line);

      const sceneEdge: SceneEdge = {
        id: edgeData.id,
        data: edgeData,
        line,
        sourceNode,
        targetNode,
      };
      this.sceneEdges.set(edgeData.id, sceneEdge);

      // Additional particle stream for flowing animation
      const stream = createEdgeParticles(edgeData, sourceNode.position, targetNode.position);
      if (stream) {
        this.scene.add(stream.points);
        this.particleStreams.push(stream);
      }
    }
  }

  // ─── Animation loop ───
  private animate = (): void => {
    if (this.disposed) return;
    this.animationId = requestAnimationFrame(this.animate);

    const dt = this.clock.getDelta();
    const time = this.clock.getElapsedTime();

    this.controls.update();

    // Zoom-to-node animation
    const zoomTarget = this.camera.userData.zoomTarget as THREE.Vector3 | undefined;
    if (zoomTarget) {
      const target = this.controls.target;
      target.lerp(zoomTarget, 0.05);
      if (target.distanceTo(zoomTarget) < 0.1) {
        this.camera.userData.zoomTarget = undefined;
      }
    }

    // ─── Möbius focus animation (Poincaré ball) ───
    const blendSpeed = 2.5; // seconds to full transition
    if (Math.abs(this.moebiusBlend - this.moebiusTarget) > 0.001) {
      this.moebiusBlend += (this.moebiusTarget - this.moebiusBlend) * Math.min(dt * blendSpeed, 1);

      const focusPos = this.moebiusFocusId
        ? this.hyperbolicPositions.get(this.moebiusFocusId) ?? null
        : null;

      for (const [nodeId, sn] of this.sceneNodes) {
        const restPos = this.hyperbolicPositions.get(nodeId);
        if (!restPos) continue;

        let targetPos: THREE.Vector3;
        if (focusPos && this.moebiusBlend > 0.001) {
          const transformed = moebiusTranslation(restPos, focusPos);
          targetPos = new THREE.Vector3().lerpVectors(restPos, transformed, this.moebiusBlend);
        } else {
          targetPos = restPos;
        }

        // Smoothly move mesh and update position reference
        sn.mesh.position.lerp(targetPos, 0.12);
        sn.position.copy(sn.mesh.position);

        // Update label
        const label = this.textSprites.get(nodeId);
        if (label) {
          label.position.copy(sn.mesh.position);
          label.position.y += 2.0;
        }
      }

      // Update edge lines
      for (const se of this.sceneEdges.values()) {
        updateEdgeLine(se.line, se.sourceNode.position, se.targetNode.position, se.data.type);
      }
    }

    // Hover check
    const hoveredId = this.interactionHandler.checkHover();
    this.onHoverChange?.(hoveredId);

    // Animate node particle clusters (Brownian drift)
    for (const sn of this.sceneNodes.values()) {
      animateNodeParticles(sn.mesh as THREE.Group, time, dt);
    }

    // Animate text labels (fade with distance)
    for (const [, sprite] of this.textSprites) {
      animateTextSprite(sprite, this.camera.position, 30, 55);
    }

    // Animate edge particle streams
    for (const stream of this.particleStreams) {
      const se = this.sceneEdges.get(stream.edge.id);
      if (se) {
        animateEdgeParticles(stream, se.sourceNode.position, se.targetNode.position, dt);
      }
    }

    // Animate LOLLI particles
    for (const pts of this.lolliParticles) {
      animateLolliParticles(pts, dt);
    }

    // Containment sphere
    animateContainmentSphere(this.containmentSphere, time);

    // Health aura
    animateHealthAura(this.healthAura, time, this.graph.globalHealth);

    // Starfield — follow camera so stars always surround the viewer
    animateStarfield(this.starfield, dt, this.camera);

    // Floating equations
    this.equationField?.update(dt);

    // Render with bloom
    this.composer.render();
  };

  // ─── Resize ───
  resize(): void {
    if (this.disposed) return;
    const w = this.container.clientWidth;
    const h = this.container.clientHeight;
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(w, h);
    this.composer.setSize(w, h);
  }

  // ─── Focus on a specific node ───
  focusOnNode(nodeId: string): void {
    this.interactionHandler.focusOnNode(nodeId);
  }

  // ─── Dispose ───
  dispose(): void {
    this.disposed = true;
    if (this.animationId !== null) {
      cancelAnimationFrame(this.animationId);
    }
    this.interactionHandler?.dispose();
    this.controls?.dispose();

    this.scene?.traverse((obj) => {
      if (obj instanceof THREE.Mesh) {
        obj.geometry?.dispose();
        if (obj.material instanceof THREE.Material) {
          obj.material.dispose();
        }
      }
      if (obj instanceof THREE.Points) {
        obj.geometry?.dispose();
        if (obj.material instanceof THREE.Material) {
          obj.material.dispose();
        }
      }
      if (obj instanceof THREE.Sprite) {
        (obj.material as THREE.SpriteMaterial).map?.dispose();
        obj.material.dispose();
      }
    });

    this.equationField?.dispose();
    this.renderer?.dispose();
    this.composer?.dispose();
    disposeNodeGeometries();

    this.sceneNodes.clear();
    this.sceneEdges.clear();
    this.textSprites.clear();
    this.particleStreams = [];
    this.lolliParticles = [];
  }
}
