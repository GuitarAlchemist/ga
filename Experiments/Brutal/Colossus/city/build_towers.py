# Models Colossus's towers in Blender (headless, factory settings) and exports them as one GLB, one mesh a variant
# (tower_00, tower_01, ...). Original brutalist massing, made from boxes and cylinders by rule, from a seed:
# - a shaft in two to four stacked masses, each set back or pushed out from the one below;
# - every 3.2 m a floor: a concrete spandrel, then a band of glass set 0.45 m back;
# - on some masses, vertical concrete fins in front of the glass, or a windowless service core on one side;
# - a cantilevered crown, plant boxes, vents and a mast on the roof, and amber warning lamps.
# Four materials split each mesh's primitives: concrete, glass, metal and lamp.
# Units are metres, y up, the base at y = 0 (the scene sinks it into the haze below the walkway).
# Usage: blender -b --factory-startup --python build_towers.py -- <out.glb> [<preview.png>]
import math, random, sys

import bmesh
import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
out, preview = argv[0], (argv[1] if len(argv) > 1 else None)
VARIANTS, FLOOR, SPANDREL, INSET = 12, 3.2, 1.9, 0.45

bpy.ops.wm.read_factory_settings(use_empty=True)
materials = []
for name, colour in (("concrete", (0.32, 0.3, 0.28)), ("glass", (0.05, 0.06, 0.07)), ("metal", (0.12, 0.11, 0.1)),
                     ("lamp", (1.0, 0.5, 0.15))):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*colour, 1.0)
    materials.append(m)
CONCRETE, GLASS, METAL, LAMP = range(4)


def box(bm, lo, hi, mat):
    """An axis-aligned box from corner lo to corner hi."""
    m = Matrix.Translation((Vector(lo) + Vector(hi)) / 2) @ Matrix.Diagonal((*(Vector(hi) - Vector(lo)), 1.0))
    geom = bmesh.ops.create_cube(bm, size=1.0, matrix=m)
    for f in {f for v in geom["verts"] for f in v.link_faces}:
        f.material_index = mat


def cylinder(bm, x, z, y0, y1, r, mat, segments=10):
    m = Matrix.Translation((x, (y0 + y1) / 2, z)) @ Matrix.Rotation(math.pi / 2, 4, "X")
    geom = bmesh.ops.create_cone(bm, cap_ends=True, segments=segments, radius1=r, radius2=r, depth=y1 - y0, matrix=m)
    for f in {f for v in geom["verts"] for f in v.link_faces}:
        f.material_index = mat


def floors(bm, w, d, y0, y1, glazed_sides):
    """Floors from y0 to y1 on a w by d mass: spandrels all round, glass bands set back on the glazed sides."""
    y = y0
    while y + FLOOR <= y1 + 1e-6:
        box(bm, (-w / 2, y, -d / 2), (w / 2, y + SPANDREL, d / 2), CONCRETE)
        gx = [-(w / 2 - INSET if "-x" in glazed_sides else w / 2), (w / 2 - INSET if "+x" in glazed_sides else w / 2)]
        gz = [-(d / 2 - INSET if "-z" in glazed_sides else d / 2), (d / 2 - INSET if "+z" in glazed_sides else d / 2)]
        box(bm, (gx[0], y + SPANDREL, gz[0]), (gx[1], y + FLOOR, gz[1]), GLASS)
        y += FLOOR
    if y < y1:
        box(bm, (-w / 2, y, -d / 2), (w / 2, y1, d / 2), CONCRETE)


def fins(bm, w, d, y0, y1, spacing, depth, rng):
    """Vertical fins standing out of the long sides."""
    for side in (-1, 1):
        n = max(2, int(w / spacing))
        for k in range(n + 1):
            x = -w / 2 + k * w / n
            box(bm, (x - 0.18, y0, side * d / 2 - (depth if side < 0 else 0)), (x + 0.18, y1, side * d / 2 + (depth if side > 0 else 0)), CONCRETE)


def waffle(bm, w, d, y0, y1, spacing, storeys):
    """An egg-crate facade: glass set 0.9 m back behind a grid of concrete piers and deep spandrels."""
    box(bm, (-w / 2 + 0.9, y0, -d / 2 + 0.9), (w / 2 - 0.9, y1, d / 2 - 0.9), GLASS)
    y = y0
    while y < y1 - 1e-6:
        box(bm, (-w / 2, y, -d / 2), (w / 2, min(y + 1.1, y1), d / 2), CONCRETE)
        y += FLOOR * storeys
    for length, across, axis in ((w, d, "x"), (d, w, "z")):
        n = max(2, int(length / spacing))
        for k in range(n + 1):
            a = -length / 2 + k * length / n
            for side in (-1, 1):
                lo = [a - 0.22, y0, side * across / 2 - (0.9 if side > 0 else 0)]
                hi = [a + 0.22, y1, side * across / 2 + (0.9 if side < 0 else 0)]
                if axis == "z":
                    lo = [lo[2], y0, lo[0]]
                    hi = [hi[2], y1, hi[0]]
                box(bm, tuple(min(p, q) for p, q in zip(lo, hi)), tuple(max(p, q) for p, q in zip(lo, hi)), CONCRETE)


