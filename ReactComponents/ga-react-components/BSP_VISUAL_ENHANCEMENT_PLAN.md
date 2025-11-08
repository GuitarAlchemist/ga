# BSP DOOM Explorer - Visual Enhancement Plan

> **Vision**: Transform the BSP DOOM Explorer from functional to atmospheric while maintaining scientific readability of tonal "rooms." Keep geometry simple, but make the light alive.

## Current State Analysis

### Strengths
- ✅ Solid WebGPU/WebGL rendering foundation
- ✅ PBR materials with metalness/roughness
- ✅ Basic lighting (ambient + directional + point lights)
- ✅ Environment mapping for reflections
- ✅ Fog for depth
- ✅ Starfield background
- ✅ Gradient skybox
- ✅ Procedural wood textures
- ✅ Shader-based sand terrain

### Areas for Enhancement
- ⚠️ Flat, sterile lighting (no depth or shadows)
- ⚠️ Uniform fog (no tonal family variation)
- ⚠️ Static lighting (no dynamic response to chords)
- ⚠️ Pure green UI (lacks retro CRT polish)
- ⚠️ Black void skybox (could be more atmospheric)
- ⚠️ Abrupt floor transitions (no smooth animations)
- ⚠️ No spatial cues (floor grids, depth markers)
- ⚠️ No harmonic visualization (particle trails, waveforms)

---

## Phase 1: Lighting & Atmosphere Enhancement

### 1.1 Ambient Occlusion & Soft Shadows
**Goal**: Add depth and anchor floating elements visually

**Implementation**:
```typescript
// Option A: Fake SSAO via shader (lightweight)
const ssaoShader = new THREE.ShaderMaterial({
  uniforms: {
    tDiffuse: { value: null },
    tDepth: { value: null },
    cameraNear: { value: camera.near },
    cameraFar: { value: camera.far },
    aoIntensity: { value: 0.5 },
    aoRadius: { value: 5.0 }
  },
  // ... SSAO shader code
});

// Option B: Baked contact shadows under platforms
const createContactShadow = (position: THREE.Vector3, radius: number) => {
  const shadowGeometry = new THREE.CircleGeometry(radius, 32);
  const shadowMaterial = new THREE.MeshBasicMaterial({
    color: 0x000000,
    transparent: true,
    opacity: 0.3,
    depthWrite: false
  });
  const shadow = new THREE.Mesh(shadowGeometry, shadowMaterial);
  shadow.rotation.x = -Math.PI / 2;
  shadow.position.copy(position);
  shadow.position.y = 0.01; // Just above floor
  return shadow;
};
```

**Performance**: Baked shadows preferred (no runtime cost)

### 1.2 Volumetric Fog with Tonal Family Colors
**Goal**: Spatial separation via colored fog gradients

**Implementation**:
```typescript
// Tonal family fog colors
const TONAL_FOG_COLORS: Record<string, THREE.Color> = {
  'G-Family': new THREE.Color(0xff9955), // Warm amber
  'C-Family': new THREE.Color(0x5599ff), // Cool cyan
  'D-Family': new THREE.Color(0x99ff55), // Fresh green
  'A-Family': new THREE.Color(0xff5599), // Warm magenta
  'E-Family': new THREE.Color(0x55ff99), // Cool mint
  'B-Family': new THREE.Color(0x9955ff), // Deep purple
  'F-Family': new THREE.Color(0xffff55), // Bright yellow
};

// Dynamic fog color based on current region
const updateFogColor = (region: BSPRegion) => {
  const targetColor = TONAL_FOG_COLORS[region.name] || new THREE.Color(0x2a1f1a);
  
  // Smooth color transition (lerp over time)
  if (scene.fog instanceof THREE.FogExp2) {
    const currentColor = scene.fog.color;
    currentColor.lerp(targetColor, 0.05); // Smooth transition
  }
};
```

**Visual Impact**: Each tonal family has its own atmospheric "mood"

### 1.3 Dynamic Light Response to Chord Intensity
**Goal**: Lights pulse/flicker based on harmonic tension

