// src/components/PrimeRadiant/shaders/ProceduralMoonTSL.ts
// TSL replacements for the inline GLSL moon shaders in SolarSystem.ts.
// One factory per moon variant; each returns a MeshBasicNodeMaterial that
// auto-compiles to GLSL (WebGL2) or WGSL (WebGPU).
//
// The original GLSL shaders used `vPos = position` (local position). The TSL
// versions use `positionLocal` which is equivalent.

import { MeshBasicNodeMaterial } from 'three/webgpu';
import {
  Fn, float, vec3,
  positionLocal, time,
  sin, cos, fract, mix, abs, smoothstep, max, length,
} from 'three/tsl';
import { noise3, fbm3 } from './TSLNoiseLib';

// ── Shared: hash-based grain for simple rocky/icy surfaces ──
// Mimics: fract(sin(dot(vPos*k, vec3(...)))*43758.5)
const grain = (scale: number, a: number, b: number, c: number) =>
  Fn(() => {
    const p = positionLocal.mul(scale);
    const d = p.x.mul(a).add(p.y.mul(b)).add(p.z.mul(c));
    return fract(sin(d).mul(43758.5));
  })();

// ── ROCKY variants ──

export function createRockyGreyMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const c = grain(40.0, 12.99, 78.23, 45.16);
    return vec3(0.55, 0.53, 0.5).mul(float(0.4).add(c.mul(0.6)));
  })();
  return material;
}

export function createRockyDarkMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const c = grain(50.0, 9.1, 37.2, 71.8);
    return vec3(0.3, 0.28, 0.26).mul(float(0.5).add(c.mul(0.5)));
  })();
  return material;
}

export function createRockyReddishMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const c = grain(35.0, 15.7, 42.3, 8.9);
    return vec3(0.5, 0.35, 0.25).mul(float(0.4).add(c.mul(0.6)));
  })();
  return material;
}

export function createIcyWhiteMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const c = grain(30.0, 11.3, 47.9, 23.1);
    return vec3(0.85, 0.87, 0.9).mul(float(0.7).add(c.mul(0.3)));
  })();
  return material;
}

export function createIcyBlueMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const c = grain(25.0, 7.3, 31.7, 59.1);
    return vec3(0.6, 0.65, 0.75).mul(float(0.6).add(c.mul(0.4)));
  })();
  return material;
}

// ── Io — volcanic sulfur ──
export function createIoMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const p = positionLocal;
    const v = fbm3(p.mul(4.0));
    const spots = smoothstep(0.55, 0.65, noise3(p.mul(8.0).add(vec3(time.mul(0.01), 0, 0))));
    const sulfur = mix(vec3(0.9, 0.85, 0.2), vec3(0.95, 0.6, 0.1), v);
    const lava = vec3(0.1, 0.05, 0.02);
    const base = mix(sulfur, lava, spots);
    const hotspot = smoothstep(0.7, 0.9, noise3(p.mul(12.0))).mul(spots);
    return base.add(vec3(1.0, 0.3, 0.05).mul(hotspot).mul(0.6));
  })();
  return material;
}

// ── Europa — ice cracks ──
export function createEuropaMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const p = positionLocal;
    const ice = vec3(0.82, 0.85, 0.9);
    const crack1 = abs(sin(p.x.mul(15.0).add(p.z.mul(8.0))))
      .mul(abs(cos(p.y.mul(12.0).add(p.x.mul(6.0)))));
    const crack2 = abs(sin(p.z.mul(18.0).add(p.y.mul(10.0))))
      .mul(abs(cos(p.x.mul(14.0).add(p.z.mul(7.0)))));
    const cracks = smoothstep(0.85, 0.95, max(crack1, crack2));
    const base = mix(ice, vec3(0.55, 0.3, 0.15), cracks.mul(0.7));
    const grain20 = noise3(p.mul(20.0));
    return base.add(vec3(0.05, 0.08, 0.12).mul(grain20).mul(0.3));
  })();
  return material;
}

// ── Ganymede — grooved terrain ──
export function createGanymedeMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const p = positionLocal;
    const t = fbm3(p.mul(3.0));
    const dark = vec3(0.3, 0.28, 0.25);
    const light = vec3(0.7, 0.72, 0.75);
    const base = mix(dark, light, smoothstep(0.4, 0.6, t));
    const grooves = sin(p.y.mul(30.0).add(p.x.mul(5.0))).mul(0.5).add(0.5);
    return base.mul(float(0.85).add(grooves.mul(0.15)));
  })();
  return material;
}

// ── Callisto — heavily cratered ──
export function createCallistoMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const p = positionLocal;
    const t = fbm3(p.mul(5.0));
    const craters = smoothstep(0.6, 0.7, noise3(p.mul(10.0)));
    const base = vec3(0.35, 0.32, 0.3).mul(float(0.6).add(t.mul(0.4)));
    return mix(base, vec3(0.55, 0.52, 0.5), craters.mul(0.4));
  })();
  return material;
}

