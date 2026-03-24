// src/components/PrimeRadiant/RadiantEngine.ts
// Three.js scene setup, renderer, camera, lights, force layout, animation loop

import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { EffectComposer } from 'three/examples/jsm/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/examples/jsm/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/examples/jsm/postprocessing/UnrealBloomPass.js';
import { SSAOPass } from 'three/examples/jsm/postprocessing/SSAOPass.js';
import { SMAAPass } from 'three/examples/jsm/postprocessing/SMAAPass.js';
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
  FOG_COLOR,
  FOG_NEAR,
  FOG_FAR,
  BLOOM_STRENGTH,
  BLOOM_RADIUS,
  BLOOM_THRESHOLD,
  NODE_SCALES,
} from './types';

import { buildGraphIndex, type GraphIndex } from './DataLoader';
import {
  createNodeMesh,
  createDepartmentParticles,
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
  createStarfield,
  animateStarfield,
} from './HealthOverlay';
import { InteractionHandler } from './InteractionHandler';

// ---------------------------------------------------------------------------
// Force layout constants
// ---------------------------------------------------------------------------
const REPULSION = 80;
const ATTRACTION = 0.008;
const CENTER_GRAVITY = 0.002;
const DAMPING = 0.92;
const LAYOUT_ITERATIONS = 200;
const DT_LAYOUT = 0.5;

// ---------------------------------------------------------------------------
// Initial position seeding by node type (cluster grouping)
// ---------------------------------------------------------------------------
const TYPE_ANGLE: Record<GovernanceNodeType, number> = {
  constitution: 0,
  policy: Math.PI * 0.3,
  persona: Math.PI * 0.7,
  pipeline: Math.PI * 1.0,
  department: Math.PI * 1.3,
  schema: Math.PI * 1.5,
  test: Math.PI * 1.7,
  ixql: Math.PI * 1.9,
};

function seedPosition(node: GovernanceNode, index: number): THREE.Vector3 {
  const angle = TYPE_ANGLE[node.type] + (Math.random() - 0.5) * 0.5;
  const radius = node.type === 'constitution' ? 2 : 10 + Math.random() * 15;
  const y = (Math.random() - 0.5) * 10;
  return new THREE.Vector3(
    Math.cos(angle) * radius + (Math.random() - 0.5) * 3,
    y,
    Math.sin(angle) * radius + (Math.random() - 0.5) * 3,
  );
}

