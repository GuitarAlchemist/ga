// LevelLoader.ts
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { MeshBVH, acceleratedRaycast } from 'three-mesh-bvh';

THREE.Mesh.prototype.raycast = acceleratedRaycast as any;

export type Portal = {
  from: string;
  to: string;
  quad: THREE.Vector3[];   // 4 points world-space (convex quad)
  plane: THREE.Plane;      // plane du portal (orientation: normal vers "to")
};

export type Cell = {
  id: string;
  meshes: THREE.Mesh[];
  merged?: THREE.Mesh;     // optionnel: mesh fusionné
  portals: Portal[];
  aabb: THREE.Box3;
};

export type Level = {
  sceneRoot: THREE.Object3D;
  cells: Map<string, Cell>;
  portals: Portal[];
};

function getExtras(o: any): any {
  // glTF → three transfère souvent extras sur userData, mais on sécurise
  return o.userData ?? (o.extras ?? undefined);
}

function readVec3FromAttr(geom: THREE.BufferGeometry, i: number): THREE.Vector3 {
  const pos = geom.getAttribute('position') as THREE.BufferAttribute;
  return new THREE.Vector3(
    pos.getX(i),
    pos.getY(i),
    pos.getZ(i)
  );
}

function ensureIndex(geom: THREE.BufferGeometry) {
  if (!geom.getIndex()) geom.setIndex([...Array(geom.getAttribute('position').count).keys()]);
}

function mergeByMaterial(meshes: THREE.Mesh[]): THREE.Mesh | undefined {
  if (meshes.length === 0) return undefined;

  // Stratégie simple : groupe contenant 1 mesh par material
  // (plus robuste qu'un vrai merge de géométries pour garder UV/mtl intacts).
  const group = new THREE.Group();
  for (const m of meshes) {
    // clone léger: on réutilise la géo/material, on fixe les matrices
    const clone = new THREE.Mesh(m.geometry, m.material);
    clone.matrixAutoUpdate = false;
    clone.applyMatrix4(m.matrixWorld);
    clone.updateMatrixWorld(true);
    group.add(clone);
  }

  // Option: convertir Group -> single mesh en bakeant matrices (facultatif)
  // Ici on préfère garder les meshes séparés mais regroupés sous un parent.
  // Pour BVH per-mesh:
  group.traverse(obj => {
    if ((obj as any).isMesh) {
      const mesh = obj as THREE.Mesh;
      ensureIndex(mesh.geometry as THREE.BufferGeometry);
      (mesh.geometry as any).boundsTree = new MeshBVH(mesh.geometry as THREE.BufferGeometry);
    }
  });

  // Emballe sous un Mesh vide pour API homogène
  const holder = new THREE.Mesh();
  holder.add(group);
  return holder;
}

export class CellsPortalsLevelLoader {
  constructor(private gltfLoader = new GLTFLoader()) {}

  async load(url: string): Promise<Level> {
    const gltf = await this.gltfLoader.loadAsync(url);
    const root = gltf.scene;

    // Collecte
    const cells = new Map<string, Cell>();
    const portals: Portal[] = [];

    // Étape 1 : tagger objets avec userData (assure extras → userData)
    root.traverse(o => {
      const anyO = o as any;
      // si "extras" vient du parser glTF, on les répercute
      if (anyO.extras) {
        anyO.userData = { ...(anyO.userData ?? {}), ...(anyO.extras ?? {}) };
      }
    });

    // Étape 2 : détecter cells/portals + associer meshes aux cells
    root.traverse(o => {
      const ud = getExtras(o);
      if (!ud) return;

      // Portals: un mesh quad avec {type:"portal", from, to}
      if ((o as any).isMesh && ud.type === 'portal') {
        const mesh = o as THREE.Mesh;
        const geom = mesh.geometry as THREE.BufferGeometry;
        const quadLocal: THREE.Vector3[] = [];
        const posAttr = geom.getAttribute('position') as THREE.BufferAttribute;
        if (!posAttr || posAttr.count < 4) {
          console.warn('Portal mesh must have at least 4 vertices for a quad.');
          return;
        }
        // On prend les 4 premiers sommets (supposés former un quad convexe)
        for (let i = 0; i < 4; i++) {
          const v = readVec3FromAttr(geom, i).applyMatrix4(mesh.matrixWorld);
          quadLocal.push(v);
        }
        const plane = new THREE.Plane().setFromCoplanarPoints(quadLocal[0], quadLocal[1], quadLocal[2]);

        portals.push({
          from: ud.from,
          to: ud.to,
          quad: quadLocal,
          plane
        });
      }

      // Cells: un objet "conteneur" avec {type:"cell", cellId}
      if (ud.type === 'cell' && ud.cellId) {
        if (!cells.has(ud.cellId)) {
          cells.set(ud.cellId, {
            id: ud.cellId,
            meshes: [],
            portals: [],
            aabb: new THREE.Box3()
          });
        }
      }
    });

    // Étape 3 : associer chaque Mesh enfant au parent "cell"
    root.traverse(o => {
      const parent = o.parent as any;
      if (!parent) return;
      const pud = getExtras(parent);
      if (!pud || pud.type !== 'cell' || !pud.cellId) return;

      if ((o as any).isMesh) {
        const cell = cells.get(pud.cellId)!;
        const mesh = o as THREE.Mesh;

        // Calcul BVH par mesh (utile si on ne merge pas)
        ensureIndex(mesh.geometry as THREE.BufferGeometry);
        (mesh.geometry as any).boundsTree = new MeshBVH(mesh.geometry as THREE.BufferGeometry);

        cell.meshes.push(mesh);
        // agrandir AABB de la cellule
        mesh.updateWorldMatrix(true, true);
        const bb = new THREE.Box3().setFromObject(mesh);
        cell.aabb.union(bb);
      }
    });

    // Étape 4 : attacher les portals aux cellules "from"
    for (const p of portals) {
      const c = cells.get(p.from);
      if (!c) continue;
      c.portals.push(p);
    }

    // Étape 5 : optionnel — fusion logique par cellule (grouping) + BVH
    for (const c of cells.values()) {
      c.merged = mergeByMaterial(c.meshes);
    }

    return {
      sceneRoot: root,
      cells,
      portals
    };
  }
}
