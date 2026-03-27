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
