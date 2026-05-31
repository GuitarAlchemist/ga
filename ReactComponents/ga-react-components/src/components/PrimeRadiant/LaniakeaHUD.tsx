// src/components/PrimeRadiant/LaniakeaHUD.tsx
// SDvision/Tully-style schematic HUD for our position inside Laniakea.

import React, { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';

type BasinPoint = [number, number];

const BASIN_POINTS: BasinPoint[] = [
  [-1.34, 0.0],
  [-1.14, 0.38],
  [-0.74, 0.64],
  [-0.2, 0.58],
  [0.28, 0.72],
  [0.88, 0.52],
  [1.28, 0.2],
  [1.08, -0.14],
  [0.66, -0.38],
  [0.42, -0.68],
  [-0.04, -0.55],
  [-0.42, -0.74],
  [-0.86, -0.52],
  [-1.18, -0.24],
];

const ATTRACTOR_2D = new THREE.Vector2(0.48, -0.18);
const MILKY_WAY_2D = new THREE.Vector2(-0.82, 0.03);

function toVec3(point: THREE.Vector2 | BasinPoint, z = 0): THREE.Vector3 {
  const x = Array.isArray(point) ? point[0] : point.x;
  const y = Array.isArray(point) ? point[1] : point.y;
  return new THREE.Vector3(x, y, z);
}

function disposeObject(root: THREE.Object3D): void {
  root.traverse(obj => {
    const renderable = obj as THREE.Mesh | THREE.Line | THREE.Points;
    renderable.geometry?.dispose();

    const material = renderable.material;
    if (Array.isArray(material)) {
      material.forEach(m => m.dispose());
    } else {
      material?.dispose();
    }
  });
}

function pointInPolygon(point: THREE.Vector2, polygon: BasinPoint[]): boolean {
  let inside = false;
  for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
    const xi = polygon[i][0];
    const yi = polygon[i][1];
    const xj = polygon[j][0];
    const yj = polygon[j][1];
    const intersects = yi > point.y !== yj > point.y
      && point.x < ((xj - xi) * (point.y - yi)) / (yj - yi) + xi;
    if (intersects) inside = !inside;
  }
  return inside;
}

function createBasinFill(): THREE.Group {
  const group = new THREE.Group();
  const shape = new THREE.Shape(BASIN_POINTS.map(([x, y]) => new THREE.Vector2(x, y)));

  const fill = new THREE.Mesh(
    new THREE.ShapeGeometry(shape),
    new THREE.MeshBasicMaterial({
      color: 0xd87b27,
      transparent: true,
      opacity: 0.09,
      side: THREE.DoubleSide,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    }),
  );
  group.add(fill);

  const boundaryPoints = [...BASIN_POINTS, BASIN_POINTS[0]].map(p => toVec3(p, 0.015));
  const boundary = new THREE.Line(
    new THREE.BufferGeometry().setFromPoints(boundaryPoints),
    new THREE.LineBasicMaterial({
      color: 0xff8f32,
      transparent: true,
      opacity: 0.95,
      blending: THREE.AdditiveBlending,
    }),
  );
  group.add(boundary);

  const inner = new THREE.Line(
    new THREE.BufferGeometry().setFromPoints(boundaryPoints.map(p => p.clone().multiplyScalar(0.83).setZ(0.025))),
    new THREE.LineBasicMaterial({
      color: 0xffb45a,
      transparent: true,
      opacity: 0.22,
      blending: THREE.AdditiveBlending,
    }),
  );
  group.add(inner);

  return group;
}

function createDensityField(): THREE.Points {
  const count = 420;
  const positions: number[] = [];
  const colors: number[] = [];
  const color = new THREE.Color();

  for (let i = 0; positions.length / 3 < count && i < count * 10; i++) {
    const seedA = Math.sin(i * 12.9898) * 43758.5453;
    const seedB = Math.sin((i + 71) * 78.233) * 24634.6345;
    const x = -1.28 + (seedA - Math.floor(seedA)) * 2.46;
    const y = -0.68 + (seedB - Math.floor(seedB)) * 1.34;
    const point = new THREE.Vector2(x, y);
    if (!pointInPolygon(point, BASIN_POINTS)) continue;

    const attractorDistance = point.distanceTo(ATTRACTOR_2D);
    const localSheet = Math.exp(-Math.abs(y + 0.05 + Math.sin(x * 3.0) * 0.08) * 5.0);
    const density = Math.max(0, 1.1 - attractorDistance) * 0.7 + localSheet * 0.4;

    positions.push(x, y, (seedA - Math.floor(seedA) - 0.5) * 0.12);

    if (density > 0.72) {
      color.setRGB(1.0, 0.72, 0.34);
    } else if (density > 0.45) {
      color.setRGB(0.95, 0.95, 0.88);
    } else {
      color.setRGB(0.34, 0.55, 0.8);
    }
    colors.push(color.r, color.g, color.b);
  }

  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));

  return new THREE.Points(
    geometry,
    new THREE.PointsMaterial({
      size: 0.018,
      vertexColors: true,
      transparent: true,
      opacity: 0.9,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    }),
  );
}

function createFlowLine(start: THREE.Vector2, index: number): THREE.Line {
  const end = ATTRACTOR_2D.clone();
  const mid = start.clone().lerp(end, 0.58);
  mid.x += Math.sin(index * 1.3) * 0.12;
  mid.y += Math.cos(index * 1.9) * 0.1;

  const curve = new THREE.QuadraticBezierCurve3(
    toVec3(start, 0.045),
    toVec3(mid, 0.105 + (index % 3) * 0.018),
    toVec3(end, 0.06),
  );

  return new THREE.Line(
    new THREE.BufferGeometry().setFromPoints(curve.getPoints(44)),
    new THREE.LineBasicMaterial({
      color: 0xf2f5ff,
      transparent: true,
      opacity: 0.42,
      blending: THREE.AdditiveBlending,
    }),
  );
}

