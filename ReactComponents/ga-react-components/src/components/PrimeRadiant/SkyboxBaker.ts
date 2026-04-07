// src/components/PrimeRadiant/SkyboxBaker.ts
// Bake the procedural TSL skybox shader to a static CubeTexture so the GPU
// stops re-evaluating noise every frame. The skybox has NO time uniforms —
// it's 100% deterministic — so a one-shot bake at startup is lossless.
//
// Usage in ForceRadiant.tsx after scene setup:
//   import { bakeSkyboxToCubemap } from './SkyboxBaker';
//   const { dispose: disposeBakedSky } = bakeSkyboxToCubemap(
//     fg.renderer(), scene, budgetToTier(qualityBudget)
//   );
//   Call disposeBakedSky() in cleanup.

import * as THREE from 'three';
import { WebGPURenderer } from 'three/webgpu';
import { createSkyboxNebulaMaterialTSL } from './shaders/SkyboxNebulaTSL';
import type { QualityTier } from './shaders/TSLUniforms';

/** Resolution per cube face by quality tier */
const RESOLUTION: Record<QualityTier, number> = {
  low: 512,
  medium: 1024,
  high: 2048,
};

/**
 * Camera orientations for the 6 cube faces.
 * Each entry: [lookAt target, up vector] relative to origin.
 * Order matches THREE.CubeTexture face layout: +X, -X, +Y, -Y, +Z, -Z.
 */
const CUBE_FACES: Array<{ target: THREE.Vector3; up: THREE.Vector3 }> = [
  { target: new THREE.Vector3( 1,  0,  0), up: new THREE.Vector3(0, -1,  0) }, // +X
  { target: new THREE.Vector3(-1,  0,  0), up: new THREE.Vector3(0, -1,  0) }, // -X
  { target: new THREE.Vector3( 0,  1,  0), up: new THREE.Vector3(0,  0,  1) }, // +Y
  { target: new THREE.Vector3( 0, -1,  0), up: new THREE.Vector3(0,  0, -1) }, // -Y
  { target: new THREE.Vector3( 0,  0,  1), up: new THREE.Vector3(0, -1,  0) }, // +Z
  { target: new THREE.Vector3( 0,  0, -1), up: new THREE.Vector3(0, -1,  0) }, // -Z
];

export interface BakeSkyboxResult {
  /** The baked cube texture (can also be assigned to scene.background) */
  cubeTexture: THREE.CubeTexture;
  /** Free GPU resources — call in cleanup */
  dispose: () => void;
}

/**
 * Bake the procedural skybox shader to a CubeTexture, then swap the existing
 * `sky-nebula` mesh material to a cheap cubemap lookup.
 *
 * Works with both WebGPURenderer and WebGLRenderer. Renders 6 face views
 * with a 90-degree FOV camera, reads pixels back, and assembles a CubeTexture.
 */
export function bakeSkyboxToCubemap(
  renderer: THREE.WebGLRenderer | WebGPURenderer,
  scene: THREE.Scene,
  quality: QualityTier,
  resolution?: number,
): BakeSkyboxResult {
  const res = resolution ?? RESOLUTION[quality];

  // ── 1. Build a tiny bake scene with the procedural material ──
  const bakeScene = new THREE.Scene();
  const proceduralMat = createSkyboxNebulaMaterialTSL({ quality });
  const bakeSphere = new THREE.Mesh(
    new THREE.SphereGeometry(10, 32, 32),
    proceduralMat,
  );
  bakeScene.add(bakeSphere);

  // 90-degree FOV, square aspect, camera at origin looking outward
  const bakeCamera = new THREE.PerspectiveCamera(90, 1, 0.1, 100);
  bakeCamera.position.set(0, 0, 0);

  // ── 2. Render 6 faces into a RenderTarget and read pixels ──
  const renderTarget = new THREE.WebGLRenderTarget(res, res, {
    format: THREE.RGBAFormat,
    type: THREE.UnsignedByteType,
    colorSpace: THREE.SRGBColorSpace,
  });

  const faceDataArrays: ImageData[] = [];
  const savedRenderTarget = renderer.getRenderTarget();
  const savedViewport = new THREE.Vector4();
  renderer.getViewport(savedViewport);

  for (const face of CUBE_FACES) {
    bakeCamera.lookAt(face.target);
    bakeCamera.up.copy(face.up);
    bakeCamera.updateMatrixWorld(true);

    renderer.setRenderTarget(renderTarget);
    renderer.setViewport(0, 0, res, res);
    renderer.render(bakeScene, bakeCamera);

    // Read pixels back to CPU
    const buffer = new Uint8Array(res * res * 4);
    renderer.readRenderTargetPixels(renderTarget, 0, 0, res, res, buffer);

    // readRenderTargetPixels gives bottom-up rows; CubeTexture expects top-down.
    // Flip vertically.
    const flipped = new Uint8ClampedArray(res * res * 4);
    const rowBytes = res * 4;
    for (let y = 0; y < res; y++) {
      const srcOffset = (res - 1 - y) * rowBytes;
      const dstOffset = y * rowBytes;
      flipped.set(buffer.subarray(srcOffset, srcOffset + rowBytes), dstOffset);
    }
    faceDataArrays.push(new ImageData(flipped, res, res));
  }

  // Restore renderer state
  renderer.setRenderTarget(savedRenderTarget);
  renderer.setViewport(savedViewport);

  // ── 3. Assemble CubeTexture from the 6 ImageData faces ──
  // CubeTexture expects an array of 6 image-like sources.
  // Convert ImageData to ImageBitmap or use canvas elements.
  const faceCanvases = faceDataArrays.map((imageData) => {
    const canvas = document.createElement('canvas');
    canvas.width = res;
    canvas.height = res;
    const ctx = canvas.getContext('2d')!;
    ctx.putImageData(imageData, 0, 0);
    return canvas;
  });

  const cubeTexture = new THREE.CubeTexture(faceCanvases as unknown as HTMLImageElement[]);
  cubeTexture.needsUpdate = true;
  cubeTexture.colorSpace = THREE.SRGBColorSpace;

  // ── 4. Swap the sky-nebula mesh material ──
  const skyMesh = scene.getObjectByName('sky-nebula') as THREE.Mesh | undefined;
  let oldMaterial: THREE.Material | undefined;

  if (skyMesh) {
    oldMaterial = skyMesh.material as THREE.Material;
    skyMesh.material = new THREE.MeshBasicMaterial({
      envMap: cubeTexture,
      side: THREE.BackSide,
      depthWrite: false,
    });
    oldMaterial.dispose();
  } else {
    console.warn('[SkyboxBaker] sky-nebula mesh not found in scene — cubemap created but not applied');
  }

  // ── 5. Cleanup bake resources ──
  bakeSphere.geometry.dispose();
  proceduralMat.dispose();
  renderTarget.dispose();

  console.info(`[SkyboxBaker] Baked ${quality} skybox to ${res}x${res} cubemap (6 faces)`);

  return {
    cubeTexture,
    dispose: () => {
      cubeTexture.dispose();
      if (skyMesh) {
        (skyMesh.material as THREE.Material).dispose();
      }
      faceCanvases.length = 0;
    },
  };
}
