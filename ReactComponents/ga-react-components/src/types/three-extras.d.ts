declare module 'three/examples/jsm/loaders/GLTFLoader' {
  import { Loader, LoadingManager } from 'three';

  export class GLTFLoader extends Loader {
    constructor(manager?: LoadingManager);
    load(
      url: string,
      onLoad: (gltf: { scene: import('three').Group; animations: import('three').AnimationClip[]; scenes: import('three').Group[]; cameras: import('three').Camera[]; asset: Record<string, unknown> }) => void,
      onProgress?: (event: ProgressEvent<EventTarget>) => void,
      onError?: (event: unknown) => void
    ): void;
  }
}

// Minimal shim for @mkkellogg/gaussian-splats-3d (no types ship with the package).
// Covers only the surface we use in VoicingSplatsLayer; expand if you reach for more.
declare module '@mkkellogg/gaussian-splats-3d' {
  import { Group, Object3D } from 'three';

  export enum SceneFormat {
    Ply = 0,
    Splat = 1,
    KSplat = 2,
  }

  export enum SceneRevealMode {
    Default = 0,
    Gradual = 1,
    Instant = 2,
  }

  export enum LogLevel {
    None = 0,
    Error = 1,
    Warning = 2,
    Info = 3,
    Debug = 4,
  }

  export interface AddSplatSceneOptions {
    format?: SceneFormat;
    splatAlphaRemovalThreshold?: number;
    showLoadingUI?: boolean;
    position?: [number, number, number];
    rotation?: [number, number, number, number];
    scale?: [number, number, number];
    progressiveLoad?: boolean;
    onProgress?: (percent: number, percentLabel: string, loaderStatus: number) => void;
  }

  export interface DropInViewerOptions {
    selfDrivenMode?: boolean;
    sharedMemoryForWorkers?: boolean;
    gpuAcceleratedSort?: boolean;
    integerBasedSort?: boolean;
    halfPrecisionCovariancesOnGPU?: boolean;
    sceneRevealMode?: SceneRevealMode;
    enableOptionalEffects?: boolean;
    sphericalHarmonicsDegree?: 0 | 1 | 2;
    logLevel?: LogLevel;
    antialiased?: boolean;
    focalAdjustment?: number;
    inMemoryCompressionLevel?: 0 | 1 | 2;
    freeIntermediateSplatData?: boolean;
    ignoreDevicePixelRatio?: boolean;
    [key: string]: unknown;
  }

  export interface SplatScene extends Object3D {
    opacity: number;
    visible: boolean;
  }

  export class DropInViewer extends Group {
    constructor(options?: DropInViewerOptions);
    addSplatScene(path: string, options?: AddSplatSceneOptions): Promise<void>;
    addSplatScenes(sceneOptions: Array<AddSplatSceneOptions & { path: string }>, showLoadingUI?: boolean): Promise<void>;
    getSplatScene(sceneIndex: number): SplatScene;
    getSceneCount(): number;
    removeSplatScene(index: number, showLoadingUI?: boolean): Promise<void>;
    setActiveSphericalHarmonicsDegrees(activeSphericalHarmonicsDegrees: number): void;
    dispose(): Promise<void>;
    splatMesh: Object3D | null;
  }
}