function createMarker(position: THREE.Vector2, color: number, size: number): THREE.Group {
  const group = new THREE.Group();
  group.position.copy(toVec3(position, 0.16));

  const core = new THREE.Mesh(
    new THREE.SphereGeometry(size, 28, 18),
    new THREE.MeshBasicMaterial({ color }),
  );
  group.add(core);

  const ring = new THREE.Mesh(
    new THREE.RingGeometry(size * 1.65, size * 2.28, 44),
    new THREE.MeshBasicMaterial({
      color,
      transparent: true,
      opacity: 0.62,
      side: THREE.DoubleSide,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
    }),
  );
  ring.name = 'pulse-ring';
  group.add(ring);
  return group;
}

export const LaniakeaHUD: React.FC = () => {
  const hostRef = useRef<HTMLDivElement>(null);
  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    if (collapsed) return undefined;

    const host = hostRef.current;
    if (!host) return undefined;

    const renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true, preserveDrawingBuffer: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setClearColor(0x000000, 0);
    host.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(34, 1, 0.1, 20);
    camera.position.set(0, 0.2, 4.4);

    const root = new THREE.Group();
    root.scale.set(1.02, 1.02, 1.02);
    root.rotation.set(-0.48, 0.08, -0.02);
    scene.add(root);

    const grid = new THREE.GridHelper(3.0, 10, 0x25476b, 0x182337);
    grid.rotation.x = Math.PI / 2;
    grid.position.z = -0.03;
    grid.material.opacity = 0.16;
    grid.material.transparent = true;
    root.add(grid);

    root.add(createBasinFill());
    root.add(createDensityField());

    [
      new THREE.Vector2(-1.16, 0.32),
      new THREE.Vector2(-1.02, -0.28),
      new THREE.Vector2(-0.72, 0.48),
      new THREE.Vector2(-0.36, -0.48),
      new THREE.Vector2(0.0, 0.48),
      new THREE.Vector2(0.18, -0.52),
      new THREE.Vector2(0.74, 0.3),
      new THREE.Vector2(0.96, -0.06),
      MILKY_WAY_2D,
    ].forEach((start, index) => root.add(createFlowLine(start, index)));

    const attractor = createMarker(ATTRACTOR_2D, 0xff613d, 0.044);
    attractor.name = 'great-attractor-marker';
    root.add(attractor);

    const milkyWay = createMarker(MILKY_WAY_2D, 0x44a6ff, 0.05);
    milkyWay.name = 'milky-way-marker';
    root.add(milkyWay);

    const resize = () => {
      const { clientWidth, clientHeight } = host;
      renderer.setSize(clientWidth, clientHeight, false);
      camera.aspect = Math.max(clientWidth, 1) / Math.max(clientHeight, 1);
      camera.updateProjectionMatrix();
    };
    resize();

    const ro = new ResizeObserver(resize);
    ro.observe(host);

    let raf = 0;
    const clock = new THREE.Clock();
    const animate = () => {
      const t = clock.getElapsedTime();
      root.rotation.y = 0.08 + Math.sin(t * 0.28) * 0.08;
      root.rotation.x = -0.48 + Math.sin(t * 0.18) * 0.035;
      root.traverse(obj => {
        if (obj.name === 'pulse-ring') {
          obj.scale.setScalar(1.0 + Math.sin(t * 3.1) * 0.2);
          obj.lookAt(camera.position);
        }
      });
      renderer.render(scene, camera);
      raf = requestAnimationFrame(animate);
    };
    animate();

    return () => {
      cancelAnimationFrame(raf);
      ro.disconnect();
      disposeObject(scene);
      renderer.dispose();
      renderer.domElement.remove();
    };
  }, [collapsed]);

  return (
    <section className={`laniakea-hud ${collapsed ? 'laniakea-hud--collapsed' : ''}`} aria-label="Laniakea Supercluster position HUD">
      <div className="laniakea-hud__head">
        <div>
          <div className="laniakea-hud__title">Laniakea</div>
          {!collapsed && <div className="laniakea-hud__meta">Cosmic flow basin map</div>}
        </div>
        <button
          className="laniakea-hud__collapse"
          type="button"
          onClick={() => setCollapsed(v => !v)}
          title={collapsed ? 'Show Laniakea HUD' : 'Collapse Laniakea HUD'}
          aria-label={collapsed ? 'Show Laniakea HUD' : 'Collapse Laniakea HUD'}
        >
          {collapsed ? '+' : '-'}
        </button>
      </div>

      {!collapsed && (
        <>
          <div className="laniakea-hud__canvas" ref={hostRef} />
          <div className="laniakea-hud__callout laniakea-hud__callout--home">
            <span className="laniakea-hud__dot laniakea-hud__dot--home" />
            Milky Way / Local Group
          </div>
          <div className="laniakea-hud__callout laniakea-hud__callout--attractor">
            <span className="laniakea-hud__dot laniakea-hud__dot--attractor" />
            Great Attractor
          </div>
          <div className="laniakea-hud__legend">
            <span>Orange contour: Laniakea basin</span>
            <span>White lines: galaxy flow</span>
          </div>
        </>
      )}
    </section>
  );
};
