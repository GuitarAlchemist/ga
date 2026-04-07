// src/components/PrimeRadiant/BorgCubeNode.ts
// Borg cube for constitution governance nodes — dark metallic hull with
// irregular panel extrusions, green emissive windows, orange conduit lines.
// Matches the look of the BlenderKit "SciFi Cube Spaceship" reference.

import * as THREE from 'three';

// Deterministic hash
function hash(a: number, b: number, c: number): number {
  let h = (a * 73856093) ^ (b * 19349663) ^ (c * 83492791);
  h = ((h >> 16) ^ h) * 0x45d9f3b;
  return ((h >> 16) ^ h) & 0x7fffffff;
}
function hashF(a: number, b: number, c: number): number {
  return (hash(a, b, c) % 10000) / 10000;
}

// ---------------------------------------------------------------------------
// Geometry — 8x8 panels with random depth extrusions per face
// ---------------------------------------------------------------------------

export function createBorgCubeGeometry(size: number): THREE.BufferGeometry {
  const sub = 8;
  const geo = new THREE.BoxGeometry(size, size, size, sub, sub, sub);
  const pos = geo.attributes.position;
  const half = size / 2;
  const cell = size / sub;
  const maxDepth = size * 0.06;

  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i), y = pos.getY(i), z = pos.getZ(i);
    const ax = Math.abs(x), ay = Math.abs(y), az = Math.abs(z);
    const mx = Math.max(ax, ay, az);
    if (Math.abs(mx - half) > 0.01) continue;

    let fid: number, cu: number, cv: number;
    if (ax === mx) { fid = x > 0 ? 0 : 1; cu = Math.floor((y + half) / cell); cv = Math.floor((z + half) / cell); }
    else if (ay === mx) { fid = y > 0 ? 2 : 3; cu = Math.floor((x + half) / cell); cv = Math.floor((z + half) / cell); }
    else { fid = z > 0 ? 4 : 5; cu = Math.floor((x + half) / cell); cv = Math.floor((y + half) / cell); }

    const h = hashF(fid * 100 + cu, cv, 42);
    const depth = h < 0.25 ? maxDepth * (0.5 + h * 2)
      : h < 0.6 ? -maxDepth * (0.3 + (h - 0.25) * 1.5)
      : -maxDepth * 0.1;

    const nx = ax === mx ? Math.sign(x) : 0;
    const ny = ay === mx ? Math.sign(y) : 0;
    const nz = az === mx ? Math.sign(z) : 0;

    // Seam grooves at cell boundaries
    const gs = sub / size;
    const uf = Math.abs((x * gs) % 1 - 0.5) * 2;
    const vf = Math.abs((y * gs) % 1 - 0.5) * 2;
    const wf = Math.abs((z * gs) % 1 - 0.5) * 2;
    const seam = size * 0.012 * (1 - Math.min(1, Math.min(uf, vf, wf) * 4));

    pos.setXYZ(i, x + nx * (depth - seam), y + ny * (depth - seam), z + nz * (depth - seam));
  }
  geo.computeVertexNormals();
  return geo;
}

// ---------------------------------------------------------------------------
// Composite Borg cube node
// ---------------------------------------------------------------------------

