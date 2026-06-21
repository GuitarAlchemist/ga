// src/components/PrimeRadiant/VoicingSplatsLayer.ts
//
// Backdrop layer that loads the ix-emitted voicing cloud (Gaussian splats
// from `voicing-cloud.ply`) into the Prime Radiant scene. Wraps Mark Kellogg's
// `@mkkellogg/gaussian-splats-3d` `DropInViewer`, which is a `THREE.Group` that
// drives its own per-frame `viewer.update()` via an `onBeforeRender` hook on a
// hidden callback mesh — meaning the layer plugs straight into 3d-force-graph's
// existing render loop with zero rAF wiring on our side.
//
// Three modes:
//   - 'off'       — Group is invisible; no draw cost.
//   - 'backdrop'  — Translucent, behind the governance graph (renderOrder=-1).
//   - 'solo'      — Full opacity; caller is expected to hide the graph.
//
// Failure mode: load() rejects silently with a console.warn. The rest of the
// Prime Radiant scene is untouched if the PLY is missing (404) or malformed.

import * as THREE from 'three';
import {
  DropInViewer,
  LogLevel,
  SceneFormat,
  SceneRevealMode,
} from '@mkkellogg/gaussian-splats-3d';

export type VoicingSplatsMode = 'off' | 'backdrop' | 'solo';

const BACKDROP_OPACITY = 0.4;
const SOLO_OPACITY = 1.0;

export class VoicingSplatsLayer {
  private readonly scene: THREE.Scene;
  private readonly camera: THREE.Camera;
  private readonly renderer: THREE.WebGLRenderer;
  private viewer: DropInViewer | null = null;
  private mode: VoicingSplatsMode = 'backdrop';
  private loaded = false;
  private disposed = false;

  constructor(scene: THREE.Scene, camera: THREE.Camera, renderer: THREE.WebGLRenderer) {
    this.scene = scene;
    this.camera = camera;
    this.renderer = renderer;
  }

  /**
   * Fetch + decode the PLY and attach the DropInViewer to the scene.
   * Resolves once the splat scene is built and visible. If the URL 404s or the
   * PLY is malformed, logs a warning and resolves anyway — the rest of the
   * scene must continue to render.
   */
  async load(url: string): Promise<void> {
    if (this.disposed) return;
    if (this.viewer) return; // idempotent
    // Probe first — DropInViewer's loader pops a UI spinner on failure which we
    // don't want for a missing optional dataset.
    try {
      const head = await fetch(url, { method: 'HEAD' });
      if (!head.ok) {
        console.warn(
          `[VoicingSplatsLayer] ${url} returned ${head.status}; splats disabled. ` +
          `Generate with: cargo run --release -p ix-voicings --bin fit-splats`,
        );
        return;
      }
    } catch (err) {
      console.warn('[VoicingSplatsLayer] HEAD probe failed; splats disabled.', err);
      return;
    }

    try {
      const viewer = new DropInViewer({
        selfDrivenMode: false,
        sharedMemoryForWorkers: false, // SAB requires COOP/COEP headers we don't ship
        gpuAcceleratedSort: false,     // requires sharedMemoryForWorkers
        sceneRevealMode: SceneRevealMode.Instant,
        enableOptionalEffects: true,   // unlocks per-scene opacity control
        sphericalHarmonicsDegree: 0,   // ix#68 emits SH degree 0 (DC only)
        logLevel: LogLevel.Warning,
        antialiased: true,
        freeIntermediateSplatData: true,
      });
      viewer.renderOrder = -1;
      viewer.name = 'voicing-splats-layer';
      this.viewer = viewer;
      this.scene.add(viewer);

      await viewer.addSplatScene(url, {
        format: SceneFormat.Ply,
        splatAlphaRemovalThreshold: 5,
        showLoadingUI: false,
        progressiveLoad: false,
      });

      if (this.disposed) {
        // Race: caller unmounted during load. Tear down the viewer we just built.
        await this.teardown();
        return;
      }
      this.loaded = true;
      this.applyMode();
    } catch (err) {
      console.warn('[VoicingSplatsLayer] addSplatScene failed; splats disabled.', err);
      await this.teardown();
    }
  }

  setMode(mode: VoicingSplatsMode): void {
    if (this.mode === mode) return;
    this.mode = mode;
    this.applyMode();
  }

  /**
   * Hook for the host rAF loop. Currently a no-op — DropInViewer drives its own
   * update via the callback mesh's `onBeforeRender`. Kept on the surface so the
   * caller's wiring stays stable if we ever switch to a self-managed `Viewer`.
   */
  update(_deltaSec: number): void {
    void _deltaSec;
  }

  async dispose(): Promise<void> {
    this.disposed = true;
    await this.teardown();
  }

  // ---------------------------------------------------------------------------
  // internals
  // ---------------------------------------------------------------------------

  private applyMode(): void {
    const v = this.viewer;
    if (!v) return;
    if (this.mode === 'off') {
      v.visible = false;
      return;
    }
    v.visible = true;
    if (!this.loaded) return;
    const targetOpacity = this.mode === 'solo' ? SOLO_OPACITY : BACKDROP_OPACITY;
    try {
      const count = v.getSceneCount();
      for (let i = 0; i < count; i++) {
        const splatScene = v.getSceneCount() > 0 ? v.getSplatScene(i) : null;
        if (splatScene) {
          splatScene.opacity = targetOpacity;
          splatScene.visible = true;
        }
      }
    } catch (err) {
      // getSplatScene throws before any scene is added — harmless during early
      // race conditions, log once at debug volume only.
      console.debug('[VoicingSplatsLayer] applyMode skipped:', err);
    }
  }

  private async teardown(): Promise<void> {
    const v = this.viewer;
    this.viewer = null;
    this.loaded = false;
    if (!v) return;
    try {
      this.scene.remove(v);
      await v.dispose();
    } catch (err) {
      console.warn('[VoicingSplatsLayer] dispose error (non-fatal):', err);
    }
  }
}

// Re-export the mode union for the call sites that need the literal type.
export const VOICING_SPLATS_MODES: readonly VoicingSplatsMode[] = ['off', 'backdrop', 'solo'] as const;
