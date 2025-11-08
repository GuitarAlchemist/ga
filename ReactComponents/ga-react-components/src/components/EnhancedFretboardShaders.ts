/**
 * Enhanced Fretboard Shaders for Professional Rendering
 * Includes: Normal mapping, wood grain, metallic frets, wrapped strings
 */

// Wood texture with normal mapping for depth
export const WOOD_FRAGMENT_SHADER = `
  @group(0) @binding(0) var samplr: sampler;
  @group(0) @binding(1) var albedoTex: texture_2d<f32>;
  @group(0) @binding(2) var normalTex: texture_2d<f32>;
  
  struct Varyings {
    @location(0) uv: vec2<f32>,
    @builtin(position) pos: vec4<f32>
  };
  
  @fragment
  fn main(v: Varyings) -> @location(0) vec4<f32> {
    let albedo = textureSample(albedoTex, samplr, v.uv);
    
    // Sample normal map (or use subtle procedural normal)
    let normalSample = textureSample(normalTex, samplr, v.uv);
    let normal = normalize(normalSample.rgb * 2.0 - 1.0);
    
    // Lighting direction (top-left, slightly forward)
    let lightDir = normalize(vec3(0.3, -0.7, 0.65));
    let viewDir = normalize(vec3(0.0, 0.0, 1.0));
    
    // Diffuse
    let ndotl = max(dot(normal, lightDir), 0.0);
    let diffuse = mix(0.5, 1.0, ndotl);
    
    // Specular (wood has subtle specularity)
    let halfDir = normalize(lightDir + viewDir);
    let spec = pow(max(dot(normal, halfDir), 0.0), 24.0) * 0.15;
    
    // Ambient occlusion simulation (darker at edges)
    let ao = mix(0.7, 1.0, v.uv.y);
    
    let finalColor = albedo.rgb * diffuse * ao + spec;
    return vec4(finalColor, albedo.a);
  }
`;

// Metallic fret shader with crown effect
export const FRET_FRAGMENT_SHADER = `
  struct Varyings {
    @location(0) uv: vec2<f32>,
    @location(1) localPos: vec2<f32>,
    @builtin(position) pos: vec4<f32>
  };
  
  @fragment
  fn main(v: Varyings) -> @location(0) vec4<f32> {
    // Fret crown: raised in center, beveled at edges
    let centerDist = abs(v.localPos.x - 0.5);
    let crown = 1.0 - centerDist * 2.0;
    crown = max(0.0, crown);
    
    // Base fret color (nickel/brass)
    let baseColor = vec3(0.84, 0.84, 0.80);
    
    // Lighting based on crown
    let lightDir = normalize(vec3(0.2, -0.8, 0.6));
    let normal = normalize(vec3(
      -sin(crown * 3.14159) * 0.5,
      0.0,
      cos(crown * 3.14159) * 0.5 + 0.5
    ));
    
    let ndotl = max(dot(normal, lightDir), 0.0);
    let diffuse = mix(0.6, 1.0, ndotl);
    
    // Specular highlight on crown
    let viewDir = normalize(vec3(0.0, 0.0, 1.0));
    let halfDir = normalize(lightDir + viewDir);
    let spec = pow(max(dot(normal, halfDir), 0.0), 64.0) * 0.8;
    
    // Tarnish/aging effect
    let tarnish = mix(0.92, 0.88, crown * 0.3);
    
    let color = baseColor * diffuse * tarnish + spec;
    return vec4(color, 1.0);
  }
`;

