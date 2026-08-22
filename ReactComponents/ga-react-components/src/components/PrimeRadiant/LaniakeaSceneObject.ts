// src/components/PrimeRadiant/LaniakeaSceneObject.ts
// Full 3D Laniakea supercluster as a scene-graph object you can orbit around.
// Reimplemented with Sprite/Line materials because the WebGL2 fallback of
// WebGPURenderer does not render Mesh/Points materials (they remain black), while
// Sprites and Lines render correctly once window.THREE is the project Three.js.

import * as THREE from 'three';

type BasinPoint = [number, number];
const BASIN_POINTS: BasinPoint[] = [
  [-1.34, 0.0], [-1.14, 0.38], [-0.74, 0.64], [-0.2, 0.58], [0.28, 0.72], [0.88, 0.52],
  [1.28, 0.2], [1.08, -0.14], [0.66, -0.38], [0.42, -0.68], [-0.04, -0.55], [-0.42, -0.74],
  [-0.86, -0.52], [-1.18, -0.24],
];

const ATTRACTOR_2D = new THREE.Vector2(0.48, -0.18);
const MILKY_WAY_2D = new THREE.Vector2(-0.82, 0.03);

function toVec3(v: THREE.Vector2 | BasinPoint, z = 0): THREE.Vector3 {
  return new THREE.Vector3(Array.isArray(v) ? v[0] : v.x, Array.isArray(v) ? v[1] : v.y, z);
}

function pointInPolygon(p: THREE.Vector2, poly: BasinPoint[]): boolean {
  let inside = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const [xi, yi] = poly[i];
    const [xj, yj] = poly[j];
    if (yi > p.y !== yj > p.y && p.x < (xj - xi) * (p.y - yi) / (yj - yi + 1e-9) + xi) inside = !inside;
  }
  return inside;
}

function makeCanvasTexture(draw: (ctx: CanvasRenderingContext2D, size: number) => void): THREE.CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = 256;
  canvas.height = 256;
  const ctx = canvas.getContext('2d')!;
  draw(ctx, 256);
  const tex = new THREE.CanvasTexture(canvas);
  tex.needsUpdate = true;
  return tex;
}

const WHITE_DOT_TEXTURE = makeCanvasTexture((ctx, size) => {
  const half = size / 2;
  const grad = ctx.createRadialGradient(half, half, 0, half, half, half);
  grad.addColorStop(0, 'rgba(255,255,255,1)');
  grad.addColorStop(0.5, 'rgba(255,255,255,0.5)');
  grad.addColorStop(1, 'rgba(255,255,255,0)');
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, size, size);
});

const WHITE_RING_TEXTURE = makeCanvasTexture((ctx, size) => {
  const half = size / 2;
  ctx.clearRect(0, 0, size, size);
  ctx.strokeStyle = 'rgba(255,255,255,1)';
  ctx.lineWidth = size * 0.08;
  ctx.beginPath();
  ctx.arc(half, half, half * 0.45, 0, Math.PI * 2);
  ctx.stroke();
});

function makeBasinShapeTexture(): THREE.CanvasTexture {
  return makeCanvasTexture((ctx, size) => {
    ctx.clearRect(0, 0, size, size);
    // Match the sprite's world bounds so the basin fill aligns with the
    // density-field points and the boundary line.
    const minX = -1.5, maxX = 1.5, rangeX = maxX - minX;
    const minY = -0.9, maxY = 0.9, rangeY = maxY - minY;
    const mapX = (x: number) => ((x - minX) / rangeX) * size;
    const mapY = (y: number) => size - ((y - minY) / rangeY) * size;
    ctx.beginPath();
    const [firstX, firstY] = BASIN_POINTS[0];
    ctx.moveTo(mapX(firstX), mapY(firstY));
    for (let i = 1; i < BASIN_POINTS.length; i++) {
      const [x, y] = BASIN_POINTS[i];
      ctx.lineTo(mapX(x), mapY(y));
    }
    ctx.closePath();
    const grad = ctx.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
    grad.addColorStop(0, 'rgba(255, 145, 55, 0.26)');
    grad.addColorStop(0.6, 'rgba(216, 123, 39, 0.08)');
    grad.addColorStop(1, 'rgba(216, 123, 39, 0.02)');
    ctx.fillStyle = grad;
    ctx.fill();
  });
}

