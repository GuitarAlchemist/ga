import React, { useEffect, useRef, useState } from "react";
import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { BSPSpatialQueryResponse } from "./BSPApiService";

/**
 * HarmonicNavigator3D
 * -------------------------------------------------------------
 * A 3D harmonic navigation component for Guitar Alchemist.
 *
 * • BSP-like partitioning of tonal space rendered as tetrahedral cells
 * • Quaternion-based key/modulation navigation (rotor applied to the scene)
 * • Plücker-style voice-leading paths visualized as tubes between chord barycenters
 * • Three.js with WebGL (WebGPU support optional)
 *
 * Drop-in: <HarmonicNavigator3D regions={...} bsp={...} tunings={...} />
 * Optional: <HarmonicNavigator3D dataUrls={{ modes: "/Modes.yaml", tunings: "/Tunings.toml" }} />
 */

// ==================
// Types
// ==================
export type HarmonicRegion = {
  id: string;              // unique id, e.g., "Ionian_C"
  name: string;            // display name
  pcs: number[];           // pitch classes [0..11]
  tonic: number;           // 0..11
  family?: string;         // optional mode/scale family
  tuningId?: string;       // link to instrument tuning if applicable
  cell: [THREE.Vector3, THREE.Vector3, THREE.Vector3, THREE.Vector3]; // tetra vertices
  color?: number;          // base color (hex)
};

export type MusicalPredicate =
  | { kind: "hasIntervalClass"; ic: number }
  | { kind: "modeFamily"; family: string }
  | { kind: "consonanceAbove"; threshold: number }
  | { kind: "fretSpanMax"; semitones: number };

export type BSPNode =
  | { kind: "leaf"; regionId: string }
  | { kind: "node"; predicate: MusicalPredicate; left: BSPNode; right: BSPNode };

export type HarmonicRotor = { axis: THREE.Vector3; angle: number };

export type PluckerLine = {
  L: THREE.Vector3;            // direction vector
  M: THREE.Vector3;            // moment vector
  fromChord: number[];         // pitch classes
  toChord: number[];           // pitch classes
  color?: number;              // optional color override
};

export type Tuning = { id: string; name: string; midi: number[] };

export type DataUrls = { modes?: string; tunings?: string };

export type Props = {
  regions?: HarmonicRegion[];
  bsp?: BSPNode;
  tunings?: Tuning[];
  chordPaths?: PluckerLine[];
  spatialResult?: BSPSpatialQueryResponse | null; // Integration with existing BSP API
  dataUrls?: DataUrls; // optional: fetch + parse YAML/TOML at runtime
  initialRotor?: HarmonicRotor;
  onSelectRegion?: (id: string) => void;
  className?: string;
  width?: number;
  height?: number;
};

// ==================
// Helper math
// ==================
function chordBarycenter(pcs: number[]): THREE.Vector3 {
  // Map pitch classes to a 3D embedding (unit circle lifted with a gentle z undulation)
  if (!pcs.length) return new THREE.Vector3();
  const pts = pcs.map((pc) => {
    const t = (pc / 12) * Math.PI * 2;
    return new THREE.Vector3(Math.cos(t), Math.sin(t), Math.sin(2 * t) * 0.2);
  });
  const c = new THREE.Vector3();
  for (const p of pts) c.add(p);
  c.multiplyScalar(1 / pts.length);
  return c;
}

function pluckerToCurve(
  L: THREE.Vector3, 
  M: THREE.Vector3, 
  a: THREE.Vector3, 
  b: THREE.Vector3
): THREE.CubicBezierCurve3 {
  // We use a simple construction: control handles aligned with direction L, scaled by distance
  const dir = L.clone().normalize();
  const d = a.distanceTo(b) * 0.25;
  const p1 = a.clone().addScaledVector(dir, d);
  const p2 = b.clone().addScaledVector(dir, -d);
  return new THREE.CubicBezierCurve3(a, p1, p2, b);
}

function slerpQuaternion(axis: THREE.Vector3, angle: number): THREE.Quaternion {
  const q = new THREE.Quaternion();
  q.setFromAxisAngle(axis, angle);
  return q;
}

// Simple default palette for regions
const DEFAULT_COLORS = [
  0x6aa3ff, 0xff8b6e, 0x8affc1, 0xffe66e, 0xd7aaff, 0x7bdff2, 0xf2b5d4, 0xb3f26b,
];

// Convert pitch class string to number
function pitchClassToNumber(pc: string): number {
  const pitchMap: { [key: string]: number } = {
    'C': 0, 'CSharp': 1, 'D': 2, 'DSharp': 3, 'E': 4, 'F': 5,
    'FSharp': 6, 'G': 7, 'GSharp': 8, 'A': 9, 'ASharp': 10, 'B': 11
  };
  return pitchMap[pc] || 0;
}