def tower(index):
    rng = random.Random(4100 + index * 37)
    bm = bmesh.new()
    w, d = rng.uniform(10, 18), rng.uniform(9, 16)
    height = rng.uniform(80, 200)
    masses = rng.randint(2, 4)
    cuts = sorted(rng.uniform(0.25, 0.85) for _ in range(masses - 1))
    levels = [0.0] + [round(c * height / FLOOR) * FLOOR for c in cuts] + [round(height / FLOOR) * FLOOR]
    mw, md = w, d
    for k in range(masses):
        y0, y1 = levels[k], levels[k + 1]
        if y1 - y0 < FLOOR:
            continue
        glazed = {"+x", "-x", "+z", "-z"}
        core = rng.random() < 0.45
        if core:
            side = rng.choice(["+x", "-x"])
            glazed.discard(side)
            cw = rng.uniform(3.5, 5.5)
            sx = 1 if side == "+x" else -1
            box(bm, (sx * mw / 2 - (cw if sx < 0 else 0), y0, -md * 0.3), (sx * mw / 2 + (cw if sx > 0 else 0), y1 + rng.uniform(3, 9), md * 0.3), CONCRETE)
        facade = rng.choice(["ribbon", "waffle", "fins"])
        if facade == "waffle":
            waffle(bm, mw, md, y0, y1, rng.uniform(2.2, 3.4), rng.choice([1, 2]))
        else:
            floors(bm, mw, md, y0, y1, glazed)
        if facade == "fins":
            fins(bm, mw, md, y0, y1, rng.uniform(2.8, 4.2), rng.uniform(0.6, 1.4), rng)
        if k < masses - 1 and rng.random() < 0.4:
            # A plant floor between masses: a blind concrete band with louvres.
            box(bm, (-mw / 2 - 0.3, y1 - FLOOR, -md / 2 - 0.3), (mw / 2 + 0.3, y1, md / 2 + 0.3), CONCRETE)
            box(bm, (-mw / 2 - 0.31, y1 - FLOOR + 0.8, -md / 2 - 0.31), (mw / 2 + 0.31, y1 - 0.8, md / 2 + 0.31), METAL)
        # The next mass: set back, or pushed out over this one as a cantilever.
        grow = rng.choice([0.72, 0.82, 0.9, 1.12, 1.25])
        mw, md = mw * grow, md * (grow if rng.random() < 0.6 else 1 / grow)

    top = levels[-1]
    crown = rng.random() < 0.6
    if crown:
        cw, cd, ch = mw * rng.uniform(1.15, 1.4), md * rng.uniform(1.15, 1.4), FLOOR * rng.randint(2, 5)
        box(bm, (-cw / 2, top, -cd / 2), (cw / 2, top + ch, cd / 2), CONCRETE)
        box(bm, (-cw / 2 + INSET, top + 0.9, -cd / 2 - 0.01), (cw / 2 - INSET, top + 1.7, cd / 2 + 0.01), GLASS)
        top += ch
    for _ in range(rng.randint(2, 4)):
        px, pz = rng.uniform(-mw * 0.3, mw * 0.3), rng.uniform(-md * 0.3, md * 0.3)
        pw, pd, ph = rng.uniform(2, 5), rng.uniform(2, 5), rng.uniform(2, 5)
        box(bm, (px - pw / 2, top, pz - pd / 2), (px + pw / 2, top + ph, pz + pd / 2), METAL)
    for _ in range(rng.randint(1, 3)):
        cylinder(bm, rng.uniform(-mw * 0.35, mw * 0.35), rng.uniform(-md * 0.35, md * 0.35), top, top + rng.uniform(2, 6), rng.uniform(0.6, 1.4), METAL)
    mast = rng.uniform(8, 22)
    cylinder(bm, 0, 0, top, top + mast, 0.25, METAL, 6)
    box(bm, (-0.45, top + mast, -0.45), (0.45, top + mast + 0.9, 0.45), LAMP)
    for sx in (-1, 1):
        for sz in (-1, 1):
            box(bm, (sx * mw / 2 - 0.35, top - 0.2, sz * md / 2 - 0.35), (sx * mw / 2 + 0.35, top + 0.5, sz * md / 2 + 0.35), LAMP)

    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    mesh = bpy.data.meshes.new(f"tower_{index:02d}")
    bm.to_mesh(mesh)
    bm.free()
    for m in materials:
        mesh.materials.append(m)
    for p in mesh.polygons:
        p.use_smooth = False
    obj = bpy.data.objects.new(f"tower_{index:02d}", mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj, len(mesh.polygons), top + mast


objs = []
for i in range(VARIANTS):
    o, faces, h = tower(i)
    objs.append(o)
    print(f"TOWER {i:02d}: {faces} faces, {h:.0f} m tall", flush=True)

# Blender's frame is z up: the meshes were built y up, so turn them for the exporter, which writes y up again.
for o in objs:
    o.rotation_euler = (math.pi / 2, 0, 0)
bpy.ops.object.select_all(action="SELECT")
bpy.context.view_layer.objects.active = objs[0]
bpy.ops.object.transform_apply(rotation=True)
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_yup=True, export_apply=True, export_texcoords=False,
                          export_normals=True, export_materials="EXPORT", export_extras=False)
print("TOWERS exported", out, flush=True)

if preview:
    for k, o in enumerate(objs):
        o.location = ((k % 6) * 40 - 100, (k // 6) * 60, 0)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_cavity = True
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.lens = 35
    cam.location = (0, -330, 120)
    cam.rotation_euler = (math.radians(78), 0, 0)
    scene.render.filepath = preview
    bpy.ops.render.render(write_still=True)