export function createBorgCubeNode(size: number, _healthColor?: THREE.Color): THREE.Group {
  const group = new THREE.Group();

  // 1. HULL — very dark metallic, barely visible emissive
  const hull = new THREE.Mesh(
    createBorgCubeGeometry(size),
    new THREE.MeshPhysicalMaterial({
      color: 0x080810,
      metalness: 0.95,
      roughness: 0.4,
      emissive: 0x001108,
      emissiveIntensity: 0.3,
      clearcoat: 0.2,
    }),
  );
  group.add(hull);

  // 2. WIREFRAME — very faint dark blue-green grid (panel seams)
  const wire = new THREE.Mesh(
    new THREE.BoxGeometry(size * 1.001, size * 1.001, size * 1.001, 8, 8, 8),
    new THREE.MeshBasicMaterial({
      color: 0x1a3a2a,
      wireframe: true,
      transparent: true,
      opacity: 0.12,
      depthWrite: false,
    }),
  );
  group.add(wire);

  // 3. ORANGE CONDUIT LINES — sparse circuit traces
  const segs: number[] = [];
  const half = size / 2;
  const step = size / 8;
  for (let face = 0; face < 6; face++) {
    for (let i = 0; i <= 8; i++) {
      if (hashF(face, i, 7) > 0.3) continue; // only ~30% of lines
      const t = -half + i * step;
      const off = half * 1.005;
      // Alternate horizontal/vertical per face
      const horiz = hashF(face, i, 99) > 0.5;
      if (face < 2) {
        const s = face === 0 ? off : -off;
        if (horiz) segs.push(s, t, -half, s, t, half);
        else segs.push(s, -half, t, s, half, t);
      } else if (face < 4) {
        const s = face === 2 ? off : -off;
        if (horiz) segs.push(-half, s, t, half, s, t);
        else segs.push(t, s, -half, t, s, half);
      } else {
        const s = face === 4 ? off : -off;
        if (horiz) segs.push(-half, t, s, half, t, s);
        else segs.push(t, -half, s, t, half, s);
      }
    }
  }
  const conduitGeo = new THREE.BufferGeometry();
  conduitGeo.setAttribute('position', new THREE.Float32BufferAttribute(segs, 3));
  group.add(new THREE.LineSegments(conduitGeo, new THREE.LineBasicMaterial({
    color: 0xcc6622, transparent: true, opacity: 0.4, depthWrite: false,
  })));

  // 4. GREEN EMISSIVE WINDOWS — tiny bright dots scattered on panels
  const dotCount = 80;
  const dotPos = new Float32Array(dotCount * 3);
  const dotCol = new Float32Array(dotCount * 3);
  const green = [0.1, 1.0, 0.4];
  const cyan = [0.2, 0.8, 1.0];
  const amber = [1.0, 0.6, 0.1];
  for (let i = 0; i < dotCount; i++) {
    const face = Math.floor(hashF(i, 0, 11) * 6);
    const u = (hashF(i, 1, 22) - 0.5) * size * 0.9;
    const v = (hashF(i, 2, 33) - 0.5) * size * 0.9;
    const off = half * 1.02;
    const i3 = i * 3;
    switch (face) {
      case 0: dotPos[i3] = off; dotPos[i3+1] = u; dotPos[i3+2] = v; break;
      case 1: dotPos[i3] = -off; dotPos[i3+1] = u; dotPos[i3+2] = v; break;
      case 2: dotPos[i3] = u; dotPos[i3+1] = off; dotPos[i3+2] = v; break;
      case 3: dotPos[i3] = u; dotPos[i3+1] = -off; dotPos[i3+2] = v; break;
      case 4: dotPos[i3] = u; dotPos[i3+1] = v; dotPos[i3+2] = off; break;
      default: dotPos[i3] = u; dotPos[i3+1] = v; dotPos[i3+2] = -off; break;
    }
    const r = hashF(i, 3, 44);
    const c = r < 0.55 ? green : r < 0.8 ? cyan : amber;
    dotCol[i3] = c[0]; dotCol[i3+1] = c[1]; dotCol[i3+2] = c[2];
  }
  const dotGeo = new THREE.BufferGeometry();
  dotGeo.setAttribute('position', new THREE.BufferAttribute(dotPos, 3));
  dotGeo.setAttribute('color', new THREE.BufferAttribute(dotCol, 3));
  group.add(new THREE.Points(dotGeo, new THREE.PointsMaterial({
    size: size * 0.04,
    vertexColors: true,
    transparent: true,
    opacity: 0.95,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  })));

  // 5. CORNER GLOW — faint orange emission at corners (like the reference AO glow)
  const corners = [
    [-1,-1,-1], [-1,-1,1], [-1,1,-1], [-1,1,1],
    [1,-1,-1], [1,-1,1], [1,1,-1], [1,1,1],
  ];
  const cornerPos = new Float32Array(8 * 3);
  for (let i = 0; i < 8; i++) {
    cornerPos[i*3] = corners[i][0] * half * 0.95;
    cornerPos[i*3+1] = corners[i][1] * half * 0.95;
    cornerPos[i*3+2] = corners[i][2] * half * 0.95;
  }
  const cornerGeo = new THREE.BufferGeometry();
  cornerGeo.setAttribute('position', new THREE.BufferAttribute(cornerPos, 3));
  group.add(new THREE.Points(cornerGeo, new THREE.PointsMaterial({
    size: size * 0.2,
    color: 0xff6600,
    transparent: true,
    opacity: 0.15,
    blending: THREE.AdditiveBlending,
    depthWrite: false,
    sizeAttenuation: true,
  })));

  group.name = 'borg-cube';
  return group;
}