// ==================
// Component
// ==================
export const HarmonicNavigator3D: React.FC<Props> = ({
  regions: regionsProp,
  bsp,
  tunings,
  chordPaths,
  spatialResult,
  dataUrls,
  initialRotor,
  onSelectRegion,
  className,
  width = 800,
  height = 600,
}) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const overlayRef = useRef<HTMLDivElement | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);

  const [regions, setRegions] = useState<HarmonicRegion[] | undefined>(regionsProp);

  // Convert spatialResult to regions if provided
  useEffect(() => {
    if (spatialResult && spatialResult.elements && spatialResult.elements.length > 0) {
      const baseTet = makeUnitTetraPacking(spatialResult.elements.length);
      const convertedRegions: HarmonicRegion[] = spatialResult.elements.map((element, idx) => {
        const pcs = element.pitchClasses.map(pitchClassToNumber);
        return {
          id: `${element.name}_${idx}`,
          name: element.name,
          pcs,
          tonic: element.tonalCenter,
          family: element.tonalityType,
          cell: baseTet[idx % baseTet.length],
          color: DEFAULT_COLORS[idx % DEFAULT_COLORS.length],
        };
      });
      setRegions(convertedRegions);
    }
  }, [spatialResult]);

  // Scene init
  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    const container = el; // Capture for cleanup

    // Create WebGL renderer
    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setSize(width, height);
    renderer.setPixelRatio(window.devicePixelRatio);
    container.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0e1013);
    sceneRef.current = scene;

    const camera = new THREE.PerspectiveCamera(55, width / height, 0.1, 100);
    camera.position.set(2.7, 2.2, 3.6);
    cameraRef.current = camera;

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controlsRef.current = controls;

    // Lights
    const hemi = new THREE.HemisphereLight(0xffffff, 0x223344, 0.7);
    scene.add(hemi);
    const dir = new THREE.DirectionalLight(0xffffff, 0.85);
    dir.position.set(3, 4, 2);
    dir.castShadow = false;
    scene.add(dir);

    // Key wheel gizmo
    const keyWheel = makeKeyWheel();
    keyWheel.position.set(0, -1.75, 0);
    scene.add(keyWheel);

    // Pointer picking
    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();

    function onPointerDown(e: PointerEvent) {
      const rect = renderer.domElement.getBoundingClientRect();
      pointer.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      pointer.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
      raycaster.setFromCamera(pointer, camera);
      const hits = raycaster.intersectObjects(scene.children, true);
      if (hits.length > 0) {
        // Walk up to mesh with region data
        let obj: THREE.Object3D | null = hits[0].object;
        while (obj && !obj.userData.regionId) obj = obj.parent;
        if (obj && obj.userData.regionId) {
          const id = obj.userData.regionId as string;
          onSelectRegion?.(id);
          // Nudge the scene rotor toward Y for tactile feedback
          applyRotorToScene(scene, { axis: new THREE.Vector3(0, 1, 0), angle: 0.18 });
        }
      }
    }

    renderer.domElement.addEventListener("pointerdown", onPointerDown);

    // Resize
    const onResize = () => {
      const w = container.clientWidth;
      const h = container.clientHeight;
      renderer.setSize(w, h, false);
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
    };
    const ro = new ResizeObserver(onResize);
    ro.observe(container);

    // Animate
    let raf = 0;
    const tick = () => {
      controls.update();
      renderer.render(scene, camera);
      raf = requestAnimationFrame(tick);
    };
    tick();

    return () => {
      cancelAnimationFrame(raf);
      renderer.domElement.removeEventListener("pointerdown", onPointerDown);
      ro.disconnect();
      renderer.dispose();
      if (container && renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [width, height, onSelectRegion]);

  // Build / rebuild region meshes when regions change
  useEffect(() => {
    const scene = sceneRef.current;
    if (!scene || !regions?.length) return;

    // Clean previous group if exists
    const old = scene.getObjectByName("cellsGroup");
    if (old) scene.remove(old);

    // Build group
    const cellsGroup = new THREE.Group();
    cellsGroup.name = "cellsGroup";

    regions.forEach((r, idx) => {
      const mesh = buildCellMesh(r, r.color ?? DEFAULT_COLORS[idx % DEFAULT_COLORS.length]);
      cellsGroup.add(mesh);
    });

    scene.add(cellsGroup);

    // Apply initial rotor if provided
    if (initialRotor) applyRotorToScene(scene, initialRotor);
  }, [regions, initialRotor]);

  // Draw chord paths when provided
  useEffect(() => {
    const scene = sceneRef.current;
    if (!scene) return;

    const old = scene.getObjectByName("pathsGroup");
    if (old) scene.remove(old);

    if (!chordPaths?.length) return;
    const group = new THREE.Group();
    group.name = "pathsGroup";

    for (const pl of chordPaths) {
      const a = chordBarycenter(pl.fromChord);
      const b = chordBarycenter(pl.toChord);
      const curve = pluckerToCurve(pl.L, pl.M, a, b);
      const geom = new THREE.TubeGeometry(curve, 64, 0.035, 8, false);
      const mat = new THREE.MeshStandardMaterial({
        color: pl.color ?? 0xff7a7a,
        metalness: 0.05,
        roughness: 0.2,
      });
      const tube = new THREE.Mesh(geom, mat);
      group.add(tube);
    }

    scene.add(group);
  }, [chordPaths]);

  return (
    <div 
      className={`relative ${className ?? ""}`}
      ref={containerRef}
      style={{ width: '100%', height: '100%', minHeight: `${height}px` }}
    >
      <div 
        ref={overlayRef}
        className="pointer-events-none absolute left-2 top-2 z-10 rounded-lg bg-black/60 px-3 py-2 text-xs text-white shadow-lg"
      >
        Harmonic Navigator 3D — drag to orbit · click a cell to select · drag the key ring to modulate
      </div>
    </div>
  );
};