// String shader with wrapped wire effect
export const STRING_FRAGMENT_SHADER = `
  struct Varyings {
    @location(0) uv: vec2<f32>,
    @location(1) localPos: vec2<f32>,
    @builtin(position) pos: vec4<f32>
  };
  
  @fragment
  fn main(v: Varyings) -> @location(0) vec4<f32> {
    // String gauge (thicker for wound strings E-A-D)
    let isWound = v.uv.z > 0.5; // Passed as extra data
    
    // Cylindrical shading
    let centerDist = abs(v.localPos.y - 0.5);
    let cylindrical = 1.0 - centerDist * 2.0;
    cylindrical = max(0.0, cylindrical);
    
    // Wrapped wire effect (for wound strings)
    let wrapFreq = 8.0;
    let wrapPattern = sin(v.uv.x * wrapFreq * 6.28) * 0.5 + 0.5;
    let wrapEffect = mix(1.0, wrapPattern, 0.3);
    
    // Base string color (steel/nickel)
    let baseColor = vec3(0.72, 0.72, 0.75);
    
    // Lighting
    let lightDir = normalize(vec3(0.1, -0.9, 0.4));
    let normal = normalize(vec3(
      sin(cylindrical * 3.14159) * 0.7,
      0.0,
      cos(cylindrical * 3.14159) * 0.7 + 0.3
    ));
    
    let ndotl = max(dot(normal, lightDir), 0.0);
    let diffuse = mix(0.5, 1.0, ndotl);
    
    // Specular
    let viewDir = normalize(vec3(0.0, 0.0, 1.0));
    let halfDir = normalize(lightDir + viewDir);
    let spec = pow(max(dot(normal, halfDir), 0.0), 48.0) * 0.6;
    
    let color = baseColor * diffuse * cylindrical * wrapEffect + spec;
    return vec4(color, 0.9);
  }
`;

// Inlay/Pearl shader
export const INLAY_FRAGMENT_SHADER = `
  struct Varyings {
    @location(0) uv: vec2<f32>,
    @builtin(position) pos: vec4<f32>
  };
  
  @fragment
  fn main(v: Varyings) -> @location(0) vec4<f32> {
    // Pearl has iridescent quality
    let dist = length(v.uv - vec2(0.5));
    if (dist > 0.5) { discard; }
    
    // Iridescent effect
    let iridescence = sin(dist * 10.0) * 0.3 + 0.7;
    let pearlColor = vec3(0.95, 0.92, 0.88) * iridescence;
    
    // Lighting
    let lightDir = normalize(vec3(0.3, -0.7, 0.65));
    let normal = normalize(vec3(
      (v.uv.x - 0.5) * 2.0,
      (v.uv.y - 0.5) * 2.0,
      sqrt(max(0.0, 1.0 - dist * dist))
    ));
    
    let ndotl = max(dot(normal, lightDir), 0.0);
    let diffuse = mix(0.7, 1.0, ndotl);
    
    let viewDir = normalize(vec3(0.0, 0.0, 1.0));
    let halfDir = normalize(lightDir + viewDir);
    let spec = pow(max(dot(normal, halfDir), 0.0), 128.0) * 0.9;
    
    let color = pearlColor * diffuse + spec;
    return vec4(color, 0.95);
  }
`;

// Ambient Occlusion for under-string shadows
export const AO_FRAGMENT_SHADER = `
  struct Varyings {
    @location(0) uv: vec2<f32>,
    @builtin(position) pos: vec4<f32>
  };
  
  @fragment
  fn main(v: Varyings) -> @location(0) vec4<f32> {
    // Simple AO: darker near strings and frets
    let ao = 1.0 - (sin(v.uv.x * 20.0) * 0.15 + sin(v.uv.y * 15.0) * 0.1);
    return vec4(vec3(ao), 0.3);
  }
`;

// Helper to create a procedural normal map
export function createProceduralNormalMap(width: number, height: number): Uint8Array {
  const data = new Uint8Array(width * height * 4);
  
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = (y * width + x) * 4;
      
      // Wood grain-like normal variation
      const grain = Math.sin(x * 0.02) * Math.cos(y * 0.01) * 0.5 + 0.5;
      const nx = Math.sin(grain * Math.PI) * 127 + 128;
      const ny = 128;
      const nz = Math.cos(grain * Math.PI) * 127 + 128;
      
      data[idx] = nx;
      data[idx + 1] = ny;
      data[idx + 2] = nz;
      data[idx + 3] = 255;
    }
  }
  
  return data;
}

// Helper to create wrapped string pattern
export function createStringWrapPattern(width: number, height: number, frequency: number = 8): Uint8Array {
  const data = new Uint8Array(width * height * 4);
  
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const idx = (y * width + x) * 4;
      
      // Wrapped wire pattern
      const wrap = Math.sin(x * frequency * Math.PI / width) * 127 + 128;
      
      data[idx] = wrap;
      data[idx + 1] = wrap;
      data[idx + 2] = wrap;
      data[idx + 3] = 255;
    }
  }
  
  return data;
}

