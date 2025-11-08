import React, { useEffect, useRef } from "react";
import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { BSPSpatialQueryResponse, BSPElement } from "./BSPApiService";

// --- Types ---
type PitchClass = 0|1|2|3|4|5|6|7|8|9|10|11;
type ChordPoint = { name: string; pcs: PitchClass[]; pos: THREE.Vector3; cost: number; };
type QuaternionMod = { from: THREE.Quaternion; to: THREE.Quaternion; t: number; active: boolean; };

interface ThreeHarmonicNavigatorProps {
  spatialResult?: BSPSpatialQueryResponse | null;
  width?: number;
  height?: number;
}

// --- Helpers ---
function pcToTorus3D(pc: number, costNorm = 0.4): THREE.Vector3 {
  const theta = (pc / 12) * Math.PI * 2;
  const x = Math.cos(theta);
  const y = Math.sin(theta);
  const z = costNorm; // placeholder: consonance/fret cost
  return new THREE.Vector3(x, y, z);
}

function chordCentroid(pcs: PitchClass[], cost = 0.4): THREE.Vector3 {
  const pts = pcs.map(pc => pcToTorus3D(pc, cost));
  const c = new THREE.Vector3();
  pts.forEach(p => c.add(p));
  c.multiplyScalar(1 / pts.length);
  return c.normalize().multiplyScalar(3.0); // push out a bit for spacing
}



// Convert pitch class string to number
function pitchClassToNumber(pc: string): PitchClass {
  const pitchMap: { [key: string]: PitchClass } = {
    'C': 0, 'CSharp': 1, 'D': 2, 'DSharp': 3, 'E': 4, 'F': 5,
    'FSharp': 6, 'G': 7, 'GSharp': 8, 'A': 9, 'ASharp': 10, 'B': 11
  };
  return pitchMap[pc] || 0;
}

// Convert BSPElement to ChordPoint
function bspElementToChordPoint(element: BSPElement, index: number): ChordPoint {
  const pcs = element.pitchClasses.map(pitchClassToNumber) as PitchClass[];
  const cost = 0.3 + (index * 0.1); // Vary cost for visual separation
  return {
    name: element.name,
    pcs,
    pos: chordCentroid(pcs, cost),
    cost
  };
}