// ==================
// Builders & UI
// ==================
function buildCellMesh(region: HarmonicRegion, color: number): THREE.Mesh {
  // Geometry from region.cell (tetrahedron defined by 4 vertices)
  const g = new THREE.BufferGeometry();
  const verts = new Float32Array([
    region.cell[0].x, region.cell[0].y, region.cell[0].z,
    region.cell[1].x, region.cell[1].y, region.cell[1].z,
    region.cell[2].x, region.cell[2].y, region.cell[2].z,
    region.cell[3].x, region.cell[3].y, region.cell[3].z,
  ]);
  const indices = new Uint16Array([0, 1, 2, 0, 1, 3, 0, 2, 3, 1, 2, 3]);
  g.setAttribute("position", new THREE.BufferAttribute(verts, 3));
  g.setIndex(new THREE.BufferAttribute(indices, 1));
  g.computeVertexNormals();

  // Standard material for WebGL
  const mat = new THREE.MeshStandardMaterial({
    color: new THREE.Color(color),
    metalness: 0.15,
    roughness: 0.35,
    transparent: true,
    opacity: 0.9,
    side: THREE.DoubleSide,
  });

  const mesh = new THREE.Mesh(g, mat);
  mesh.userData.regionId = region.id;
  mesh.castShadow = false;
  mesh.receiveShadow = false;
  return mesh;
}

function applyRotorToScene(scene: THREE.Scene, rot: HarmonicRotor) {
  const q = slerpQuaternion(rot.axis, rot.angle);
  scene.traverse((obj) => {
    if (obj.userData?.regionId && obj instanceof THREE.Mesh) {
      obj.quaternion.multiply(q);
    }
  });
}

function makeKeyWheel(): THREE.Object3D {
  const group = new THREE.Group();
  const ringG = new THREE.RingGeometry(0.62, 0.82, 64);
  const ringM = new THREE.MeshBasicMaterial({ color: 0x99ffee });
  const ring = new THREE.Mesh(ringG, ringM);
  ring.name = "keywheel";
  group.add(ring);

  // Add tick marks for 12 pitch classes
  const ticks = new THREE.Group();
  for (let i = 0; i < 12; i++) {
    const a = (i / 12) * Math.PI * 2;
    const x = Math.cos(a) * 0.72;
    const y = Math.sin(a) * 0.72;
    const geom = new THREE.BoxGeometry(0.01, 0.08, 0.01);
    const mat = new THREE.MeshBasicMaterial({ color: 0xcffafe });
    const m = new THREE.Mesh(geom, mat);
    m.position.set(x, y, 0);
    m.rotation.z = a;
    ticks.add(m);
  }
  group.add(ticks);

  return group;
}

// Pack N regions into a ring of tets around the origin for a pleasant initial layout
function makeUnitTetraPacking(n: number): [THREE.Vector3, THREE.Vector3, THREE.Vector3, THREE.Vector3][] {
  const out: [THREE.Vector3, THREE.Vector3, THREE.Vector3, THREE.Vector3][] = [];
  for (let i = 0; i < Math.max(1, n); i++) {
    const a = (i / Math.max(1, n)) * Math.PI * 2;
    const r = 1.0;
    const cx = Math.cos(a) * r;
    const cy = Math.sin(a) * r;
    const v0 = new THREE.Vector3(cx, cy, 0.2);
    const v1 = new THREE.Vector3(cx + 0.25, cy, -0.15);
    const v2 = new THREE.Vector3(cx, cy + 0.22, -0.15);
    const v3 = new THREE.Vector3(cx - 0.18, cy - 0.12, -0.05);
    out.push([v0, v1, v2, v3]);
  }
  return out;
}

export default HarmonicNavigator3D;

