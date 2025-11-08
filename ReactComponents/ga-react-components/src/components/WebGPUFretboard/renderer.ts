/**
 * WebGPU Renderer setup for Pixi.js v8
 */
import { WebGPURenderer } from 'pixi.js';

export async function createRenderer(canvas: HTMLCanvasElement): Promise<WebGPURenderer> {
  // @ts-ignore - Pixi.js v8 API compatibility issue
  const renderer = new WebGPURenderer({
    canvas,
    backgroundAlpha: 0,
    antialias: true,
    preference: 'high-performance',
  });

  await renderer.init();

  // Handle device loss
  canvas.addEventListener('webgpubackendlost', async () => {
    console.warn('WebGPU backend lost, reinitializing...');
    await renderer.init();
  });

  return renderer;
}

export function handleResize(
  renderer: WebGPURenderer,
  width: number,
  height: number
): void {
  renderer.resize(width, height);
}

