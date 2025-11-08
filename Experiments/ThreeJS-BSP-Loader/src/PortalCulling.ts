// PortalCulling.ts
import * as THREE from 'three';
import type { Level, Cell, Portal } from './LevelLoader';

export function frustumFromCamera(cam: THREE.PerspectiveCamera): THREE.Frustum {
  cam.updateMatrixWorld();
  const projView = new THREE.Matrix4().multiplyMatrices(cam.projectionMatrix, cam.matrixWorldInverse);
  return new THREE.Frustum().setFromProjectionMatrix(projView);
}

export function locateCell(level: Level, pos: THREE.Vector3): Cell | null {
  // Heuristique simple: test AABB; si multiple, on prend la plus petite boîte qui contient.
  let best: { cell: Cell; volume: number } | null = null;
  for (const c of level.cells.values()) {
    if (c.aabb.containsPoint(pos)) {
      const vol = (c.aabb.max.x - c.aabb.min.x) * (c.aabb.max.y - c.aabb.min.y) * (c.aabb.max.z - c.aabb.min.z);
      if (!best || vol < best.volume) best = { cell: c, volume: vol };
    }
  }
  return best?.cell ?? null;
}

/**
 * Construit un frustum "resserré" par un portail:
 * - on conserve les 6 plans du frustum courant
 * - on ajoute jusqu'à 4 plans latéraux formés par (caméra, edge du quad)
 * - on impose le plan du portail (face orientée vers la cellule "to")
 */
export function frustumClippedByPortal(
  camPos: THREE.Vector3,
  frustum: THREE.Frustum,
  portal: Portal
): THREE.Frustum | null {
  // Vecteurs du quad (supposé convexe et ordonné)
  const q = portal.quad;
  if (q.length < 4) return null;

  // Vérif côté: on ne rend que si la caméra est du côté "from" du portail
  const side = portal.plane.distanceToPoint(camPos);
  if (side < 0) {
    // caméra du "mauvais" côté: on peut rejeter
    return null;
  }

  // Construire une liste de plans: 6 du frustum + 1 plan du portail + 4 "edge planes"
  const planes: THREE.Plane[] = [];
  for (let i = 0; i < 6; i++) planes.push(frustum.planes[i].clone());

  // Plan du portail — orienter la normale vers la cellule "to"
  const portalPlane = portal.plane.clone();
  // Si la normale regarde vers la caméra, on l'inverse pour "pousser" le frustum à travers le portail
  if (portalPlane.distanceToPoint(camPos) > 0) {
    portalPlane.negate();
  }
  planes.push(portalPlane);

  // Plans latéraux: pour chaque arête (q[i] -> q[i+1]), construire un plan passant par (camPos, q[i], q[i+1])
  for (let i = 0; i < 4; i++) {
    const a = q[i];
    const b = q[(i + 1) % 4];
    const n = new THREE.Vector3().crossVectors(
      new THREE.Vector3().subVectors(a, camPos),
      new THREE.Vector3().subVectors(b, camPos)
    ).normalize();
    // Plan passant par camPos et l'arête (orientation: normale vers l'intérieur du cône)
    const plane = new THREE.Plane().setFromNormalAndCoplanarPoint(n, camPos);
    // Oriente pour que le quad soit "devant"
    if (plane.distanceToPoint(a.clone().add(b).multiplyScalar(0.5)) < 0) plane.negate();
    planes.push(plane);
  }

  // Construire un frustum résultant
  const newFrustum = new THREE.Frustum(
    planes[0], planes[1], planes[2], planes[3], planes[4], planes[5]
  );

  // Vérif rapide: si le quad lui-même est hors frustum, inutile
  const quadBB = new THREE.Box3().setFromPoints(q);
  if (!newFrustum.intersectsBox(quadBB)) return null;

  // NB: Three.Frustum n'accepte que 6 plans, mais on peut tester l'intersection manuellement
  // Pour rester simple/pragmatique, on garde les 6 d'origine et on remplace le near plane par le plan du portail:
  const clipped = frustum.clone();
  clipped.planes[4] = portalPlane; // 4 = near; 5 = far (ordre interne de three)
  return clipped;
}

export function collectVisibleCells(
  level: Level,
  cam: THREE.PerspectiveCamera
): Set<Cell> {
  const start = locateCell(level, cam.position) ?? [...level.cells.values()][0] ?? null;
  if (!start) return new Set();

  const baseFrustum = frustumFromCamera(cam);
  const visible = new Set<Cell>();
  const stack: { cell: Cell; frustum: THREE.Frustum }[] = [{ cell: start, frustum: baseFrustum }];
  const seen = new Set<string>();

  while (stack.length) {
    const { cell, frustum } = stack.pop()!;
    if (seen.has(cell.id)) continue;
    seen.add(cell.id);

    if (!frustum.intersectsBox(cell.aabb)) continue;
    visible.add(cell);

    for (const p of cell.portals) {
      const next = level.cells.get(p.to);
      if (!next) continue;
      const clipped = frustumClippedByPortal(cam.position, frustum, p);
      if (clipped) stack.push({ cell: next, frustum: clipped });
    }
  }
  return visible;
}