// ---------------------------------------------------------------------------
// RadiantEngine
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
  private healthAura!: THREE.Mesh;
  private starfield!: THREE.Points;

  // Data
  private graph!: GovernanceGraph;
  private graphIndex!: GraphIndex;

  // Interaction
  private interactionHandler!: InteractionHandler;

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
    console.log('[RadiantEngine] init() called with', graph.nodes.length, 'nodes,', graph.edges.length, 'edges');
    this.graph = graph;
    this.graphIndex = buildGraphIndex(graph);

    try {
      console.log('[RadiantEngine] initRenderer...');
      this.initRenderer();
      console.log('[RadiantEngine] initScene...');
      this.initScene();
      console.log('[RadiantEngine] initCamera...');
      this.initCamera();
      console.log('[RadiantEngine] initControls...');
      this.initControls();
      console.log('[RadiantEngine] initPostProcessing...');
      this.initPostProcessing();
      console.log('[RadiantEngine] buildGraph...');
      this.buildGraph();
      console.log('[RadiantEngine] runForceLayout...');
      this.runForceLayout();

      console.log('[RadiantEngine] creating InteractionHandler...');
      this.interactionHandler = new InteractionHandler(
        this.camera,
        this.canvas,
        this.sceneNodes,
        this.sceneEdges,
        this.graphIndex,
        this.onNodeSelect,
      );

      console.log('[RadiantEngine] starting animation loop');
      this.animate();
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
      console.warn('[RadiantEngine] Fell back to WebGLRenderer without antialias');
    }
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.setSize(this.container.clientWidth, this.container.clientHeight);
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.0;
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    console.log('[RadiantEngine] Renderer initialized:', this.renderer.getSize(new THREE.Vector2()));
  }

  // ─── Scene ───
  private initScene(): void {
    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(BACKGROUND_COLOR);
    this.scene.fog = new THREE.Fog(FOG_COLOR, FOG_NEAR, FOG_FAR);

    // Generate procedural environment map for PBR reflections
    this.generateEnvironmentMap();

    // Hemisphere light for subtle ambient fill (sky blue + ground dark)
    const hemiLight = new THREE.HemisphereLight(0x1a2040, 0x080810, 0.6);
    this.scene.add(hemiLight);

    // Ambient for baseline illumination
    const ambient = new THREE.AmbientLight(0x404060, 0.3);
    this.scene.add(ambient);

    // Key light — directional with shadows
    const keyLight = new THREE.DirectionalLight(0xffffff, 1.0);
    keyLight.position.set(20, 30, 20);
    keyLight.castShadow = true;
    keyLight.shadow.mapSize.width = 2048;
    keyLight.shadow.mapSize.height = 2048;
    keyLight.shadow.camera.near = 1;
    keyLight.shadow.camera.far = 100;
    keyLight.shadow.camera.left = -40;
    keyLight.shadow.camera.right = 40;
    keyLight.shadow.camera.top = 40;
    keyLight.shadow.camera.bottom = -40;
    keyLight.shadow.bias = -0.001;
    this.scene.add(keyLight);

    // Fill light — cool blue from opposite side
    const fillLight = new THREE.PointLight(0x58A6FF, 0.8, 120);
    fillLight.position.set(-25, 15, -25);
    this.scene.add(fillLight);

    // Rim/accent light — warm gold from center
    const rimLight = new THREE.PointLight(0xFFD700, 0.5, 100);
    rimLight.position.set(0, -5, 0);
    this.scene.add(rimLight);

    // Third accent — subtle purple for persona highlighting
    const accentLight = new THREE.PointLight(0xC678DD, 0.3, 80);
    accentLight.position.set(15, -10, -20);
    this.scene.add(accentLight);

    // Starfield
    this.starfield = createStarfield();
    this.scene.add(this.starfield);

    // Health aura
    this.healthAura = createHealthAura(this.graph.globalHealth);
    this.scene.add(this.healthAura);
  }

  // ─── Procedural environment map for PBR reflections ───
  private generateEnvironmentMap(): void {
    const pmremGenerator = new THREE.PMREMGenerator(this.renderer);
    pmremGenerator.compileEquirectangularShader();

    // Create a simple gradient environment scene
    const envScene = new THREE.Scene();
    const envGeo = new THREE.SphereGeometry(100, 32, 32);
    const envMat = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      uniforms: {
        topColor: { value: new THREE.Color(0x0a1628) },
        bottomColor: { value: new THREE.Color(0x040810) },
        midColor: { value: new THREE.Color(0x162040) },
      },
      vertexShader: `
        varying vec3 vWorldPosition;
        void main() {
          vec4 worldPos = modelMatrix * vec4(position, 1.0);
          vWorldPosition = worldPos.xyz;
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: `
        uniform vec3 topColor;
        uniform vec3 bottomColor;
        uniform vec3 midColor;
        varying vec3 vWorldPosition;
        void main() {
          float h = normalize(vWorldPosition).y;
          vec3 color = mix(bottomColor, midColor, smoothstep(-1.0, 0.0, h));
          color = mix(color, topColor, smoothstep(0.0, 1.0, h));
          // Add subtle star-like speckles for reflections
          float sparkle = fract(sin(dot(vWorldPosition.xz * 0.1, vec2(12.9898, 78.233))) * 43758.5453);
          color += vec3(0.15, 0.18, 0.25) * step(0.995, sparkle);
          gl_FragColor = vec4(color, 1.0);
        }
      `,
    });
    envScene.add(new THREE.Mesh(envGeo, envMat));

    const envCamera = new THREE.PerspectiveCamera(90, 1, 0.1, 200);
    const renderTarget = pmremGenerator.fromScene(envScene, 0);
    this.scene.environment = renderTarget.texture;

    envGeo.dispose();
    envMat.dispose();
    pmremGenerator.dispose();
  }

  // ─── Camera ───
  private initCamera(): void {
    const aspect = this.container.clientWidth / this.container.clientHeight;
    this.camera = new THREE.PerspectiveCamera(60, aspect, 0.1, 500);
    this.camera.position.set(0, 20, 40);
    this.camera.lookAt(0, 0, 0);
  }

  // ─── Controls ───
  private initControls(): void {
    this.controls = new OrbitControls(this.camera, this.canvas);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.08;
    this.controls.minDistance = 5;
    this.controls.maxDistance = 150;
    this.controls.autoRotate = true;
    this.controls.autoRotateSpeed = 0.2;
  }

  // ─── Post-processing (SSAO + Bloom + SMAA + OutputPass) ───
  private initPostProcessing(): void {
    const w = this.container.clientWidth;
    const h = this.container.clientHeight;

    this.composer = new EffectComposer(this.renderer);

    // 1. Render pass
    const renderPass = new RenderPass(this.scene, this.camera);
    this.composer.addPass(renderPass);

    // 2. SSAO — screen-space ambient occlusion for depth
    const ssaoPass = new SSAOPass(this.scene, this.camera, w, h);
    ssaoPass.kernelRadius = 12;
    ssaoPass.minDistance = 0.001;
    ssaoPass.maxDistance = 0.15;
    ssaoPass.output = SSAOPass.OUTPUT.Default;
    this.composer.addPass(ssaoPass);

    // 3. Bloom — tuned for PBR glow
    const bloomPass = new UnrealBloomPass(
      new THREE.Vector2(w, h),
      BLOOM_STRENGTH,
      BLOOM_RADIUS,
      BLOOM_THRESHOLD,
    );
    this.composer.addPass(bloomPass);

    // 4. SMAA — subpixel morphological anti-aliasing
    const smaaPass = new SMAAPass(w * this.renderer.getPixelRatio(), h * this.renderer.getPixelRatio());
    this.composer.addPass(smaaPass);

    // 5. Output pass — handles tone mapping for post-processing pipeline
    const outputPass = new OutputPass();
    this.composer.addPass(outputPass);
  }

  // ─── Build graph objects ───
  private buildGraph(): void {
    // Create nodes
    for (let i = 0; i < this.graph.nodes.length; i++) {
      const nodeData = this.graph.nodes[i];
      const mesh = createNodeMesh(nodeData);
      const position = seedPosition(nodeData, i);
      mesh.position.copy(position);

      this.scene.add(mesh);

      // Department particle cloud
      if (nodeData.type === 'department') {
        const cloud = createDepartmentParticles(nodeData);
        cloud.position.copy(position);
        this.scene.add(cloud);
      }

      const sceneNode: SceneNode = {
        id: nodeData.id,
        data: nodeData,
        mesh,
        position: position.clone(),
        velocity: new THREE.Vector3(),
        fixed: nodeData.type === 'constitution' && nodeData.id === 'const-asimov',
      };

      this.sceneNodes.set(nodeData.id, sceneNode);

      // LOLLI decay particles for unhealthy nodes
      if (nodeData.health && nodeData.health.lolliCount > 0) {
        const lolliPts = createLolliParticles(position);
        this.scene.add(lolliPts);
        this.lolliParticles.push(lolliPts);
      }
    }

    // Create edges
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

      // Particle streams for pipeline-flow and lolli
      const stream = createEdgeParticles(edgeData, sourceNode.position, targetNode.position);
      if (stream) {
        this.scene.add(stream.points);
        this.particleStreams.push(stream);
      }
    }
  }

  // ─── Force-directed layout ───
  private runForceLayout(): void {
    const nodes = Array.from(this.sceneNodes.values());
    const edges = this.graph.edges;

    for (let iter = 0; iter < LAYOUT_ITERATIONS; iter++) {
      // Repulsion between all node pairs
      for (let i = 0; i < nodes.length; i++) {
        for (let j = i + 1; j < nodes.length; j++) {
          const a = nodes[i];
          const b = nodes[j];
          const diff = new THREE.Vector3().subVectors(a.position, b.position);
          const dist = Math.max(diff.length(), 0.5);
          const force = REPULSION / (dist * dist);
          diff.normalize().multiplyScalar(force * DT_LAYOUT);

          if (!a.fixed) a.velocity.add(diff);
          if (!b.fixed) b.velocity.sub(diff);
        }
      }

      // Attraction along edges
      for (const edge of edges) {
        const a = this.sceneNodes.get(edge.source);
        const b = this.sceneNodes.get(edge.target);
        if (!a || !b) continue;

        const diff = new THREE.Vector3().subVectors(b.position, a.position);
        const dist = diff.length();
        const force = ATTRACTION * dist * (edge.weight ?? 0.5);
        diff.normalize().multiplyScalar(force * DT_LAYOUT);

        if (!a.fixed) a.velocity.add(diff);
        if (!b.fixed) b.velocity.sub(diff);
      }

      // Center gravity
      for (const node of nodes) {
        if (node.fixed) continue;
        const toCenter = new THREE.Vector3().sub(node.position).multiplyScalar(CENTER_GRAVITY);
        node.velocity.add(toCenter);
      }

      // Apply velocities with damping
      for (const node of nodes) {
        if (node.fixed) continue;
        node.velocity.multiplyScalar(DAMPING);
        node.position.add(node.velocity);
        node.mesh.position.copy(node.position);
      }
    }

    // Update edges to final positions
    for (const se of this.sceneEdges.values()) {
      updateEdgeLine(
        se.line,
        se.sourceNode.position,
        se.targetNode.position,
        se.data.type,
      );
    }

    // Update LOLLI particle positions
    let lolliIdx = 0;
    for (const node of this.graph.nodes) {
      if (node.health && node.health.lolliCount > 0) {
        const sn = this.sceneNodes.get(node.id);
        if (sn && lolliIdx < this.lolliParticles.length) {
          this.lolliParticles[lolliIdx].position.copy(sn.position);
          lolliIdx++;
        }
      }
    }
  }

  // ─── Animation loop ───
  private animate = (): void => {
    if (this.disposed) return;
    this.animationId = requestAnimationFrame(this.animate);

    const dt = this.clock.getDelta();
    const time = this.clock.getElapsedTime();

    // Controls
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

    // Hover check
    const hoveredId = this.interactionHandler.checkHover();
    this.onHoverChange?.(hoveredId);

    // Node rotation animation
    for (const sn of this.sceneNodes.values()) {
      const speed = sn.data.type === 'constitution' ? 0.3 : 0.1;
      sn.mesh.rotation.y += dt * speed;
      if (sn.data.type === 'pipeline') {
        sn.mesh.rotation.x += dt * 0.2;
      }
    }

    // Animate particle streams
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

    // Health aura
    animateHealthAura(this.healthAura, time, this.graph.globalHealth);

    // Starfield
    animateStarfield(this.starfield, dt);

    // Render
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

    // Dispose scene objects
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
      if (obj instanceof THREE.Line) {
        obj.geometry?.dispose();
        if (obj.material instanceof THREE.Material) {
          obj.material.dispose();
        }
      }
    });

    this.renderer?.dispose();
    this.composer?.dispose();
    disposeNodeGeometries();

    this.sceneNodes.clear();
    this.sceneEdges.clear();
    this.particleStreams = [];
    this.lolliParticles = [];
  }
}
