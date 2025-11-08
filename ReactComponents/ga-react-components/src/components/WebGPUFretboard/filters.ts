/**
 * WebGPU WGSL Filters for Pixi.js v8
 */
import { Filter } from 'pixi.js';

/**
 * Wood lighting filter (WGSL)
 * Adds directional lighting and specular highlights to wood texture
 */
export function makeLightingFilter(): Filter {
  const fragment = /*wgsl*/`
    @group(0) @binding(0) var s: sampler;
    @group(0) @binding(1) var t: texture_2d<f32>;
    
    struct Varyings {
      @location(0) uv: vec2<f32>,
      @builtin(position) pos: vec4<f32>
    }
    
    @fragment
    fn main(v: Varyings) -> @location(0) vec4<f32> {
      let albedo = textureSample(t, s, v.uv);
      
      // Normal (pointing out of screen)
      let normal = vec3<f32>(0.0, 0.0, 1.0);
      
      // Light direction (from top-left-front)
      let lightDir = normalize(vec3<f32>(0.2, -0.6, 0.77));
      
      // Diffuse lighting
      let ndl = max(dot(normal, lightDir), 0.0);
      
      // Specular highlight
      let viewDir = vec3<f32>(0.0, 0.0, 1.0);
      let halfDir = normalize(lightDir + viewDir);
      let spec = pow(max(dot(halfDir, normal), 0.0), 32.0);
      
      // Combine
      let diffuse = albedo.rgb * (0.65 + 0.35 * ndl);
      let specular = vec3<f32>(0.18) * spec;
      let finalColor = diffuse + specular;
      
      return vec4<f32>(finalColor, albedo.a);
    }
  `;

  return new Filter({
    glProgram: undefined,
    // @ts-ignore - Pixi.js v8 API compatibility issue
    gpuProgram: {
      vertex: {
        source: /*wgsl*/`
          struct Uniforms {
            uProjectionMatrix: mat3x3<f32>,
            uWorldTransformMatrix: mat3x3<f32>,
            uWorldColorAlpha: vec4<f32>,
          }
          
          @group(0) @binding(0) var<uniform> uniforms: Uniforms;
          
          struct Attributes {
            @location(0) aPosition: vec2<f32>,
            @location(1) aUV: vec2<f32>,
          }
          
          struct Varyings {
            @builtin(position) vPosition: vec4<f32>,
            @location(0) vUV: vec2<f32>,
          }
          
          @vertex
          fn main(input: Attributes) -> Varyings {
            var output: Varyings;
            let worldPos = uniforms.uWorldTransformMatrix * vec3<f32>(input.aPosition, 1.0);
            let screenPos = uniforms.uProjectionMatrix * worldPos;
            output.vPosition = vec4<f32>(screenPos.xy, 0.0, 1.0);
            output.vUV = input.aUV;
            return output;
          }
        `,
        entryPoint: 'main',
      },
      fragment: {
        source: fragment,
        entryPoint: 'main',
      },
    },
  });
}

