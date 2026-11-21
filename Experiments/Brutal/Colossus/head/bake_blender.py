# Turns the generated head into Colossus's swarm targets, in Blender (headless, factory settings): smooths the mesh,
# keeps what faces the figure, and samples it as blue noise in progressive order, so that the first n points of the file
# cover the face evenly for any swarm size. Each point has a normal and three weights: eyes, mouth (they glow) and jaw
# (it opens when the face speaks).
# Usage: blender -b --factory-startup --python bake_head.py -- <head.glb> <out.bin>
# Output: "CHD1", int32 count, then per point 9 float32: position x y z, normal x y z, eye, mouth, jaw. Axes are the
# scene's: x right, y up, z toward the figure; units are the head's own (crown to neck base is 1).
import math, random, struct, sys

import bmesh
import bpy
import numpy as np
from mathutils.bvhtree import BVHTree

src, dst = sys.argv[sys.argv.index("--") + 1:][:2]
COUNT = 20000
EYES = ((-0.118, 0.103), (0.118, 0.103))   # (x, height) in the front view, read off a Workbench render
MOUTH = (0.0, -0.127)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
bpy.context.view_layer.objects.active = meshes[0]
for o in meshes:
    o.select_set(True)
if len(meshes) > 1:
    bpy.ops.object.join()
head = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
smooth = head.modifiers.new("smooth", "SMOOTH")
smooth.factor, smooth.iterations = 0.5, 6
bpy.ops.object.modifier_apply(modifier="smooth")

bm = bmesh.new()
bm.from_mesh(head.data)
bmesh.ops.triangulate(bm, faces=bm.faces)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.faces.ensure_lookup_table()
volume = sum(f.calc_center_median().dot(f.normal) * f.calc_area() for f in bm.faces) / 3
# Islands of connected faces: a generated mesh can carry inner shells and loose bits; keep the largest.
island, islands = {}, []
for f in bm.faces:
    if f.index in island:
        continue
    stack, members = [f], []
    island[f.index] = len(islands)
    while stack:
        g = stack.pop()
        members.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in island:
                    island[h.index] = len(islands)
                    stack.append(h)
    islands.append(members)
islands.sort(key=lambda m: -sum(x.calc_area() for x in m))
print("HEAD islands", len(islands), [(len(m), round(sum(x.calc_area() for x in m), 4)) for m in islands[:5]], flush=True)
largest = set(x.index for x in islands[0])
print(f"HEAD signed volume {volume:.4f}", flush=True)
# Only the outer skin: a triangle whose normal ray hits the mesh again is inside it (a second shell) or hidden.
tree = BVHTree.FromBMesh(bm)
outer = []
for f in bm.faces:
    c, n = f.calc_center_median(), f.normal
    outer.append(f.index in largest and n.length > 0 and tree.ray_cast(c + n * 2e-3, n, 2.0)[0] is None)
print(f"HEAD outer triangles {sum(outer)} of {len(outer)}", flush=True)
tri = np.array([[v.co[:] for v in f.verts] for f, o in zip(bm.faces, outer) if o], dtype=np.float64)
# Blender's frame is z up with the face looking along -y: convert to the scene's (x, y up, z toward the viewer).
tri = np.stack([tri[..., 0], tri[..., 2], -tri[..., 1]], axis=-1)
lo, hi = tri.reshape(-1, 3).min(0), tri.reshape(-1, 3).max(0)
tri -= (lo + hi) / 2
tri /= (hi - lo)[1]
cross = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
area = np.linalg.norm(cross, axis=1) / 2
normal = cross / np.maximum(area[:, None] * 2, 1e-12)
centre = tri.mean(1)
keep = (normal[:, 2] > -0.1) & (centre[:, 1] > -0.30) & (area > 0)
tri, area, normal = tri[keep], area[keep], normal[keep]
print(f"HEAD triangles kept {keep.sum()} of {len(keep)}, area {area.sum():.4f}", flush=True)

rng = np.random.default_rng(7)
m = COUNT * 12
pick = rng.choice(len(tri), size=m, p=area / area.sum())
u, v = rng.random(m), rng.random(m)
flip = u + v > 1
u[flip], v[flip] = 1 - u[flip], 1 - v[flip]
cand = tri[pick, 0] + u[:, None] * (tri[pick, 1] - tri[pick, 0]) + v[:, None] * (tri[pick, 2] - tri[pick, 0])
cand_n = normal[pick]

# Progressive Poisson-disk thinning: passes of shrinking radius, each accepting candidates far enough from all others.
r_final = math.sqrt(area.sum() / (1.6 * COUNT))
order, points = [], []
for k in range(8, -1, -1):
    r = r_final * 2 ** (k / 2)
    grid = {}
    for j in order:
        grid.setdefault(tuple((cand[j] // r).astype(int)), []).append(j)
    for j in rng.permutation(m):
        if len(order) >= COUNT:
            break
        c = (cand[j] // r).astype(int)
        near = False
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for dz in (-1, 0, 1):
                    for o in grid.get((c[0] + dx, c[1] + dy, c[2] + dz), ()):
                        if np.dot(cand[o] - cand[j], cand[o] - cand[j]) < r * r:
                            near = True
                            break
                    if near:
                        break
                if near:
                    break
            if near:
                break
        if not near:
            order.append(j)
            grid.setdefault(tuple(c), []).append(j)
    print(f"HEAD pass r={r:.4f}: {len(order)} points", flush=True)

P, N = cand[order], cand_n[order]
front = np.clip(N[:, 2], 0, 1)
g = lambda x, y, sx, sy: np.exp(-(((P[:, 0] - x) / sx) ** 2 + ((P[:, 1] - y) / sy) ** 2))
eye = np.maximum(g(*EYES[0], 0.03, 0.017), g(*EYES[1], 0.03, 0.017)) * front
mouth = g(*MOUTH, 0.06, 0.02) * front
jaw = np.clip((MOUTH[1] - P[:, 1]) / 0.03, 0, 1) * np.clip(1 - (P[:, 0] / 0.17) ** 2, 0, 1) * (P[:, 1] > -0.32) * front
with open(dst, "wb") as f:
    f.write(b"CHD1" + struct.pack("<i", len(order)))
    f.write(np.hstack([P, N, eye[:, None], mouth[:, None], jaw[:, None]]).astype("<f4").tobytes())
print(f"HEAD wrote {len(order)} points; eye>0.5 {int((eye > 0.5).sum())}, mouth>0.5 {int((mouth > 0.5).sum())}, "
      f"jaw>0.5 {int((jaw > 0.5).sum())}; bounds {P.min(0).round(3)} {P.max(0).round(3)}", flush=True)
