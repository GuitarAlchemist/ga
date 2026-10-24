import * as THREE from 'three';

// 3d-force-graph bundles its own (stale) Three.js copy unless window.THREE is
// set when its module is evaluated. Prime Radiant uses the project Three.js
// (including the WebGPU NodeMaterial stack), so publish it globally before the
// graph library is imported.
(window as typeof window & { THREE: typeof THREE }).THREE = THREE;
