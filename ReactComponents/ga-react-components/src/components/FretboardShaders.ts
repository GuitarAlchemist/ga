/**
 * Custom shaders for realistic fretboard rendering
 * WebGPU WGSL Shaders for Pixi.js v8
 */

// Vertex shader for frets (metallic appearance)
export const FRET_VERTEX_SHADER = `
struct Uniforms {
  projectionMatrix: mat3x3<f32>,
  translationMatrix: mat3x3<f32>,
  fretColor: vec3<f32>,
  time: f32,
}

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
  @location(0) position: vec2<f32>,
  @location(1) uv: vec2<f32>,
}

struct VertexOutput {
  @builtin(position) position: vec4<f32>,
  @location(0) uv: vec2<f32>,
  @location(1) worldPos: vec2<f32>,
}

@vertex
fn main(input: VertexInput) -> VertexOutput {
  var output: VertexOutput;

  let pos3 = vec3<f32>(input.position, 1.0);
  let transformed = uniforms.projectionMatrix * uniforms.translationMatrix * pos3;

  output.position = vec4<f32>(transformed.xy, 0.0, 1.0);
  output.uv = input.uv;
  output.worldPos = input.position;

  return output;
}
`;

// Fragment shader for frets (metallic with reflections)
export const FRET_FRAGMENT_SHADER = `
struct Uniforms {
  projectionMatrix: mat3x3<f32>,
  translationMatrix: mat3x3<f32>,
  fretColor: vec3<f32>,
  time: f32,
}

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct FragmentInput {
  @location(0) uv: vec2<f32>,
  @location(1) worldPos: vec2<f32>,
}

@fragment
fn main(input: FragmentInput) -> @location(0) vec4<f32> {
  // Create metallic effect with horizontal lines
  let metallic = sin(input.worldPos.y * 50.0) * 0.1 + 0.9;

  // Add subtle reflection/highlight
  let highlight = smoothstep(0.4, 0.6, input.uv.x) * 0.3;

  // Combine colors
  let finalColor = uniforms.fretColor * metallic + vec3<f32>(1.0) * highlight;

  return vec4<f32>(finalColor, 1.0);
}
`;

// Vertex shader for strings (cylindrical appearance)
export const STRING_VERTEX_SHADER = `
struct Uniforms {
  projectionMatrix: mat3x3<f32>,
  translationMatrix: mat3x3<f32>,
  stringColor: vec3<f32>,
  stringThickness: f32,
  time: f32,
}

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
  @location(0) position: vec2<f32>,
  @location(1) uv: vec2<f32>,
}

struct VertexOutput {
  @builtin(position) position: vec4<f32>,
  @location(0) uv: vec2<f32>,
  @location(1) worldPos: vec2<f32>,
}

@vertex
fn main(input: VertexInput) -> VertexOutput {
  var output: VertexOutput;

  let pos3 = vec3<f32>(input.position, 1.0);
  let transformed = uniforms.projectionMatrix * uniforms.translationMatrix * pos3;

  output.position = vec4<f32>(transformed.xy, 0.0, 1.0);
  output.uv = input.uv;
  output.worldPos = input.position;

  return output;
}
`;

// Fragment shader for strings (cylindrical with reflections)
export const STRING_FRAGMENT_SHADER = `
struct Uniforms {
  projectionMatrix: mat3x3<f32>,
  translationMatrix: mat3x3<f32>,
  stringColor: vec3<f32>,
  stringThickness: f32,
  time: f32,
}

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct FragmentInput {
  @location(0) uv: vec2<f32>,
  @location(1) worldPos: vec2<f32>,
}

@fragment
fn main(input: FragmentInput) -> @location(0) vec4<f32> {
  // Create cylindrical appearance with lighting
  let centerDistance = abs(input.uv.y - 0.5) * 2.0;

  // Cylindrical shading (darker at edges, lighter in center)
  let cylindrical = 1.0 - (centerDistance * centerDistance);

  // Add specular highlight (reflection)
  let highlight = exp(-centerDistance * centerDistance * 5.0) * 0.6;

  // Subtle wave effect for string vibration
  let wave = sin(input.worldPos.x * 0.05 + uniforms.time * 2.0) * 0.05;

  // Combine effects
  let finalColor = uniforms.stringColor * (0.7 + cylindrical * 0.3) + vec3<f32>(1.0) * highlight;

  return vec4<f32>(finalColor, 1.0);
}
`;