function createBasinVolume(): THREE.Sprite {
  const material = new THREE.SpriteMaterial({
    map: makeBasinShapeTexture(),
    transparent: true,
    opacity: 0.9,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    fog: false,
  });
  const sprite = new THREE.Sprite(material);
  sprite.scale.set(3.0, 1.8, 1);
  sprite.name = 'basin-fill';
  return sprite;
}

function createBasinBoundary(): THREE.Group {
  const group = new THREE.Group();
  const outline = [...BASIN_POINTS, BASIN_POINTS[0]].map(p => toVec3(p, 0));
  const makeLine = (pts: THREE.Vector3[], color: number, opacity: number) => {
    const material = new THREE.LineBasicMaterial({
      color,
      transparent: opacity < 1,
      opacity,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      fog: false,
    });
    return new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts), material);
  };
  group.add(makeLine(outline, 0xff8f32, 0.95));
  group.add(makeLine(outline.map(p => p.clone().multiplyScalar(0.83)), 0xffb45a, 0.22));
  return group;
}

function createDensityField(): THREE.Group {
  const group = new THREE.Group();
  group.name = 'density-field';
  const count = 150; // reduced from 420 to keep the HUD lightweight while world-locked
  const materials = [
    new THREE.SpriteMaterial({ map: WHITE_DOT_TEXTURE, color: 0xffa022, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, opacity: 0.9, fog: false }),
    new THREE.SpriteMaterial({ map: WHITE_DOT_TEXTURE, color: 0xf0f0e0, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, opacity: 0.7, fog: false }),
    new THREE.SpriteMaterial({ map: WHITE_DOT_TEXTURE, color: 0x5588cc, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false, opacity: 0.7, fog: false }),
  ];
  for (let i = 0; group.children.length < count && i < count * 10; i++) {
    const sa = Math.sin(i * 12.9898) * 43758.5453;
    const sb = Math.sin((i + 71) * 78.233) * 24634.6345;
    const x = -1.28 + (sa - Math.floor(sa)) * 2.46;
    const y = -0.68 + (sb - Math.floor(sb)) * 1.34;
    const p = new THREE.Vector2(x, y);
    if (!pointInPolygon(p, BASIN_POINTS)) continue;
    const d = p.distanceTo(ATTRACTOR_2D);
    const z = (sa - Math.floor(sa) - 0.5) * 3.8;
    const sheet = Math.exp(-Math.abs(y + 0.05 + Math.sin(x * 3.0) * 0.08) * 5.0);
    const density = Math.max(0, 1.1 - d) * 0.7 + sheet * 0.4;
    const matIndex = density > 0.72 ? 0 : density > 0.45 ? 1 : 2;
    const sprite = new THREE.Sprite(materials[matIndex]);
    sprite.position.set(x, y, z);
    const scale = 0.02 + density * 0.03;
    sprite.scale.set(scale, scale, 1);
    group.add(sprite);
  }
  return group;
}

function createFlowLine(start: THREE.Vector2, index: number): THREE.Line {
  const end = ATTRACTOR_2D.clone();
  const mid = start.clone().lerp(end, 0.58);
  mid.x += Math.sin(index * 1.3) * 0.12;
  mid.y += Math.cos(index * 1.9) * 0.1;
  const curve = new THREE.QuadraticBezierCurve3(
    toVec3(start, (index % 2) * 0.8),
    toVec3(mid, 1.8 + (index % 3) * 0.8),
    toVec3(end, 1.2),
  );
  const material = new THREE.LineDashedMaterial({
    color: 0xf2f5ff,
    transparent: true,
    opacity: 0.42,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    fog: false,
    dashSize: 0.06,
    gapSize: 0.05,
  });
  const geometry = new THREE.BufferGeometry().setFromPoints(curve.getPoints(64));
  const line = new THREE.Line(geometry, material);
  line.computeLineDistances();
  line.name = 'flow-line';
  line.userData.speed = 0.35 + (index % 4) * 0.12;
  return line;
}

