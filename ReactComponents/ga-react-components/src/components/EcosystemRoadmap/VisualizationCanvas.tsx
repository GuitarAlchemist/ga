// src/components/EcosystemRoadmap/VisualizationCanvas.tsx

import React, { useCallback, useEffect, useRef } from 'react';
import * as THREE from 'three';
import { useAtom, useAtomValue, useSetAtom } from 'jotai';
import { Box, Typography } from '@mui/material';
import type { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import type { RoadmapNode, RoadmapView, ViewMode } from './types';
import {
  selectedNodeAtom,
  viewModeAtom,
  zoomLevelAtom,
  rendererTypeAtom,
} from './atoms';
import { createIcicleView } from './IcicleView';
import { createPoincareDiskView } from './PoincareDiskView';
import { createPoincareBallView } from './PoincareBallView';
import { clearTextureCache } from './textureUtils';
import { ROADMAP_TREE } from './roadmapData';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------
export interface VisualizationCanvasProps {
  onNodeClick: (node: RoadmapNode) => void;
  onNodeHover: (node: RoadmapNode | null) => void;
}

// ---------------------------------------------------------------------------
// Error boundary
// ---------------------------------------------------------------------------
class VizErrorBoundary extends React.Component<
  { children: React.ReactNode },
  { error: Error | null }
> {
  state = { error: null as Error | null };

  static getDerivedStateFromError(error: Error) {
    return { error };
  }

  render() {
    if (this.state.error) {
      return (
        <Box sx={{ p: 4, textAlign: 'center', color: '#8b949e' }}>
          <Typography>Visualization failed to load.</Typography>
          <Typography variant="caption">{this.state.error.message}</Typography>
        </Box>
      );
    }
    return this.props.children;
  }
}

// ---------------------------------------------------------------------------
// Inner canvas component
// ---------------------------------------------------------------------------
function VisualizationCanvasInner({
  onNodeClick,
  onNodeHover,
}: VisualizationCanvasProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);

  // Jotai atoms
  const viewMode = useAtomValue(viewModeAtom);
  const selectedNode = useAtomValue(selectedNodeAtom);
  const [zoomLevel, setZoomLevel] = useAtom(zoomLevelAtom);
  const setRendererType = useSetAtom(rendererTypeAtom);

  // Mutable refs for animation loop access
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.Camera | null>(null);
  const viewRef = useRef<RoadmapView | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const raycasterRef = useRef(new THREE.Raycaster());
  const mouseRef = useRef(new THREE.Vector2(9999, 9999)); // offscreen initially
  const zoomRef = useRef(zoomLevel);
  const selectedRef = useRef(selectedNode);
  const viewModeRef = useRef<ViewMode>(viewMode);

  // Keep refs in sync
  useEffect(() => {
    zoomRef.current = zoomLevel;
  }, [zoomLevel]);
  useEffect(() => {
    selectedRef.current = selectedNode;
  }, [selectedNode]);

  // Stable callbacks
  const callbacksRef = useRef({ onNodeClick, onNodeHover });
  useEffect(() => {
    callbacksRef.current = { onNodeClick, onNodeHover };
  }, [onNodeClick, onNodeHover]);

  // ------------------------------------------------------------------
  // Create a view for the given mode
  // ------------------------------------------------------------------
  const createView = useCallback(
    (
      mode: ViewMode,
      scene: THREE.Scene,
      renderer: THREE.WebGLRenderer,
      width: number,
      height: number,
    ) => {
      // Dispose previous view + camera
      if (viewRef.current) {
        viewRef.current.dispose();
        viewRef.current = null;
      }
      if (controlsRef.current) {
        controlsRef.current.dispose();
        controlsRef.current = null;
      }
      // Clear scene children (views add groups to scene)
      while (scene.children.length > 0) {
        scene.remove(scene.children[0]);
      }

      const callbacks = {
        onNodeClick: (node: RoadmapNode) => callbacksRef.current.onNodeClick(node),
        onNodeHover: (node: RoadmapNode | null) => callbacksRef.current.onNodeHover(node),
      };

      if (mode === 'icicle') {
        const aspect = width / height;
        const frustumH = 8;
        const frustumW = frustumH * aspect;
        const camera = new THREE.OrthographicCamera(
          -frustumW / 2,
          frustumW / 2,
          frustumH / 2,
          -frustumH / 2,
          0.1,
          100,
        );
        camera.position.set(0, 0, 10);
        camera.lookAt(0, 0, 0);
        cameraRef.current = camera;
        const view = createIcicleView(scene, camera, ROADMAP_TREE, callbacks);
        viewRef.current = view;
      } else if (mode === 'disk') {
        const camera = new THREE.PerspectiveCamera(50, width / height, 0.01, 50);
        camera.position.set(0, 0, 2.2);
        camera.lookAt(0, 0, 0);
        cameraRef.current = camera;
        const view = createPoincareDiskView(scene, camera, ROADMAP_TREE, callbacks);
        viewRef.current = view;
      } else {
        // ball
        const camera = new THREE.PerspectiveCamera(50, width / height, 0.01, 50);
        camera.position.set(0, 0, 3);
        camera.lookAt(0, 0, 0);
        cameraRef.current = camera;
        const result = createPoincareBallView(
          scene,
          camera,
          renderer,
          ROADMAP_TREE,
          callbacks,
        );
        viewRef.current = result;
        controlsRef.current = result.controls;
      }
    },
    [],
  );

  // ------------------------------------------------------------------
  // Initialization effect — renderer + first view
  // ------------------------------------------------------------------
  useEffect(() => {
    const canvas = canvasRef.current;
    const container = containerRef.current;
    if (!canvas || !container) return;

    const width = container.clientWidth;
    const height = container.clientHeight;

    // Create renderer (WebGL — WebGPU attempted via dynamic import below)
    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
    } catch {
      // Fallback: should not normally fail
      renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
    }
    renderer.setClearColor('#0d1117');
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setSize(width, height);
    rendererRef.current = renderer;
    setRendererType('webgl');

    // Attempt WebGPU upgrade (non-blocking)
    (async () => {
      try {
        if (typeof navigator !== 'undefined' && 'gpu' in navigator) {
          // Dynamic import — may fail if three/webgpu is unavailable
          const mod = await import('three/webgpu' as string);
          if (mod && mod.WebGPURenderer) {
            const gpuRenderer = new mod.WebGPURenderer({ canvas, antialias: true });
            await gpuRenderer.init();
            gpuRenderer.setClearColor('#0d1117');
            gpuRenderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
            gpuRenderer.setSize(width, height);
            // Swap renderers
            renderer.dispose();
            rendererRef.current = gpuRenderer as unknown as THREE.WebGLRenderer;
            setRendererType('webgpu');
            // Rebuild current view with new renderer
            if (sceneRef.current) {
              createView(
                viewModeRef.current,
                sceneRef.current,
                rendererRef.current!,
                container.clientWidth,
                container.clientHeight,
              );
            }
          }
        }
      } catch {
        // WebGPU not available; stay on WebGL — no action needed
      }
    })();

    // Scene
    const scene = new THREE.Scene();
    sceneRef.current = scene;

    // Create initial view
    createView(viewModeRef.current, scene, renderer, width, height);

    // Animation loop
    renderer.setAnimationLoop(() => {
      const currentRenderer = rendererRef.current;
      const currentView = viewRef.current;
      const camera = cameraRef.current;
      if (!currentRenderer || !currentView || !camera) return;

      // Update raycaster from mouse
      raycasterRef.current.setFromCamera(mouseRef.current, camera);

      // Update view
      currentView.update(selectedRef.current, zoomRef.current);

      // Render
      currentRenderer.render(scene, camera);
    });

    // ---- Event handlers ----
    const onPointerMove = (e: PointerEvent) => {
      const rect = canvas.getBoundingClientRect();
      mouseRef.current.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      mouseRef.current.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
      if (viewRef.current) {
        raycasterRef.current.setFromCamera(mouseRef.current, cameraRef.current!);
        viewRef.current.handleHover(raycasterRef.current);
      }
    };

    const onClick = (e: MouseEvent) => {
      const rect = canvas.getBoundingClientRect();
      mouseRef.current.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
      mouseRef.current.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
      if (viewRef.current) {
        raycasterRef.current.setFromCamera(mouseRef.current, cameraRef.current!);
        viewRef.current.handleClick(raycasterRef.current);
      }
    };

    const onWheel = (e: WheelEvent) => {
      e.preventDefault();
      const delta = e.deltaY > 0 ? -0.1 : 0.1;
      setZoomLevel((prev) => Math.min(4.0, Math.max(0.25, prev + delta)));
    };

    canvas.addEventListener('pointermove', onPointerMove);
    canvas.addEventListener('click', onClick);
    canvas.addEventListener('wheel', onWheel, { passive: false });

    // ---- Resize observer ----
    const resizeObserver = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const { width: w, height: h } = entry.contentRect;
        if (w === 0 || h === 0) continue;

        const currentRenderer = rendererRef.current;
        if (currentRenderer) {
          currentRenderer.setSize(w, h);
        }

        const camera = cameraRef.current;
        if (camera) {
          if (camera instanceof THREE.PerspectiveCamera) {
            camera.aspect = w / h;
            camera.updateProjectionMatrix();
          } else if (camera instanceof THREE.OrthographicCamera) {
            const aspect = w / h;
            const frustumH = 8;
            const frustumW = frustumH * aspect;
            camera.left = -frustumW / 2;
            camera.right = frustumW / 2;
            camera.top = frustumH / 2;
            camera.bottom = -frustumH / 2;
            camera.updateProjectionMatrix();
          }
        }
      }
    });
    resizeObserver.observe(container);

    // ---- Cleanup ----
    return () => {
      canvas.removeEventListener('pointermove', onPointerMove);
      canvas.removeEventListener('click', onClick);
      canvas.removeEventListener('wheel', onWheel);
      resizeObserver.disconnect();

      renderer.setAnimationLoop(null);

      if (viewRef.current) {
        viewRef.current.dispose();
        viewRef.current = null;
      }
      if (controlsRef.current) {
        controlsRef.current.dispose();
        controlsRef.current = null;
      }

      const currentRenderer = rendererRef.current;
      if (currentRenderer) {
        currentRenderer.dispose();
        rendererRef.current = null;
      }

      clearTextureCache();
      sceneRef.current = null;
      cameraRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ------------------------------------------------------------------
  // React to viewMode changes
  // ------------------------------------------------------------------
  useEffect(() => {
    viewModeRef.current = viewMode;
    const scene = sceneRef.current;
    const renderer = rendererRef.current;
    const container = containerRef.current;
    if (!scene || !renderer || !container) return;

    createView(
      viewMode,
      scene,
      renderer,
      container.clientWidth,
      container.clientHeight,
    );
  }, [viewMode, createView]);

  return (
    <Box
      ref={containerRef}
      sx={{
        width: '100%',
        height: '100%',
        position: 'relative',
        overflow: 'hidden',
      }}
    >
      <canvas
        ref={canvasRef}
        role="img"
        aria-label="Ecosystem roadmap visualization"
        style={{ display: 'block', width: '100%', height: '100%' }}
      />
    </Box>
  );
}

// ---------------------------------------------------------------------------
// Exported component with error boundary
// ---------------------------------------------------------------------------
export default function VisualizationCanvas(props: VisualizationCanvasProps) {
  return (
    <VizErrorBoundary>
      <VisualizationCanvasInner {...props} />
    </VizErrorBoundary>
  );
}
