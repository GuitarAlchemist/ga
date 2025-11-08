// CapsuleCollision.ts
import * as THREE from 'three';
import { Capsule } from 'three/addons/math/Capsule.js';
import type { Level, Cell } from './LevelLoader';

export type Player = {
  capsule: Capsule;          // segment bas/haut + rayon
  velocity: THREE.Vector3;   // m/s
  onGround: boolean;
  speed: number;             // m/s
  gravity: number;           // m/s^2
  damping: number;           // 0..1
};

export function makePlayer(start = new THREE.Vector3(0, 1.0, 0)): Player {
  const cap = new Capsule(
    new THREE.Vector3(start.x, start.y, start.z),
    new THREE.Vector3(start.x, start.y + 1.6, start.z),
    0.35
  );
  return {
    capsule: cap,
    velocity: new THREE.Vector3(),
    onGround: false,
    speed: 3.0,
    gravity: 9.8,
    damping: 0.92
  };
}

function applyInput(player: Player, dir: THREE.Vector3, dt: number) {
  // dir = direction locale voulue (XZ) normalisée
  const flat = new THREE.Vector3(dir.x, 0, dir.z).normalize();
  player.velocity.addScaledVector(flat, player.speed * dt);
}

function capsuleTranslate(capsule: Capsule, delta: THREE.Vector3) {
  capsule.start.add(delta);
  capsule.end.add(delta);
}

function sqrDistSegSeg(p1: THREE.Vector3, q1: THREE.Vector3, p2: THREE.Vector3, q2: THREE.Vector3) {
  // retourne {s, t, closest1, closest2, sqDist}
  const d1 = q1.clone().sub(p1);
  const d2 = q2.clone().sub(p2);
  const r = p1.clone().sub(p2);
  const a = d1.dot(d1);
  const e = d2.dot(d2);
  const f = d2.dot(r);
  let s, t;

  if (a <= 1e-9 && e <= 1e-9) { s = t = 0; }
  else if (a <= 1e-9) { s = 0; t = THREE.MathUtils.clamp(f / e, 0, 1); }
  else {
    const c = d1.dot(r);
    if (e <= 1e-9) { t = 0; s = THREE.MathUtils.clamp(-c / a, 0, 1); }
    else {
      const b = d1.dot(d2);
      const denom = a * e - b * b;
      if (denom !== 0) s = THREE.MathUtils.clamp((b * f - c * e) / denom, 0, 1);
      else s = 0;
      t = (b * s + f) / e;
      if (t < 0) { t = 0; s = THREE.MathUtils.clamp(-c / a, 0, 1); }
      else if (t > 1) { t = 1; s = THREE.MathUtils.clamp((b - c) / a, 0, 1); }
    }
  }
  const c1 = p1.clone().add(d1.multiplyScalar(s));
  const c2 = p2.clone().add(d2.multiplyScalar(t));
  return { s, t, closest1: c1, closest2: c2, sqDist: c1.distanceToSquared(c2) };
}