function createGrid(): THREE.Group {
  const group = new THREE.Group();
  group.name = 'laniakea-grid';
  const material = new THREE.LineBasicMaterial({
    color: 0xff8f32,
    transparent: true,
    opacity: 0.12,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    fog: false,
  });
  const step = 0.4;
  const minX = -1.4, maxX = 1.4;
  const minY = -0.8, maxY = 0.8;
  for (let x = minX; x <= maxX + 1e-6; x += step) {
    const pts = [new THREE.Vector3(x, minY, 0), new THREE.Vector3(x, maxY, 0)];
    group.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts), material));
  }
  for (let y = minY; y <= maxY + 1e-6; y += step) {
    const pts = [new THREE.Vector3(minX, y, 0), new THREE.Vector3(maxX, y, 0)];
    group.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts), material));
  }
  return group;
}

type Neighbor = { name: string; pos: THREE.Vector2; color: number };
const NEIGHBORS: Neighbor[] = [
  { name: 'Perseus-Pisces', pos: new THREE.Vector2(-2.3, 0.7), color: 0x88cc88 },
  { name: 'Shapley', pos: new THREE.Vector2(1.9, 1.1), color: 0xcc88cc },
  { name: 'Coma', pos: new THREE.Vector2(0.3, -1.7), color: 0xccaa88 },
  { name: 'Hydra-Centaurus', pos: new THREE.Vector2(1.7, -0.9), color: 0x88aacc },
];

function createNeighborMarker(n: Neighbor): THREE.Group {
  const group = new THREE.Group();
  group.position.copy(toVec3(n.pos, 0.12));
  const core = new THREE.Sprite(new THREE.SpriteMaterial({
    map: WHITE_DOT_TEXTURE,
    color: n.color,
    transparent: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    fog: false,
    opacity: 0.8,
  }));
  core.scale.set(0.045, 0.045, 1);
  group.add(core);
  const label = createLabel(n.name, 17);
  label.position.set(0, 0.12, 0);
  label.scale.set(0.85, 0.2, 1);
  group.add(label);
  return group;
}

function createNeighborhood(): THREE.Group {
  const group = new THREE.Group();
  group.name = 'laniakea-neighborhood';
  const lineMat = new THREE.LineBasicMaterial({
    color: 0xffaa44,
    transparent: true,
    opacity: 0.12,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    fog: false,
  });
  NEIGHBORS.forEach(n => {
    group.add(createNeighborMarker(n));
    const pts = [toVec3(n.pos, 0.12), toVec3(ATTRACTOR_2D, 0.12)];
    group.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts), lineMat));
  });
  return group;
}

function createLabel(text: string, fontSize = 26): THREE.Sprite {
  const canvas = document.createElement('canvas');
  const width = 512;
  const height = 128;
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext('2d')!;
  ctx.clearRect(0, 0, width, height);
  ctx.font = `bold ${fontSize}px system-ui, sans-serif`;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.shadowColor = 'rgba(0,0,0,0.7)';
  ctx.shadowBlur = 4;
  ctx.fillStyle = 'rgba(255,255,255,0.95)';
  ctx.fillText(text, width / 2, height / 2);
  const tex = new THREE.CanvasTexture(canvas);
  tex.needsUpdate = true;
  const material = new THREE.SpriteMaterial({
    map: tex,
    transparent: true,
    opacity: 0.9,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    fog: false,
  });
  const sprite = new THREE.Sprite(material);
  sprite.scale.set(1.4, 0.35, 1);
  sprite.name = 'label';
  return sprite;
}

function createMarker(
  position: THREE.Vector2,
  color: number,
  size: number,
  label?: string,
  isHome = false,
  pulseRings?: THREE.Sprite[],
  homeGlows?: THREE.Sprite[],
): THREE.Group {
  const group = new THREE.Group();
  group.position.copy(toVec3(position, 0.16));
  if (isHome) {
    const glowMat = new THREE.SpriteMaterial({
      map: WHITE_DOT_TEXTURE,
      color,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      opacity: 0.18,
      fog: false,
    });
    const glow = new THREE.Sprite(glowMat);
    glow.scale.set(size * 10, size * 10, 1);
    glow.name = 'home-glow';
    group.add(glow);
    homeGlows?.push(glow);
  }
  const coreMat = new THREE.SpriteMaterial({
    map: WHITE_DOT_TEXTURE,
    color,
    transparent: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    fog: false,
  });
  const core = new THREE.Sprite(coreMat);
  core.scale.set(size * 2, size * 2, 1);
  group.add(core);
  const ringMat = new THREE.SpriteMaterial({
    map: WHITE_RING_TEXTURE,
    color,
    transparent: true,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    opacity: 0.62,
    fog: false,
  });
  const ring = new THREE.Sprite(ringMat);
  ring.scale.set(size * 4, size * 4, 1);
  ring.name = 'pulse-ring';
  group.add(ring);
  pulseRings?.push(ring);
  if (label) {
    const labelSprite = createLabel(label, 24);
    labelSprite.position.set(0, size * 8, 0);
    group.add(labelSprite);
  }
  return group;
}