**Implementation**:
```typescript
// Calculate chord tension (0-1 scale)
const calculateChordTension = (pitchClasses: number[]): number => {
  // Simple heuristic: more chromatic = more tension
  const uniquePitches = new Set(pitchClasses);
  const chromaticDensity = uniquePitches.size / 12;
  
  // Interval dissonance (tritones, minor 2nds = high tension)
  let dissonanceScore = 0;
  for (let i = 0; i < pitchClasses.length; i++) {
    for (let j = i + 1; j < pitchClasses.length; j++) {
      const interval = Math.abs(pitchClasses[i] - pitchClasses[j]) % 12;
      if (interval === 1 || interval === 6) dissonanceScore += 1; // m2 or tritone
    }
  }
  
  return Math.min(1, (chromaticDensity + dissonanceScore / 10));
};

// Apply to lights in animation loop
const updateDynamicLighting = (time: number, tension: number) => {
  // Torch light flicker (faster with higher tension)
  const flickerSpeed = 2.0 + tension * 3.0;
  const flickerAmount = 0.3 + tension * 0.5;
  torchLight.intensity = 2.5 + Math.sin(time * flickerSpeed) * flickerAmount;
  
  // Alchemy glow pulse (slower, deeper)
  const pulseSpeed = 1.0 + tension * 2.0;
  alchemyGlow.intensity = 1.2 + Math.sin(time * pulseSpeed) * 0.4;
  
  // Accent lights color shift based on tension
  const tensionColor = new THREE.Color().lerpColors(
    new THREE.Color(0x5599ff), // Low tension (cool blue)
    new THREE.Color(0xff5555), // High tension (hot red)
    tension
  );
  accentLight1.color.copy(tensionColor);
};
```

**Visual Impact**: The world "breathes" with the music

---

## Phase 2: Material Variation & Visual Depth

### 2.1 Reflective Floors with Roughness Maps
**Goal**: Imply structure without realism overload

**Implementation**:
```typescript
// Enhanced floor material with subtle reflections
const createReflectiveFloorMaterial = (baseColor: number, roughness: number = 0.7) => {
  return new THREE.MeshStandardMaterial({
    color: baseColor,
    roughness: roughness,
    metalness: 0.3, // Brushed metal / tempered glass
    envMapIntensity: 0.5, // Subtle reflections
    emissive: baseColor,
    emissiveIntensity: 0.05,
  });
};

// Procedural roughness map for variation
const createRoughnessMap = (): THREE.Texture => {
  const canvas = document.createElement('canvas');
  canvas.width = 512;
  canvas.height = 512;
  const ctx = canvas.getContext('2d')!;
  
  // Noise-based roughness variation
  for (let x = 0; x < canvas.width; x++) {
    for (let y = 0; y < canvas.height; y++) {
      const noise = Math.random() * 0.3 + 0.7; // 0.7-1.0 range
      const gray = Math.floor(noise * 255);
      ctx.fillStyle = `rgb(${gray}, ${gray}, ${gray})`;
      ctx.fillRect(x, y, 1, 1);
    }
  }
  
  const texture = new THREE.CanvasTexture(canvas);
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  return texture;
};
```

### 2.2 Emissive Edges for Active BSP Partitions
**Goal**: Highlight active partitions with glowing outlines

**Implementation**:
```typescript
// Enhanced partition material with emissive pulse
const createPartitionMaterial = (color: number, isActive: boolean = false) => {
  return new THREE.MeshStandardMaterial({
    color: color,
    transparent: true,
    opacity: isActive ? 0.5 : 0.35,
    side: THREE.DoubleSide,
    emissive: color,
    emissiveIntensity: isActive ? 1.2 : 0.7, // Brighter when active
    metalness: 0.6,
    roughness: 0.3,
  });
};

// Pulse animation for visited partitions
const animatePartitionPulse = (partition: THREE.Mesh, time: number) => {
  const material = partition.material as THREE.MeshStandardMaterial;
  const baseIntensity = 0.7;
  const pulseAmount = 0.5;
  material.emissiveIntensity = baseIntensity + Math.sin(time * 2.0) * pulseAmount;
};
```

### 2.3 Transparent Resin-like Volumes for Chords
**Goal**: Encode interval density via opacity

