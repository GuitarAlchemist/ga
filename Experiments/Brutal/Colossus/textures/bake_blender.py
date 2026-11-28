# Bakes Colossus's surface maps in Blender (headless, factory settings): from each seamless albedo, a tangent-space
# normal map through a Bump node, and a roughness map; packs normal X, normal Y, roughness and height into one RGBA PNG,
# and writes the albedo again at the same size, without the generator's metadata.
# Usage: blender -b --factory-startup --python bake.py -- <albedo dir> <out dir> [name ...] (all of them by default)
import os
import sys

import bpy
import numpy as np

src, dst, *wanted = sys.argv[sys.argv.index("--") + 1:]
SIZE = 512
# Each texture and its bump strength.
STRENGTHS = {"hull": 0.6, "facade": 0.8, "concrete": 0.7, "cladding": 0.4}
os.makedirs(dst, exist_ok=True)

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 1
scene.render.bake.margin = 0
bpy.ops.mesh.primitive_plane_add(size=2)
plane = bpy.context.active_object


def save(pixels, name, colorspace):
    img = bpy.data.images.new(name, SIZE, SIZE, alpha=True)
    img.colorspace_settings.name = colorspace
    img.pixels.foreach_set(np.clip(pixels, 0.0, 1.0).astype(np.float32).ravel())
    img.filepath_raw = os.path.join(dst, name + ".png")
    img.file_format = "PNG"
    img.save()


for name, strength in STRENGTHS.items():
    if wanted and name not in wanted:
        continue
    albedo = bpy.data.images.load(os.path.join(src, f"{name}_00001_.png"))
    albedo.scale(SIZE, SIZE)
    mat = bpy.data.materials.new(name)
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = albedo
    tex.extension = "REPEAT"
    bw = nt.nodes.new("ShaderNodeRGBToBW")
    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = strength
    bump.inputs["Distance"].default_value = 0.02
    rough = nt.nodes.new("ShaderNodeMapRange")
    rough.inputs["To Min"].default_value = 0.85
    rough.inputs["To Max"].default_value = 0.35
    nt.links.new(tex.outputs["Color"], bw.inputs["Color"])
    nt.links.new(bw.outputs["Val"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    nt.links.new(bw.outputs["Val"], rough.inputs["Value"])
    nt.links.new(rough.outputs["Result"], bsdf.inputs["Roughness"])
    target = nt.nodes.new("ShaderNodeTexImage")
    plane.data.materials.clear()
    plane.data.materials.append(mat)

    baked = {}
    for kind in ("NORMAL", "ROUGHNESS"):
        img = bpy.data.images.new(f"{name}_{kind}", SIZE, SIZE, float_buffer=True)
        img.colorspace_settings.name = "Non-Color"
        target.image = img
        nt.nodes.active = target
        bpy.ops.object.bake(type=kind)
        px = np.empty(SIZE * SIZE * 4, dtype=np.float32)
        img.pixels.foreach_get(px)
        baked[kind] = px.reshape(SIZE, SIZE, 4)

    ap = np.empty(SIZE * SIZE * 4, dtype=np.float32)
    albedo.pixels.foreach_get(ap)
    ap = ap.reshape(SIZE, SIZE, 4)
    height = ap[..., 0] * 0.2126 + ap[..., 1] * 0.7152 + ap[..., 2] * 0.0722
    packed = np.dstack([baked["NORMAL"][..., 0], baked["NORMAL"][..., 1], baked["ROUGHNESS"][..., 0], height])
    save(packed, f"{name}_nrh", "Non-Color")
    ap[..., 3] = 1.0
    save(ap, f"{name}_albedo", "sRGB")
    print(f"BAKE {name}: normal z mean {baked['NORMAL'][..., 2].mean():.3f}, roughness {baked['ROUGHNESS'][..., 0].mean():.3f}", flush=True)