type ScaleRung = { label: string; ly: number; color: number };
const SCALE_RUNGS: ScaleRung[] = [
  { label: 'Observable Universe', ly: 9.3e10, color: 0xffffff },
  { label: 'Laniakea', ly: 5.2e8, color: 0xff8f32 },
  { label: 'Milky Way', ly: 1e5, color: 0x44a6ff },
  { label: 'Solar System', ly: 5e-4, color: 0xffdd88 },
];

function formatLy(ly: number): string {
  if (ly >= 1e9) return `${(ly / 1e9).toFixed(1)} Gly`;
  if (ly >= 1e6) return `${(ly / 1e6).toFixed(0)} Mly`;
  if (ly >= 1e3) return `${(ly / 1e3).toFixed(0)} kly`;
  if (ly >= 1) return `${ly.toFixed(1)} ly`;
  return `${ly.toFixed(4)} ly`;
}

function createScaleLadder(): THREE.Group {
  const group = new THREE.Group();
  group.name = 'laniakea-scale-ladder';
  const lineMat = new THREE.LineBasicMaterial({
    color: 0xffaa44,
    transparent: true,
    opacity: 0.55,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    fog: false,
  });
  const minLog = Math.log10(SCALE_RUNGS[SCALE_RUNGS.length - 1].ly);
  const maxLog = Math.log10(SCALE_RUNGS[0].ly);
  const range = maxLog - minLog;
  const leftX = 1.55;
  const bottomY = -0.85;
  const topY = 0.85;
  const tickWidth = 0.2;
  const railPts = [new THREE.Vector3(leftX, bottomY, 0.25), new THREE.Vector3(leftX, topY, 0.25)];
  group.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(railPts), lineMat));
  SCALE_RUNGS.forEach(rung => {
    const y = bottomY + ((Math.log10(rung.ly) - minLog) / range) * (topY - bottomY);
    const tickPts = [new THREE.Vector3(leftX, y, 0.25), new THREE.Vector3(leftX + tickWidth, y, 0.25)];
    const tick = new THREE.Line(new THREE.BufferGeometry().setFromPoints(tickPts), lineMat);
    group.add(tick);
    const label = createLabel(`${rung.label}  ${formatLy(rung.ly)}`, 16);
    label.position.set(leftX + tickWidth + 0.55, y, 0.25);
    label.scale.set(1.15, 0.22, 1);
    group.add(label);
    const dot = new THREE.Sprite(new THREE.SpriteMaterial({
      map: WHITE_DOT_TEXTURE,
      color: rung.color,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      fog: false,
      opacity: 0.9,
    }));
    dot.position.set(leftX + tickWidth + 0.05, y, 0.25);
    dot.scale.set(0.035, 0.035, 1);
    group.add(dot);
  });
  return group;
}

function createAxisWidget(): THREE.Group {
  const group = new THREE.Group();
  group.name = 'laniakea-axis-widget';
  const axisLength = 0.55;
  const axes = [
    { dir: new THREE.Vector3(1, 0, 0), color: 0xff4444, label: 'SGX' },
    { dir: new THREE.Vector3(0, 1, 0), color: 0x44ff44, label: 'SGY' },
    { dir: new THREE.Vector3(0, 0, 1), color: 0x4444ff, label: 'SGZ' },
  ];
  axes.forEach(axis => {
    const end = axis.dir.clone().multiplyScalar(axisLength);
    const material = new THREE.LineBasicMaterial({
      color: axis.color,
      transparent: true,
      opacity: 0.85,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      fog: false,
    });
    const line = new THREE.Line(
      new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(0, 0, 0), end]),
      material,
    );
    group.add(line);
    const tip = new THREE.Sprite(new THREE.SpriteMaterial({
      map: WHITE_DOT_TEXTURE,
      color: axis.color,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      fog: false,
      opacity: 0.9,
    }));
    tip.position.copy(end);
    tip.scale.set(0.04, 0.04, 1);
    group.add(tip);
    const label = createLabel(axis.label, 16);
    label.position.copy(end).add(axis.dir.clone().multiplyScalar(0.14));
    label.scale.set(0.3, 0.3, 1);
    group.add(label);
  });
  return group;
}