// Vertex shader for wood texture
export const WOOD_VERTEX_SHADER = `
struct Uniforms {
  projectionMatrix: mat3x3<f32>,
  translationMatrix: mat3x3<f32>,
  woodColor: vec3<f32>,
  time: f32,
}

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct VertexInput {
  @location(0) position: vec2<f32>,
  @location(1) uv: vec2<f32>,
}

struct VertexOutput {
  @builtin(position) position: vec4<f32>,
  @location(0) uv: vec2<f32>,
  @location(1) worldPos: vec2<f32>,
}

@vertex
fn main(input: VertexInput) -> VertexOutput {
  var output: VertexOutput;

  let pos3 = vec3<f32>(input.position, 1.0);
  let transformed = uniforms.projectionMatrix * uniforms.translationMatrix * pos3;

  output.position = vec4<f32>(transformed.xy, 0.0, 1.0);
  output.uv = input.uv;
  output.worldPos = input.position;

  return output;
}
`;

// Fragment shader for wood texture with grain
export const WOOD_FRAGMENT_SHADER = `
struct Uniforms {
  projectionMatrix: mat3x3<f32>,
  translationMatrix: mat3x3<f32>,
  woodColor: vec3<f32>,
  time: f32,
}

@group(0) @binding(0) var<uniform> uniforms: Uniforms;

struct FragmentInput {
  @location(0) uv: vec2<f32>,
  @location(1) worldPos: vec2<f32>,
}

// Pseudo-random function
fn random(st: vec2<f32>) -> f32 {
  return fract(sin(dot(st, vec2<f32>(12.9898, 78.233))) * 43758.5453123);
}

// Perlin-like noise
fn noise(st: vec2<f32>) -> f32 {
  let i = floor(st);
  let f = fract(st);

  let a = random(i);
  let b = random(i + vec2<f32>(1.0, 0.0));
  let c = random(i + vec2<f32>(0.0, 1.0));
  let d = random(i + vec2<f32>(1.0, 1.0));

  let u = f * f * (3.0 - 2.0 * f);

  let ab = mix(a, b, u.x);
  let cd = mix(c, d, u.x);
  return mix(ab, cd, u.y);
}

@fragment
fn main(input: FragmentInput) -> @location(0) vec4<f32> {
  // Create wood grain pattern
  var grain = noise(input.worldPos * 0.01) * 0.5 + 0.5;
  grain += noise(input.worldPos * 0.02) * 0.3;
  grain += noise(input.worldPos * 0.05) * 0.2;

  // Add directional grain lines
  let lines = sin(input.worldPos.y * 0.1) * 0.1 + 0.9;

  // Combine grain and lines
  let woodPattern = grain * lines;

  // Create final wood color with variation
  let finalColor = uniforms.woodColor * (0.8 + woodPattern * 0.2);

  return vec4<f32>(finalColor, 1.0);
}
`;

/**
 * Helper function to convert hex color to RGB vec3
 * Returns normalized RGB values (0-1 range) for use in shaders
 */
export function hexToRgb(hex: number): [number, number, number] {
  const r = ((hex >> 16) & 255) / 255;
  const g = ((hex >> 8) & 255) / 255;
  const b = (hex & 255) / 255;
  return [r, g, b];
}

/**
 * Helper function to create WebGPU shader module
 * Note: This is a simplified helper. In practice, you'd use Pixi.js v8's
 * built-in shader system which handles WebGPU shader compilation.
 */
export function createWebGPUShader(
  device: GPUDevice,
  shaderSource: string,
  label?: string
): GPUShaderModule {
  return device.createShaderModule({
    label: label || 'Fretboard Shader',
    code: shaderSource,
  });
}