**Implementation**:
```typescript
// Chord volume with opacity based on interval density
const createChordVolume = (pitchClasses: number[], color: number) => {
  const intervalDensity = pitchClasses.length / 12; // 0-1 scale
  
  const geometry = new THREE.BoxGeometry(4, 4, 4);
  const material = new THREE.MeshStandardMaterial({
    color: color,
    transparent: true,
    opacity: 0.3 + intervalDensity * 0.4, // More notes = more opaque
    emissive: color,
    emissiveIntensity: 0.6,
    metalness: 0.2,
    roughness: 0.4,
    envMapIntensity: 1.5, // Resin-like reflections
  });
  
  return new THREE.Mesh(geometry, material);
};
```

---

## Phase 3: HUD & Interface Polish

### 3.1 Retro CRT Aesthetic
**Goal**: Blend retro with modern, evoke CRT displays

**Implementation**:
```typescript
// CRT-style HUD with phosphor glow
const hudStyle = {
  backgroundColor: 'rgba(0, 0, 0, 0.85)', // Darker for contrast
  border: '2px solid #0f0',
  boxShadow: '0 0 20px rgba(0, 255, 0, 0.3), inset 0 0 10px rgba(0, 255, 0, 0.1)',
  fontFamily: '"Courier New", monospace',
  color: '#0f0',
  textShadow: '0 0 5px #0f0, 0 0 10px #0f0',
  
  // Scanline overlay (CSS)
  '&::before': {
    content: '""',
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    background: 'repeating-linear-gradient(0deg, rgba(0, 0, 0, 0.15), rgba(0, 0, 0, 0.15) 1px, transparent 1px, transparent 2px)',
    pointerEvents: 'none',
  }
};

// Gradient text for variety (lime-yellow)
const gradientText = {
  background: 'linear-gradient(90deg, #0f0 0%, #ff0 100%)',
  WebkitBackgroundClip: 'text',
  WebkitTextFillColor: 'transparent',
};
```

### 3.2 Minimap with Depth & Glow
**Goal**: Embedded feel, not pasted

**Implementation**:
```typescript
// Enhanced minimap with parallax depth
const minimapStyle = {
  backgroundColor: 'rgba(0, 0, 0, 0.9)',
  border: '2px solid #0f0',
  boxShadow: '0 0 20px rgba(0, 255, 0, 0.5), inset 0 0 20px rgba(0, 255, 0, 0.2)',
  
  // Grid overlay for depth
  backgroundImage: 'linear-gradient(rgba(0, 255, 0, 0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(0, 255, 0, 0.1) 1px, transparent 1px)',
  backgroundSize: '20px 20px',
};
```

---

## Phase 4: Spatial Cues & Navigation

### 4.1 Low-Contrast Floor Grids
**Goal**: Reinforce navigation scale (Portal-style)

