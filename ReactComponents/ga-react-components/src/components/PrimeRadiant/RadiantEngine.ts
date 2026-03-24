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
    this.scene.fog = new THREE.Fog(FOG_COLOR, FOG_NEAR, FOG_FAR);

    // Generate nebula skybox — serves as both background AND PBR reflection source
    this.generateNebulaSkybox();

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

    // Starfield (additional foreground stars for parallax depth)
    this.starfield = createStarfield(800);
    this.scene.add(this.starfield);

    // Health aura
    this.healthAura = createHealthAura(this.graph.globalHealth);
    this.scene.add(this.healthAura);
  }

  // ─── Procedural nebula skybox — background + PBR environment ───
  private generateNebulaSkybox(): void {
    const pmremGenerator = new THREE.PMREMGenerator(this.renderer);
    pmremGenerator.compileEquirectangularShader();

    // Build a full nebula environment scene with procedural shader
    const envScene = new THREE.Scene();
    const envGeo = new THREE.SphereGeometry(200, 64, 64);
    const envMat = new THREE.ShaderMaterial({
      side: THREE.BackSide,
      depthWrite: false,
      uniforms: {
        // Base space colors — deep dark with subtle blue shift
        uDeepSpace:    { value: new THREE.Color(0x020408) },
        uMidSpace:     { value: new THREE.Color(0x0a1020) },
        // Nebula cloud colors — Asimov/Foundation aesthetic
        uNebulaPurple: { value: new THREE.Color(0x2a1040) },
        uNebulaBlue:   { value: new THREE.Color(0x0c1835) },
        uNebulaTeal:   { value: new THREE.Color(0x082828) },
        // Intensity controls — keep dim so graph stays focal
        uNebulaIntensity: { value: 0.35 },
        uStarDensity:     { value: 0.997 },
        uStarBrightness:  { value: 0.8 },
      },
      vertexShader: /* glsl */ `
        varying vec3 vDirection;
        void main() {
          vDirection = normalize(position);
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: /* glsl */ `
        uniform vec3 uDeepSpace;
        uniform vec3 uMidSpace;
        uniform vec3 uNebulaPurple;
        uniform vec3 uNebulaBlue;
        uniform vec3 uNebulaTeal;
        uniform float uNebulaIntensity;
        uniform float uStarDensity;
        uniform float uStarBrightness;

        varying vec3 vDirection;

        // ── Noise functions for nebula generation ──
        // Hash for pseudo-random (fast, GPU-friendly)
        float hash(vec3 p) {
          p = fract(p * vec3(443.8975, 397.2973, 491.1871));
          p += dot(p, p.yxz + 19.19);
          return fract((p.x + p.y) * p.z);
        }

        float hash2(vec2 p) {
          return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
        }

        // 3D value noise
        float noise3d(vec3 p) {
          vec3 i = floor(p);
          vec3 f = fract(p);
          f = f * f * (3.0 - 2.0 * f); // smoothstep

          return mix(
            mix(
              mix(hash(i + vec3(0,0,0)), hash(i + vec3(1,0,0)), f.x),
              mix(hash(i + vec3(0,1,0)), hash(i + vec3(1,1,0)), f.x),
              f.y
            ),
            mix(
              mix(hash(i + vec3(0,0,1)), hash(i + vec3(1,0,1)), f.x),
              mix(hash(i + vec3(0,1,1)), hash(i + vec3(1,1,1)), f.x),
              f.y
            ),
            f.z
          );
        }

        // Fractal Brownian Motion — layered noise for cloud-like structure
        float fbm(vec3 p, int octaves) {
          float value = 0.0;
          float amplitude = 0.5;
          float frequency = 1.0;
          for (int i = 0; i < 6; i++) {
            if (i >= octaves) break;
            value += amplitude * noise3d(p * frequency);
            frequency *= 2.0;
            amplitude *= 0.5;
          }
          return value;
        }

        // ── Star layer ──
        float stars(vec3 dir, float density, float size) {
          // Project direction onto a grid for star placement
          vec3 p = dir * 300.0;
          vec3 cell = floor(p);
          vec3 local = fract(p) - 0.5;

          float star = 0.0;
          // Check neighboring cells for star centers
          for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
              for (int z = -1; z <= 1; z++) {
                vec3 offset = vec3(float(x), float(y), float(z));
                vec3 neighbor = cell + offset;
                float h = hash(neighbor);
                if (h < (1.0 - density)) continue;

                // Star position within cell
                vec3 starPos = offset + vec3(
                  hash(neighbor + 100.0) - 0.5,
                  hash(neighbor + 200.0) - 0.5,
                  hash(neighbor + 300.0) - 0.5
                );

                float d = length(local - starPos);
                float brightness = smoothstep(size, 0.0, d);
                // Color variation — blue-white to warm-white
                float temp = hash(neighbor + 400.0);
                star += brightness * (0.5 + 0.5 * temp);
              }
            }
          }
          return star;
        }

        void main() {
          vec3 dir = normalize(vDirection);

          // ── Base space gradient ──
          float horizon = dir.y;
          vec3 base = mix(uMidSpace, uDeepSpace, smoothstep(-0.3, 0.6, horizon));

          // ── Nebula clouds — multiple FBM layers at different scales ──
          // Large-scale nebula structure
          float n1 = fbm(dir * 2.5 + vec3(42.0, 17.0, 88.0), 5);
          // Medium detail
          float n2 = fbm(dir * 5.0 + vec3(13.0, 77.0, 31.0), 4);
          // Fine wispy detail
          float n3 = fbm(dir * 12.0 + vec3(91.0, 23.0, 67.0), 3);

          // Shape the nebula into cloud-like regions (not uniform)
          float nebulaMask1 = smoothstep(0.35, 0.7, n1);
          float nebulaMask2 = smoothstep(0.4, 0.75, n2);
          float wispMask = smoothstep(0.45, 0.8, n3) * 0.3;

          // Directional bias — nebula concentrated in certain regions
          float regionBias1 = smoothstep(-0.2, 0.5, dot(dir, normalize(vec3(0.6, 0.3, -0.5))));
          float regionBias2 = smoothstep(-0.1, 0.6, dot(dir, normalize(vec3(-0.4, -0.2, 0.7))));
          float regionBias3 = smoothstep(0.0, 0.7, dot(dir, normalize(vec3(0.1, 0.8, 0.3))));

          // Compose colored nebula layers
          vec3 nebula = vec3(0.0);
          nebula += uNebulaPurple * nebulaMask1 * regionBias1 * 1.2;
          nebula += uNebulaBlue * nebulaMask2 * regionBias2 * 1.0;
          nebula += uNebulaTeal * wispMask * regionBias3 * 0.8;

          // Add wispy edge glow where nebula density changes sharply
          float edgeGlow = abs(n1 - 0.5) * 2.0;
          edgeGlow = pow(1.0 - edgeGlow, 4.0) * 0.15;
          nebula += vec3(0.15, 0.1, 0.25) * edgeGlow * nebulaMask1;

          // ── Combine base + nebula ──
          vec3 color = base + nebula * uNebulaIntensity;

          // ── Stars — two layers for depth ──
          // Bright sparse stars
          float brightStars = stars(dir, uStarDensity, 0.08);
          // Dim dense background stars
          float dimStars = stars(dir + vec3(500.0), uStarDensity * 0.998, 0.04);

          // Star color — slight blue-white tint with warm variations
          vec3 starColor1 = mix(
            vec3(0.85, 0.9, 1.0),
            vec3(1.0, 0.95, 0.8),
            hash2(dir.xz * 50.0)
          );
          vec3 starColor2 = vec3(0.7, 0.75, 0.9);

          color += starColor1 * brightStars * uStarBrightness;
          color += starColor2 * dimStars * uStarBrightness * 0.3;

          // Star twinkle in nebula regions (stars behind nebula are dimmed)
          float nebulaDensity = (nebulaMask1 + nebulaMask2) * 0.5;
          color = mix(color, base + nebula * uNebulaIntensity, nebulaDensity * 0.4);

          // ── Final adjustments ──
          // Subtle vignette toward edges for depth
          float vignette = 1.0 - 0.15 * pow(abs(horizon), 2.0);
          color *= vignette;

          // Clamp to prevent blowout
          color = clamp(color, 0.0, 1.0);

          gl_FragColor = vec4(color, 1.0);
        }
      `,
    });
    envScene.add(new THREE.Mesh(envGeo, envMat));

    // Generate PMREM from the nebula scene — this creates both the
    // environment map (for PBR reflections) and the background
    const renderTarget = pmremGenerator.fromScene(envScene, 0);

    // Use as both background and environment (PBR reflection source)
    this.scene.background = renderTarget.texture;
    this.scene.environment = renderTarget.texture;

    // Slightly reduce background intensity so graph stays focal
    // (environment intensity for reflections stays at full)
    this.scene.backgroundIntensity = 0.8;

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