export function createLaniakeaSceneObject(): THREE.Group {
  const root = new THREE.Group();
  root.name = 'laniakea-scene-object';
  root.add(createBasinVolume());
  root.add(createBasinBoundary());
  root.add(createDensityField());
  root.add(createGrid());
  root.add(createNeighborhood());
  const flowStarts = [
    new THREE.Vector2(-1.16, 0.32), new THREE.Vector2(-1.02, -0.28), new THREE.Vector2(-0.72, 0.48),
    new THREE.Vector2(-0.36, -0.48), new THREE.Vector2(0.0, 0.48), new THREE.Vector2(0.18, -0.52),
    new THREE.Vector2(0.74, 0.3), new THREE.Vector2(0.96, -0.06), MILKY_WAY_2D,
  ];
  const flowLines: THREE.Line[] = [];
  flowStarts.forEach((s, i) => {
    const line = createFlowLine(s, i);
    flowLines.push(line);
    root.add(line);
  });
  const pulseRings: THREE.Sprite[] = [];
  const homeGlows: THREE.Sprite[] = [];
  const attractor = createMarker(ATTRACTOR_2D, 0xff613d, 0.044, 'Great Attractor', false, pulseRings);
  attractor.name = 'great-attractor-marker';
  root.add(attractor);
  const milkyWay = createMarker(MILKY_WAY_2D, 0x44a6ff, 0.022, 'Milky Way', true, pulseRings, homeGlows);
  milkyWay.name = 'milky-way-marker';
  const homeLabel = createLabel('our home', 18);
  homeLabel.position.set(0, -0.05, 0);
  homeLabel.scale.set(0.9, 0.22, 1);
  milkyWay.add(homeLabel);
  root.add(milkyWay);
  const title = createLabel('Laniakea Supercluster', 32);
  title.position.set(0, 1.2, 0.5);
  title.scale.set(1.6, 0.4, 1);
  root.add(title);
  const subtitle = createLabel('cosmic overview — not to scale', 18);
  subtitle.position.set(0, 1.05, 0.5);
  subtitle.scale.set(0.95, 0.2, 1);
  root.add(subtitle);
  root.add(createScaleLadder());
  const axisWidget = createAxisWidget();
  axisWidget.position.set(-1.15, -0.65, 0.3);
  root.add(axisWidget);
  root.userData.update = (t: number) => {
    root.rotation.y = 0.08 + Math.sin(t * 0.08) * 0.08;
    root.rotation.x = Math.sin(t * 0.05) * 0.035;
    pulseRings.forEach(r => r.scale.setScalar(1.0 + Math.sin(t * 3.1) * 0.2));
    homeGlows.forEach(g => g.scale.setScalar(1.0 + Math.sin(t * 2.4) * 0.15));
    flowLines.forEach(l => {
      const mat = l.material as THREE.LineDashedMaterial;
      mat.dashOffset = -t * l.userData.speed;
    });
    // Keep the axis widget world-aligned so it reads as a genuine spatial compass,
    // even while the rest of the HUD gently rotates.
    if (axisWidget) {
      axisWidget.rotation.set(-root.rotation.x, -root.rotation.y, 0);
    }
  };
  // HUD mode: ensure every material draws on top of the main scene and never
  // gets clipped by the local galaxy/solar-system geometry.
  root.traverse(obj => {
    if (obj instanceof THREE.Sprite || obj instanceof THREE.Line || obj instanceof THREE.Mesh) {
      obj.renderOrder = 999;
      if (obj.material) {
        const materials = Array.isArray(obj.material) ? obj.material : [obj.material];
        materials.forEach(m => {
          m.depthTest = false;
          m.depthWrite = false;
        });
      }
    }
  });
  return root;
}