function capsuleTriangleResolve(capsule: Capsule, tri: THREE.Triangle, radius: number, out: THREE.Vector3) {
  // Trouve la plus proche approche entre le segment capsule et le triangle (surface)
  // 1) distance segment → segment (edges), 2) distance segment → point (sommets), 3) distance segment → face
  // Approche pragmatique: on teste face + 3 arêtes + 3 sommets et on garde la pénétration max.

  const segP = capsule.start, segQ = capsule.end;
  let bestPen = 0;
  let bestN = new THREE.Vector3();

  // Face: projeter le segment sur le plan du triangle
  const plane = new THREE.Plane().setFromCoplanarPoints(tri.a, tri.b, tri.c);
  const a = plane.projectPoint(segP, new THREE.Vector3());
  const b = plane.projectPoint(segQ, new THREE.Vector3());
  const mid = a.clone().add(b).multiplyScalar(0.5);
  if (tri.containsPoint(mid)) {
    const dist = Math.abs(plane.distanceToPoint(mid));
    const pen = radius - dist;
    if (pen > bestPen) {
      bestPen = pen;
      bestN.copy(plane.normal).multiplyScalar(plane.distanceToPoint(mid) > 0 ? -1 : 1);
    }
  }

  // Arêtes (3) vs segment capsule
  const edges = [
    [tri.a, tri.b],
    [tri.b, tri.c],
    [tri.c, tri.a]
  ] as const;
  for (const [e0, e1] of edges) {
    const { closest1, closest2, sqDist } = sqrDistSegSeg(segP, segQ, e0, e1);
    const dist = Math.sqrt(sqDist);
    const pen = radius - dist;
    if (pen > bestPen) {
      bestPen = pen;
      bestN.copy(closest1.clone().sub(closest2)).normalize();
    }
  }

  // Sommets (3) vs segment capsule
  for (const v of [tri.a, tri.b, tri.c]) {
    // projeter v sur segment capsule
    const ab = segQ.clone().sub(segP);
    const t = THREE.MathUtils.clamp(v.clone().sub(segP).dot(ab) / ab.lengthSq(), 0, 1);
    const p = segP.clone().add(ab.multiplyScalar(t));
    const dist = p.distanceTo(v);
    const pen = radius - dist;
    if (pen > bestPen) {
      bestPen = pen;
      bestN.copy(p.clone().sub(v)).normalize();
    }
  }

  if (bestPen > 0) {
    out.copy(bestN).multiplyScalar(bestPen);
    capsuleTranslate(capsule, out);
    return true;
  }
  return false;
}

function resolveAgainstCell(cell: Cell, capsule: Capsule, radius: number): { moved: boolean; ground: boolean } {
  let moved = false;
  let ground = false;
  const tri = new THREE.Triangle();
  const push = new THREE.Vector3();

  for (const mesh of cell.meshes) {
    const geom = mesh.geometry as any;
    const tree = geom.boundsTree;
    if (!tree) continue;

    // shapecast borné: on visite seulement les boîtes proches du segment + rayon
    tree.shapecast({
      intersectsBounds: (box: THREE.Box3) => {
        // test rapide: distance boîte ↔ segment capsule <= rayon
        const closest = box.clampPoint(capsule.start.clone().add(capsule.end).multiplyScalar(0.5), new THREE.Vector3());
        return closest.distanceTo(capsule.start) <= radius + 2.0 || closest.distanceTo(capsule.end) <= radius + 2.0;
      },
      intersectsTriangle: (triProxy: any) => {
        triProxy.getTriangle(tri);
        if (capsuleTriangleResolve(capsule, tri, radius, push)) {
          moved = true;
          if (push.y > 1e-3) ground = true; // poussée vers le haut → probablement sol
        }
        return false; // continuer
      }
    });
  }
  return { moved, ground };
}

export function stepPlayer(
  player: Player,
  level: Level,
  activeCell: Cell,
  inputDir: THREE.Vector3, // direction de déplacement souhaitée en coords monde (XZ)
  dt: number
) {
  applyInput(player, inputDir, dt);

  // gravité
  player.velocity.y -= player.gravity * dt;

  // déplacement proposé
  const delta = player.velocity.clone().multiplyScalar(dt);
  const before = player.capsule.clone();
  capsuleTranslate(player.capsule, delta);

  // résoudre collisions contre la cellule courante (et voisins par portails si tu veux raffiner)
  const { moved, ground } = resolveAgainstCell(activeCell, player.capsule, player.capsule.radius);
  player.onGround = ground;

  // si ça a beaucoup poussé latéralement, amortir la vitesse dans cette direction
  if (moved) {
    const after = player.capsule.clone();
    const movedVec = after.start.clone().sub(before.start);
    const normalComp = movedVec.length() > 0 ? movedVec.clone().normalize() : new THREE.Vector3();
    const vn = normalComp.multiplyScalar(player.velocity.dot(normalComp));
    player.velocity.sub(vn); // annule composante dans la normale
  }

  // frottements légers
  player.velocity.multiplyScalar(player.damping);

  // clamp Y si on est sur le sol
  if (player.onGround && player.velocity.y < 0) player.velocity.y = 0;
}
