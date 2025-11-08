// main.ts
import * as THREE from 'three';
import { WebGPURenderer } from 'three/addons/renderers/webgpu/WebGPURenderer.js';
import { buildSampleLevel } from './SampleLevel';
import { collectVisibleCells, locateCell, frustumFromCamera } from './PortalCulling';
import { makePlayer, stepPlayer } from './CapsuleCollision';
import type { Level, Cell } from './LevelLoader';

async function makeRenderer() {
  try {
    const r = new WebGPURenderer({ antialias: true, alpha: false });
    await r.init();
    return r;
  } catch (error) {
    console.warn('WebGPU not available, falling back to WebGL:', error);
    // Fallback WebGL
    const { WebGLRenderer } = await import('three');
    return new WebGLRenderer({ antialias: true, alpha: false }) as unknown as THREE.WebGLRenderer;
  }
}

// Input handling
const keys = {
  w: false, a: false, s: false, d: false,
  ArrowUp: false, ArrowLeft: false, ArrowDown: false, ArrowRight: false,
  Space: false
};

let showCellDebug = false;
let showPortalDebug = false;

// UI elements
const cullingStatus = document.getElementById('culling-status')!;
const visibleCells = document.getElementById('visible-cells')!;
const currentCell = document.getElementById('current-cell')!;
const fpsCounter = document.getElementById('fps')!;

// FPS tracking
let frameCount = 0;
let lastTime = performance.now();

function updateFPS() {
  frameCount++;
  const now = performance.now();
  if (now - lastTime >= 1000) {
    const fps = Math.round((frameCount * 1000) / (now - lastTime));
    fpsCounter.textContent = fps.toString();
    frameCount = 0;
    lastTime = now;
  }
}

(async () => {
  const renderer = await makeRenderer();
  renderer.setSize(window.innerWidth, window.innerHeight);
  document.body.appendChild((renderer as any).domElement ?? (renderer as any).domElement);

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x101014);

  const camera = new THREE.PerspectiveCamera(70, innerWidth / innerHeight, 0.05, 2000);
  camera.position.set(0, 1.7, 3);

  // Lighting
  const light = new THREE.DirectionalLight(0xffffff, 2);
  light.position.set(3, 5, 2);
  scene.add(light, new THREE.AmbientLight(0xffffff, 0.2));

  // Load level - try server first, fallback to sample
  let level;
  try {
    // Try to load from server
    const loader = new CellsPortalsLevelLoader();
    level = await loader.load('http://localhost:5193/scenes/test01.glb');
    console.log('Loaded level from server');
  } catch (error) {
    console.warn('Failed to load from server, using sample level:', error);
    // Fallback to sample level
    level = buildSampleLevel();
  }
  scene.add(level.sceneRoot);

  // Player setup
  const player = makePlayer(new THREE.Vector3(0, 1.0, 0));
  
  // Debug helpers
  const cellHelpers = new Map<string, THREE.Box3Helper>();
  const portalHelpers: THREE.Mesh[] = [];

  // Create cell debug helpers
  for (const [id, cell] of level.cells) {
    const helper = new THREE.Box3Helper(cell.aabb, 0xff00ff);
    helper.visible = showCellDebug;
    cellHelpers.set(id, helper);
    scene.add(helper);
  }

  // Create portal debug helpers
  for (const portal of level.portals) {
    const geometry = new THREE.BufferGeometry().setFromPoints(portal.quad);
    const material = new THREE.MeshBasicMaterial({ 
      color: 0x00ff00, 
      side: THREE.DoubleSide, 
      transparent: true, 
      opacity: 0.3,
      visible: showPortalDebug
    });
    const mesh = new THREE.Mesh(geometry, material);
    portalHelpers.push(mesh);
    scene.add(mesh);
  }

  // Mouse controls for camera
  let mouseX = 0, mouseY = 0;
  let isPointerLocked = false;

  document.addEventListener('click', () => {
    if (!isPointerLocked) {
      document.body.requestPointerLock();
    }
  });

  document.addEventListener('pointerlockchange', () => {
    isPointerLocked = document.pointerLockElement === document.body;
  });

  document.addEventListener('mousemove', (event) => {
    if (isPointerLocked) {
      mouseX += event.movementX * 0.002;
      mouseY += event.movementY * 0.002;
      mouseY = Math.max(-Math.PI/2, Math.min(Math.PI/2, mouseY));
    }
  });

  // Keyboard controls
  document.addEventListener('keydown', (e) => {
    if (e.code in keys) {
      (keys as any)[e.code] = true;
    }
    
    // Toggle debug views
    if (e.code === 'KeyC') {
      showCellDebug = !showCellDebug;
      for (const helper of cellHelpers.values()) {
        helper.visible = showCellDebug;
      }
    }
    
    if (e.code === 'KeyP') {
      showPortalDebug = !showPortalDebug;
      for (const helper of portalHelpers) {
        helper.material.visible = showPortalDebug;
      }
    }
  });

  document.addEventListener('keyup', (e) => {
    if (e.code in keys) {
      (keys as any)[e.code] = false;
    }
  });

  // Render loop
  const clock = new THREE.Clock();
  
  function render() {
    const dt = Math.min(clock.getDelta(), 1/30); // Cap at 30fps for physics stability
    
    // Update camera rotation
    camera.rotation.set(mouseY, mouseX, 0);
    
    // Get input direction in world space
    const inputDir = new THREE.Vector3();
    if (keys.w || keys.ArrowUp) inputDir.z -= 1;
    if (keys.s || keys.ArrowDown) inputDir.z += 1;
    if (keys.a || keys.ArrowLeft) inputDir.x -= 1;
    if (keys.d || keys.ArrowRight) inputDir.x += 1;
    
    // Transform input direction by camera rotation (only Y rotation for FPS-style movement)
    if (inputDir.length() > 0) {
      inputDir.normalize();
      inputDir.applyAxisAngle(new THREE.Vector3(0, 1, 0), mouseX);
    }
    
    // Jump
    if (keys.Space && player.onGround) {
      player.velocity.y = 5.0;
    }
    
    // Find current cell for collision
    const currentCellObj = locateCell(level, player.capsule.start);
    if (currentCellObj) {
      stepPlayer(player, level, currentCellObj, inputDir, dt);
    }
    
    // Update camera position to follow player
    camera.position.copy(player.capsule.end);
    
    // Portal culling
    const visible = collectVisibleCells(level, camera);
    for (const [id, cell] of level.cells) {
      const show = visible.has(cell);
      for (const m of cell.meshes) m.visible = show;
      if (cell.merged) cell.merged.visible = show;
    }
    
    // Update UI
    visibleCells.textContent = visible.size.toString();
    currentCell.textContent = currentCellObj?.id ?? 'None';
    cullingStatus.textContent = visible.size < level.cells.size ? 'Active' : 'Inactive';
    
    updateFPS();
    
    (renderer as any).render(scene, camera);
    requestAnimationFrame(render);
  }
  
  render();

  // Handle window resize
  addEventListener('resize', () => {
    camera.aspect = innerWidth / innerHeight;
    camera.updateProjectionMatrix();
    (renderer as any).setSize(innerWidth, innerHeight);
  });
})();