// ── Titan — orange haze ──
export function createTitanMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const p = positionLocal;
    const haze = fbm3(p.mul(2.5).add(vec3(time.mul(0.005), 0, time.mul(0.003))));
    const base = mix(vec3(0.7, 0.5, 0.15), vec3(0.85, 0.65, 0.25), haze);
    const bands = sin(p.y.mul(8.0)).mul(0.5).add(0.5);
    return base.mul(float(0.8).add(bands.mul(0.2)));
  })();
  return material;
}

// ── Enceladus — tiger stripes ──
export function createEnceladusMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const p = positionLocal;
    const ice = vec3(0.92, 0.94, 0.97);
    const lat = p.y.div(length(p));
    const stripes = abs(sin(p.x.mul(20.0).add(p.z.mul(15.0)))).mul(smoothstep(-0.8, -0.5, lat));
    const stripesFiltered = smoothstep(0.85, 0.95, stripes);
    return mix(ice, vec3(0.4, 0.6, 0.85), stripesFiltered.mul(0.5));
  })();
  return material;
}

// ── Mimas — Herschel crater ──
export function createMimasMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const p = positionLocal;
    const c = noise3(p.mul(20.0)).mul(0.3);
    const base = vec3(0.6, 0.58, 0.55).mul(float(0.6).add(c));
    const pl = length(p);
    const px = p.x.div(pl).sub(0.7);
    const py = p.y.div(pl);
    const d = length(vec3(px, py, float(0.0)).xy);
    const crater = float(1.0).sub(smoothstep(0.0, 0.25, d));
    return mix(base, vec3(0.45, 0.42, 0.4), crater.mul(0.5));
  })();
  return material;
}

// ── Iapetus — two-tone ──
export function createIapetusMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const p = positionLocal;
    const side = smoothstep(-0.01, 0.01, p.z);
    const dark = vec3(0.12, 0.1, 0.08);
    const bright = vec3(0.75, 0.73, 0.7);
    return mix(dark, bright, side);
  })();
  return material;
}

// ── Triton — nitrogen ice ──
export function createTritonMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const p = positionLocal;
    const t = fbm3(p.mul(4.0));
    const ice = mix(vec3(0.78, 0.7, 0.72), vec3(0.85, 0.75, 0.78), t);
    const streaks = smoothstep(0.6, 0.8, noise3(vec3(p.x.mul(3.0), p.y.mul(15.0), p.z.mul(3.0))));
    return mix(ice, vec3(0.2, 0.15, 0.12), streaks.mul(0.4));
  })();
  return material;
}

// ── Miranda — chaotic terrain ──
export function createMirandaMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => {
    const p = positionLocal;
    const t = fbm3(p.mul(6.0));
    const chaos = noise3(p.mul(15.0));
    const base = vec3(0.55, 0.53, 0.5).mul(float(0.5).add(t.mul(0.5)));
    const chevron = smoothstep(0.5, 0.7, abs(sin(p.x.mul(10.0).add(p.y.mul(8.0)))));
    return mix(base, vec3(0.7, 0.68, 0.65), chevron.mul(0.3).mul(chaos));
  })();
  return material;
}

// ── Generic grey placeholder for procedural planet fallback ──
export function createProceduralPlaceholderMaterialTSL(): MeshBasicNodeMaterial {
  const material = new MeshBasicNodeMaterial();
  material.colorNode = Fn(() => vec3(0.5, 0.5, 0.5))();
  return material;
}

// ── Lookup map: old GLSL constant name → TSL factory ──
// Used by createMoonMesh to pick the right TSL material based on def.fragment identifier.
export const MOON_TSL_FACTORIES: Record<string, () => MeshBasicNodeMaterial> = {
  ROCKY_GREY: createRockyGreyMaterialTSL,
  ROCKY_DARK: createRockyDarkMaterialTSL,
  ROCKY_REDDISH: createRockyReddishMaterialTSL,
  ICY_WHITE: createIcyWhiteMaterialTSL,
  ICY_BLUE: createIcyBlueMaterialTSL,
  IO_FRAG: createIoMaterialTSL,
  EUROPA_FRAG: createEuropaMaterialTSL,
  GANYMEDE_FRAG: createGanymedeMaterialTSL,
  CALLISTO_FRAG: createCallistoMaterialTSL,
  TITAN_FRAG: createTitanMaterialTSL,
  ENCELADUS_FRAG: createEnceladusMaterialTSL,
  MIMAS_FRAG: createMimasMaterialTSL,
  IAPETUS_FRAG: createIapetusMaterialTSL,
  TRITON_FRAG: createTritonMaterialTSL,
  MIRANDA_FRAG: createMirandaMaterialTSL,
  PROC_PLACEHOLDER: createProceduralPlaceholderMaterialTSL,
};