**Implementation**:
```typescript
// Subtle grid that fades with depth
const createDepthFadingGrid = (size: number = 100, divisions: number = 20) => {
  const gridHelper = new THREE.GridHelper(size, divisions, 0x444466, 0x222233);
  
  // Custom shader for depth-based fading
  const gridMaterial = new THREE.ShaderMaterial({
    uniforms: {
      cameraPosition: { value: camera.position },
      fadeDistance: { value: 50.0 },
      gridColor: { value: new THREE.Color(0x444466) },
    },
    vertexShader: `
      varying vec3 vWorldPosition;
      void main() {
        vWorldPosition = (modelMatrix * vec4(position, 1.0)).xyz;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    fragmentShader: `
      uniform vec3 cameraPosition;
      uniform float fadeDistance;
      uniform vec3 gridColor;
      varying vec3 vWorldPosition;
      
      void main() {
        float dist = distance(cameraPosition, vWorldPosition);
        float alpha = 1.0 - smoothstep(0.0, fadeDistance, dist);
        gl_FragColor = vec4(gridColor, alpha * 0.3);
      }
    `,
    transparent: true,
    depthWrite: false,
  });
  
  return gridHelper;
};
```

### 4.2 Skybox Gradient (Dark Violet to Teal)
**Goal**: Replace pure black void with atmospheric gradient

**Current**: Already implemented! Just needs color adjustment:
```typescript
// Adjust existing skybox colors for musical context
topColor: new THREE.Color(0x1a0f2a),    // Dark violet (mysterious)
horizonColor: new THREE.Color(0x2a1f3a), // Purple-brown (alchemical)
bottomColor: new THREE.Color(0x0a1f2a), // Dark teal (deep)
```

### 4.3 Smooth Floor Transitions
**Goal**: Animate vertical transitions (fade/dissolve)

**Implementation**:
```typescript
// Smooth floor transition with fade effect
const transitionToFloor = (targetFloor: number, duration: number = 1000) => {
  const startFloor = currentFloor;
  const startY = playerStateRef.current.position.y;
  const targetY = targetFloor * 20 + 3 + 15;
  const startTime = Date.now();
  
  const animate = () => {
    const elapsed = Date.now() - startTime;
    const progress = Math.min(1, elapsed / duration);
    const eased = easeInOutCubic(progress);
    
    // Lerp camera position
    playerStateRef.current.position.y = startY + (targetY - startY) * eased;
    cameraRef.current!.position.y = playerStateRef.current.position.y;
    
    // Fade out old floor, fade in new floor
    if (floorGroupsRef.current[startFloor]) {
      floorGroupsRef.current[startFloor].traverse((obj) => {
        if (obj instanceof THREE.Mesh && obj.material) {
          const mat = obj.material as THREE.MeshStandardMaterial;
          if (mat.opacity !== undefined) {
            mat.opacity = 1 - eased;
          }
        }
      });
    }
    
    if (floorGroupsRef.current[targetFloor]) {
      floorGroupsRef.current[targetFloor].traverse((obj) => {
        if (obj instanceof THREE.Mesh && obj.material) {
          const mat = obj.material as THREE.MeshStandardMaterial;
          if (mat.opacity !== undefined) {
            mat.opacity = eased;
          }
        }
      });
    }
    
    if (progress < 1) {
      requestAnimationFrame(animate);
    }
  };
  
  animate();
};

const easeInOutCubic = (t: number): number => {
  return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
};
```

---

## Phase 5: Tonal Symbolism & Color Harmonics

### 5.1 Color Harmonics System
**Goal**: Hue = tonal family, brightness = consonance, saturation = complexity

**Implementation**:
```typescript
// Color encoding for musical properties
const encodeMusicalColor = (
  tonalFamily: string,
  consonance: number, // 0-1 (0=dissonant, 1=consonant)
  complexity: number  // 0-1 (0=simple, 1=complex)
): THREE.Color => {
  // Base hue from tonal family
  const baseHue = TONAL_FAMILY_HUES[tonalFamily] || 0.5;
  
  // Brightness from consonance
  const lightness = 0.3 + consonance * 0.5; // 0.3-0.8 range
  
  // Saturation from complexity
  const saturation = 0.4 + complexity * 0.6; // 0.4-1.0 range
  
  return new THREE.Color().setHSL(baseHue, saturation, lightness);
};

