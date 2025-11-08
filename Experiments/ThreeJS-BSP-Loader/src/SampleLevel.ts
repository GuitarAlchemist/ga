// SampleLevel.ts
import * as THREE from 'three';
import { MeshBVH } from 'three-mesh-bvh';
import type { Level, Cell, Portal } from './LevelLoader';

function boxRoom(w: number, h: number, d: number, thickness = 0.1) {
  // Pièce = 5 panneaux (sol/plafond + 4 murs) ouverts côté +X
  const mats = new THREE.MeshStandardMaterial({ color: 0x808080, metalness: 0.0, roughness: 0.9 });
  const group = new THREE.Group();

  const floor = new THREE.Mesh(new THREE.BoxGeometry(w, thickness, d), mats);
  floor.position.set(0, 0, 0);
  floor.updateMatrixWorld(true);
  group.add(floor);

  const ceil = new THREE.Mesh(new THREE.BoxGeometry(w, thickness, d), mats);
  ceil.position.set(0, h, 0);
  group.add(ceil);

  const wallN = new THREE.Mesh(new THREE.BoxGeometry(thickness, h, d), mats);
  wallN.position.set(-w/2, h/2, 0);
  group.add(wallN);

  const wallS = new THREE.Mesh(new THREE.BoxGeometry(thickness, h, d), mats);
  wallS.position.set(w/2, h/2, 0);
  group.add(wallS);

  const wallW = new THREE.Mesh(new THREE.BoxGeometry(w, h, thickness), mats);
  wallW.position.set(0, h/2, -d/2);
  group.add(wallW);

  const wallE = new THREE.Mesh(new THREE.BoxGeometry(w, h, thickness), mats);
  wallE.position.set(0, h/2, d/2);
  group.add(wallE);

  group.traverse(o => {
    if ((o as any).isMesh) {
      const m = o as THREE.Mesh;
      (m.geometry as THREE.BufferGeometry).computeBoundingBox();
    }
  });

  return group;
}

function makePortalQuad(p0: THREE.Vector3, p1: THREE.Vector3, p2: THREE.Vector3, p3: THREE.Vector3) {
  const geo = new THREE.BufferGeometry().setFromPoints([p0,p1,p2,p3]);
  // On stocke juste 4 sommets; on n'affiche pas le quad (c'est un proxy logique)
  const mat = new THREE.MeshBasicMaterial({ visible: false });
  const mesh = new THREE.Mesh(geo, mat);
  return mesh;
}

export function buildSampleLevel(): Level {
  const sceneRoot = new THREE.Group();

  // Deux pièces 4x3x4
  const hallObj = boxRoom(4, 3, 4);
  const kitchenObj = boxRoom(4, 3, 4);
  kitchenObj.position.set(4.5, 0, 0); // à droite du hall

  // Tag de cellules (on suit notre convention userData.type/cellId)
  (hallObj as any).userData = { type: 'cell', cellId: 'hall' };
  (kitchenObj as any).userData = { type: 'cell', cellId: 'kitchen' };

  sceneRoot.add(hallObj, kitchenObj);

  // Portail : un quad dans l'ouverture du mur droit du hall
  const y0 = 0.2, y1 = 2.2;          // hauteur d'ouverture
  const z0 = -0.6, z1 = 0.6;         // largeur
  const xPortal = 2.0;               // mur droit du hall (w/2 = 2)

  const p0 = new THREE.Vector3(xPortal, y0, z0);
  const p1 = new THREE.Vector3(xPortal, y1, z0);
  const p2 = new THREE.Vector3(xPortal, y1, z1);
  const p3 = new THREE.Vector3(xPortal, y0, z1);

  const portalMesh = makePortalQuad(p0, p1, p2, p3);
  (portalMesh as any).userData = { type: 'portal', from: 'hall', to: 'kitchen' };
  sceneRoot.add(portalMesh);

  // Portail retour (optionnel) pour traversée inverse
  const xPortalBack = kitchenObj.position.x - 2.0; // côté gauche de la kitchen
  const q0 = new THREE.Vector3(xPortalBack, y0, z0);
  const q1 = new THREE.Vector3(xPortalBack, y1, z0);
  const q2 = new THREE.Vector3(xPortalBack, y1, z1);
  const q3 = new THREE.Vector3(xPortalBack, y0, z1);
  const portalBack = makePortalQuad(q0, q1, q2, q3);
  (portalBack as any).userData = { type: 'portal', from: 'kitchen', to: 'hall' };
  sceneRoot.add(portalBack);

  // Construire la structure Level similaire à LevelLoader
  const cells = new Map<string, Cell>();
  const portals: Portal[] = [];

  function collectCell(obj: THREE.Object3D) {
    if ((obj as any).userData?.type === 'cell' && (obj as any).userData?.cellId) {
      const id = (obj as any).userData.cellId;
      if (!cells.has(id)) cells.set(id, { id, meshes: [], portals: [], aabb: new THREE.Box3() });
    }
  }

  function collectMeshesToCell(obj: THREE.Object3D) {
    const parent = obj.parent as any;
    if (!parent?.userData || parent.userData.type !== 'cell') return;
    if ((obj as any).isMesh) {
      const cell = cells.get(parent.userData.cellId)!;
      const mesh = obj as THREE.Mesh;
      // BVH
      (mesh.geometry as THREE.BufferGeometry).setIndex(
        (mesh.geometry as THREE.BufferGeometry).getIndex() ??
        [...Array((mesh.geometry as THREE.BufferGeometry).getAttribute('position').count).keys()]
      );
      (mesh.geometry as any).boundsTree = new MeshBVH(mesh.geometry as THREE.BufferGeometry);
      cell.meshes.push(mesh);
      const bb = new THREE.Box3().setFromObject(mesh);
      cell.aabb.union(bb);
    }
  }

  function makePortalFromMesh(obj: THREE.Object3D) {
    if (!(obj as any).isMesh) return;
    const ud = (obj as any).userData;
    if (ud?.type !== 'portal') return;
    const mesh = obj as THREE.Mesh;
    const pos = (mesh.geometry as THREE.BufferGeometry).getAttribute('position') as THREE.BufferAttribute;
    const quad = [0,1,2,3].map(i => new THREE.Vector3(pos.getX(i), pos.getY(i), pos.getZ(i)));
    const plane = new THREE.Plane().setFromCoplanarPoints(quad[0], quad[1], quad[2]);
    portals.push({ from: ud.from, to: ud.to, quad, plane });
  }

  sceneRoot.traverse(collectCell);
  sceneRoot.traverse(collectMeshesToCell);
  sceneRoot.traverse(makePortalFromMesh);
  for (const p of portals) cells.get(p.from)?.portals.push(p);

  return { sceneRoot, cells, portals };
}