// --- Component ---
export const ThreeHarmonicNavigator: React.FC<ThreeHarmonicNavigatorProps> = ({ 
  spatialResult, 
  width = 800, 
  height = 600 
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);

  useEffect(() => {
    if (!containerRef.current) return;

    const container = containerRef.current; // Capture for cleanup

    // Renderer (WebGL - WebGPU support is experimental)
    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setSize(width, height);
    renderer.setPixelRatio(window.devicePixelRatio);
    container.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    // Scene + Camera
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0b0e13);
    sceneRef.current = scene;

    const camera = new THREE.PerspectiveCamera(55, width / height, 0.1, 200);
    camera.position.set(6, 5, 8);
    cameraRef.current = camera;

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controlsRef.current = controls;

    // Lights
    scene.add(new THREE.AmbientLight(0xffffff, 0.35));
    const dir = new THREE.DirectionalLight(0xffffff, 0.9);
    dir.position.set(4, 6, 3);
    scene.add(dir);

    // --- BSP Rooms (pyramids/frusta) ---
    const roomGeom = new THREE.ConeGeometry(6, 8, 5); // pyramid-ish (5 sides)
    const roomMat = new THREE.MeshStandardMaterial({ 
      color: 0x1a2a3a, 
      transparent: true, 
      opacity: 0.18,
      side: THREE.DoubleSide
    });
    const roomMesh = new THREE.Mesh(roomGeom, roomMat);
    roomMesh.position.set(0, -4, 0);
    scene.add(roomMesh);

    // Child room example
    const room2Geom = new THREE.ConeGeometry(5.5, 7.2, 5);
    const room2Mat = new THREE.MeshStandardMaterial({ 
      color: 0x2a1a3a, 
      transparent: true, 
      opacity: 0.14,
      side: THREE.DoubleSide
    });
    const room2Mesh = new THREE.Mesh(room2Geom, room2Mat);
    room2Mesh.position.set(7, -4.2, 0);
    room2Mesh.rotation.y = 0.5;
    scene.add(room2Mesh);

    // --- Chord nodes (pyramids) ---
    const chordMat = new THREE.MeshStandardMaterial({ 
      color: 0x66ffd2, 
      metalness: 0.15,
      roughness: 0.35
    });
    const chordGeom = new THREE.ConeGeometry(0.25, 0.6, 4); // small pyramid
    const chords: ChordPoint[] = [];
    const chordMeshes: THREE.Mesh[] = [];

    // Use data from spatialResult if available, otherwise use defaults
    if (spatialResult && spatialResult.elements && spatialResult.elements.length > 0) {
      spatialResult.elements.forEach((element, index) => {
        const chordPoint = bspElementToChordPoint(element, index);
        chords.push(chordPoint);
        
        const m = new THREE.Mesh(chordGeom, chordMat);
        m.position.copy(chordPoint.pos);
        m.lookAt(new THREE.Vector3(0, 0, 0));
        m.userData = { chord: chordPoint };
        scene.add(m);
        chordMeshes.push(m);
      });
    } else {
      // Default example chords
      const Cmaj: PitchClass[] = [0, 4, 7];
      const Gmaj: PitchClass[] = [7, 11, 2];
      const Amin: PitchClass[] = [9, 0, 4];

      const cNode: ChordPoint = { name: "C", pcs: Cmaj, pos: chordCentroid(Cmaj, 0.3), cost: 0.2 };
      const gNode: ChordPoint = { name: "G", pcs: Gmaj, pos: chordCentroid(Gmaj, 0.45), cost: 0.35 };
      const aNode: ChordPoint = { name: "Am", pcs: Amin, pos: chordCentroid(Amin, 0.55), cost: 0.4 };
      chords.push(cNode, gNode, aNode);

      chords.forEach(cp => {
        const m = new THREE.Mesh(chordGeom, chordMat);
        m.position.copy(cp.pos);
        m.lookAt(new THREE.Vector3(0, 0, 0));
        m.userData = { chord: cp };
        scene.add(m);
        chordMeshes.push(m);
      });
    }

    // --- Transitions (Plücker + render as lines) ---
    const lineMat = new THREE.LineBasicMaterial({ color: 0x88ccff });
    const makeLine = (a: THREE.Vector3, b: THREE.Vector3) => {
      const geo = new THREE.BufferGeometry().setFromPoints([a, b]);
      return new THREE.Line(geo, lineMat);
    };

    // Create lines between consecutive chords
    for (let i = 0; i < chords.length - 1; i++) {
      const line = makeLine(chords[i].pos, chords[i + 1].pos);
      scene.add(line);
    }

    // --- Quaternion Modulation (animate whole cloud) ---
    const mod: QuaternionMod = {
      from: new THREE.Quaternion(),
      to: new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0, 1, 0), Math.PI / 3), // 60° yaw
      t: 0,
      active: false
    };

    function triggerModulation() { 
      mod.t = 0; 
      mod.active = true; 
    }

    // Click room to trigger rotation
    room2Mesh.userData.click = triggerModulation;

    // Simple raycaster for clicks
    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();
    function onPointer(e: MouseEvent) {
      const rect = renderer.domElement.getBoundingClientRect();
      pointer.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      pointer.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
      raycaster.setFromCamera(pointer, camera);
      const hits = raycaster.intersectObjects([roomMesh, room2Mesh, ...chordMeshes], false);
      if (hits.length) {
        const obj = hits[0].object;
        if (obj.userData?.click) obj.userData.click();
      }
    }
    renderer.domElement.addEventListener("pointerdown", onPointer);

    // --- Animate ---
    const cloud = new THREE.Group();
    chordMeshes.forEach(m => cloud.add(m));
    scene.add(cloud);

    const clock = new THREE.Clock();
    let animationId: number;
    
    function animate() {
      const dt = clock.getDelta();

      // quaternion modulation on cloud
      if (mod.active) {
        mod.t = Math.min(1, mod.t + dt * 0.6); // ~1.6s
        const q = new THREE.Quaternion();
        q.slerpQuaternions(mod.from, mod.to, mod.t);
        cloud.setRotationFromQuaternion(q);
        if (mod.t >= 1) mod.active = false;
      }

      controls.update();
      renderer.render(scene, camera);
      animationId = requestAnimationFrame(animate);
    }
    animate();

    // Resize handler
    function onResize() {
      if (!containerRef.current) return;
      const newWidth = containerRef.current.clientWidth;
      const newHeight = containerRef.current.clientHeight;
      camera.aspect = newWidth / newHeight;
      camera.updateProjectionMatrix();
      renderer.setSize(newWidth, newHeight);
    }
    window.addEventListener("resize", onResize);

    // Cleanup
    return () => {
      window.removeEventListener("resize", onResize);
      renderer.domElement.removeEventListener("pointerdown", onPointer);
      cancelAnimationFrame(animationId);
      renderer.dispose();
      if (container && renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, [spatialResult, width, height]);

  return (
    <div 
      ref={containerRef} 
      style={{ 
        width: '100%', 
        height: '100%', 
        minHeight: `${height}px`,
        position: 'relative' 
      }} 
    />
  );
};

export default ThreeHarmonicNavigator;