const TONAL_FAMILY_HUES: Record<string, number> = {
  'G-Family': 0.08,  // Orange
  'C-Family': 0.55,  // Cyan
  'D-Family': 0.33,  // Green
  'A-Family': 0.92,  // Magenta
  'E-Family': 0.45,  // Teal
  'B-Family': 0.75,  // Purple
  'F-Family': 0.15,  // Yellow
};
```

### 5.2 Particle Trails for Harmonic Paths
**Goal**: Visualize voice-leading between chords

**Implementation** (simplified for now, full implementation in Phase 6):
```typescript
// Particle system for voice-leading visualization
const createVoiceLeadingTrail = (
  fromChord: THREE.Vector3,
  toChord: THREE.Vector3,
  color: THREE.Color
) => {
  const particleCount = 50;
  const geometry = new THREE.BufferGeometry();
  const positions = new Float32Array(particleCount * 3);
  const colors = new Float32Array(particleCount * 3);
  
  for (let i = 0; i < particleCount; i++) {
    const t = i / particleCount;
    const pos = new THREE.Vector3().lerpVectors(fromChord, toChord, t);
    positions[i * 3] = pos.x;
    positions[i * 3 + 1] = pos.y;
    positions[i * 3 + 2] = pos.z;
    
    colors[i * 3] = color.r;
    colors[i * 3 + 1] = color.g;
    colors[i * 3 + 2] = color.b;
  }
  
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));
  
  const material = new THREE.PointsMaterial({
    size: 0.2,
    vertexColors: true,
    transparent: true,
    opacity: 0.6,
    blending: THREE.AdditiveBlending,
  });
  
  return new THREE.Points(geometry, material);
};
```

### 5.3 Waveform Ripples for Tonal Gravity Wells
**Goal**: Show G-Family pedestal emitting tonal "gravity"

**Implementation**:
```typescript
// Animated ripple effect for tonal centers
const createTonalRipple = (position: THREE.Vector3, color: THREE.Color) => {
  const rippleGeometry = new THREE.RingGeometry(0, 10, 32);
  const rippleMaterial = new THREE.ShaderMaterial({
    uniforms: {
      time: { value: 0 },
      color: { value: color },
    },
    vertexShader: `
      varying vec2 vUv;
      void main() {
        vUv = uv;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    fragmentShader: `
      uniform float time;
      uniform vec3 color;
      varying vec2 vUv;
      
      void main() {
        float dist = length(vUv - 0.5) * 2.0;
        float wave = sin(dist * 10.0 - time * 2.0) * 0.5 + 0.5;
        float alpha = (1.0 - dist) * wave * 0.5;
        gl_FragColor = vec4(color, alpha);
      }
    `,
    transparent: true,
    side: THREE.DoubleSide,
    depthWrite: false,
  });
  
  const ripple = new THREE.Mesh(rippleGeometry, rippleMaterial);
  ripple.rotation.x = -Math.PI / 2;
  ripple.position.copy(position);
  ripple.position.y = 0.1;
  
  return ripple;
};

// Update in animation loop
const updateRipples = (time: number) => {
  ripples.forEach(ripple => {
    const material = ripple.material as THREE.ShaderMaterial;
    material.uniforms.time.value = time;
  });
};
```

---

## Phase 6: Advanced Rendering (Future)

### 6.1 WebGPU Bloom Pipeline
**Goal**: Cinematic sheen for low cost

**Status**: Deferred (requires WebGPU compute shaders)

### 6.2 Raymarched Outlines
**Goal**: Musical "boundaries" instead of literal geometry

**Status**: Deferred (complex shader work)

### 6.3 Procedural Textures from Chord Spectra
**Goal**: FFT → color gradient mapping

**Status**: Deferred (requires audio analysis integration)

---

## Implementation Priority

### Immediate (High Impact, Low Cost)
1. ✅ Baked contact shadows (Phase 1.1)
2. ✅ Volumetric fog colors (Phase 1.2)
3. ✅ CRT HUD styling (Phase 3.1)
4. ✅ Skybox color adjustment (Phase 4.2)
5. ✅ Color harmonics system (Phase 5.1)

### Short-term (Medium Impact, Medium Cost)
6. ⏳ Dynamic light response (Phase 1.3)
7. ⏳ Reflective floor materials (Phase 2.1)
8. ⏳ Emissive partition edges (Phase 2.2)
9. ⏳ Smooth floor transitions (Phase 4.3)
10. ⏳ Tonal ripple effects (Phase 5.3)

### Long-term (High Impact, High Cost)
11. 🔮 SSAO shader (Phase 1.1)
12. 🔮 Depth-fading grids (Phase 4.1)
13. 🔮 Particle trails (Phase 5.2)
14. 🔮 WebGPU bloom (Phase 6.1)

---

## Performance Budget

- **Target FPS**: 60+ (maintain current performance)
- **Draw Calls**: < 200 (current: ~150)
- **Shader Complexity**: Keep fragment shaders simple (< 100 instructions)
- **Transparency**: Minimize overlapping transparent objects (z-fighting)
- **Particle Count**: < 10,000 total particles

---

## Testing Strategy

1. **Visual Regression**: Screenshot comparison before/after
2. **Performance**: FPS monitoring (target: 60+ on mid-range GPUs)
3. **Accessibility**: Ensure readability with new colors/effects
4. **Cross-browser**: Test WebGPU fallback to WebGL

---

## Next Steps

1. Review this plan with user
2. Implement Phase 1 (Lighting & Atmosphere)
3. Gather feedback on visual direction
4. Iterate on Phases 2-5 based on feedback
5. Defer Phase 6 until core experience is polished

**Remember**: Keep geometry simple, but make the light alive. 🎵✨

