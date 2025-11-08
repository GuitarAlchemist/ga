"""
Blender script to create a detailed Pyramid Platform for BSP DOOM Explorer
Run this in Blender's scripting workspace or via command line:
blender --background --python create_pyramid.py
"""

import bpy
import math

# Clear existing objects
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

def create_pyramid():
    """Create a detailed pyramid platform model"""
    
    # === 1. MAIN PYRAMID ===
    # Create cone for pyramid shape
    bpy.ops.mesh.primitive_cone_add(
        vertices=4,  # 4 vertices = square base
        radius1=2.0,
        radius2=0.0,
        depth=2.0,
        location=(0, 0, 0)
    )
    pyramid = bpy.context.active_object
    pyramid.name = "Pyramid_Main"
    
    # Rotate to align with axes
    pyramid.rotation_euler = (0, 0, math.radians(45))
    bpy.ops.object.transform_apply(rotation=True)
    
    # === 2. BASE PLATFORM ===
    # Create square platform base
    bpy.ops.mesh.primitive_cube_add(
        size=4.5,
        location=(0, 0, -1.2)
    )
    base = bpy.context.active_object
    base.name = "Pyramid_Base"
    base.scale = (1.0, 1.0, 0.1)
    bpy.ops.object.transform_apply(scale=True)
    
    # === 3. STEPPED LAYERS (Egyptian style) ===
    # Add 3 stepped layers
    layers = []
    layer_sizes = [3.5, 3.0, 2.5]
    layer_heights = [-0.9, -0.6, -0.3]
    
    for i, (size, height) in enumerate(zip(layer_sizes, layer_heights)):
        bpy.ops.mesh.primitive_cube_add(
            size=size,
            location=(0, 0, height)
        )
        layer = bpy.context.active_object
        layer.name = f"Pyramid_Layer_{i}"
        layer.scale = (1.0, 1.0, 0.1)
        layer.rotation_euler = (0, 0, math.radians(45))
        bpy.ops.object.transform_apply(scale=True, rotation=True)
        layers.append(layer)
    
    # === 4. CORNER DECORATIONS ===
    # Add small obelisks at corners
    corner_positions = [
        (2.0, 2.0, -1.0),
        (-2.0, 2.0, -1.0),
        (2.0, -2.0, -1.0),
        (-2.0, -2.0, -1.0),
    ]
    
    corner_obelisks = []
    for i, pos in enumerate(corner_positions):
        bpy.ops.mesh.primitive_cone_add(
            vertices=4,
            radius1=0.15,
            radius2=0.05,
            depth=0.6,
            location=pos
        )
        obelisk = bpy.context.active_object
        obelisk.name = f"Corner_Obelisk_{i}"
        obelisk.rotation_euler = (0, 0, math.radians(45))
        bpy.ops.object.transform_apply(rotation=True)
        corner_obelisks.append(obelisk)
    
    # === 5. CAPSTONE (Golden top) ===
    bpy.ops.mesh.primitive_cone_add(
        vertices=4,
        radius1=0.3,
        radius2=0.0,
        depth=0.4,
        location=(0, 0, 1.2)
    )
    capstone = bpy.context.active_object
    capstone.name = "Pyramid_Capstone"
    capstone.rotation_euler = (0, 0, math.radians(45))
    bpy.ops.object.transform_apply(rotation=True)
    
    # === 6. JOIN MAIN STRUCTURE ===
    bpy.ops.object.select_all(action='DESELECT')
    pyramid.select_set(True)
    base.select_set(True)
    
    for layer in layers:
        layer.select_set(True)
    
    for obelisk in corner_obelisks:
        obelisk.select_set(True)
    
    # Set pyramid as active
    bpy.context.view_layer.objects.active = pyramid
    
    # Join all parts except capstone
    bpy.ops.object.join()
    pyramid_mesh = bpy.context.active_object
    pyramid_mesh.name = "Pyramid_Structure"
    
    # === 7. ADD SANDSTONE MATERIAL TO PYRAMID ===
    mat_sandstone = bpy.data.materials.new(name="Sandstone_Material")
    mat_sandstone.use_nodes = True
    nodes = mat_sandstone.node_tree.nodes
    links = mat_sandstone.node_tree.links
    
    nodes.clear()
    
    bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)
    
    # Sandstone color
    bsdf.inputs['Base Color'].default_value = (0.82, 0.75, 0.55, 1.0)
    bsdf.inputs['Metallic'].default_value = 0.0
    bsdf.inputs['Roughness'].default_value = 0.95
    bsdf.inputs['Specular IOR Level'].default_value = 0.2
    
    # Add noise for texture
    noise_tex = nodes.new(type='ShaderNodeTexNoise')
    noise_tex.location = (-400, 0)
    noise_tex.inputs['Scale'].default_value = 8.0
    noise_tex.inputs['Detail'].default_value = 10.0
    
    color_ramp = nodes.new(type='ShaderNodeValToRGB')
    color_ramp.location = (-200, 0)
    color_ramp.color_ramp.elements[0].color = (0.72, 0.65, 0.45, 1.0)
    color_ramp.color_ramp.elements[1].color = (0.90, 0.82, 0.60, 1.0)
    
    output = nodes.new(type='ShaderNodeOutputMaterial')
    output.location = (300, 0)
    
    links.new(noise_tex.outputs['Fac'], color_ramp.inputs['Fac'])
    links.new(color_ramp.outputs['Color'], bsdf.inputs['Base Color'])
    links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    
    if pyramid_mesh.data.materials:
        pyramid_mesh.data.materials[0] = mat_sandstone
    else:
        pyramid_mesh.data.materials.append(mat_sandstone)
    
    # === 8. ADD GOLD MATERIAL TO CAPSTONE ===
    mat_gold = bpy.data.materials.new(name="Gold_Material")
    mat_gold.use_nodes = True
    nodes_gold = mat_gold.node_tree.nodes
    links_gold = mat_gold.node_tree.links
    
    nodes_gold.clear()
    
    bsdf_gold = nodes_gold.new(type='ShaderNodeBsdfPrincipled')
    bsdf_gold.location = (0, 0)
    
    # Gold color
    bsdf_gold.inputs['Base Color'].default_value = (0.944, 0.776, 0.373, 1.0)
    bsdf_gold.inputs['Metallic'].default_value = 1.0
    bsdf_gold.inputs['Roughness'].default_value = 0.2
    bsdf_gold.inputs['Specular IOR Level'].default_value = 0.5
    
    # Add emission for glow
    bsdf_gold.inputs['Emission Color'].default_value = (0.944, 0.776, 0.373, 1.0)
    bsdf_gold.inputs['Emission Strength'].default_value = 0.5
    
    output_gold = nodes_gold.new(type='ShaderNodeOutputMaterial')
    output_gold.location = (300, 0)
    
    links_gold.new(bsdf_gold.outputs['BSDF'], output_gold.inputs['Surface'])
    
    if capstone.data.materials:
        capstone.data.materials[0] = mat_gold
    else:
        capstone.data.materials.append(mat_gold)
    
    # === 9. SMOOTH SHADING ===
    pyramid_mesh.select_set(True)
    bpy.context.view_layer.objects.active = pyramid_mesh
    bpy.ops.object.shade_smooth()
    
    capstone.select_set(True)
    bpy.context.view_layer.objects.active = capstone
    bpy.ops.object.shade_smooth()
    
    # === 10. JOIN EVERYTHING ===
    bpy.ops.object.select_all(action='DESELECT')
    pyramid_mesh.select_set(True)
    capstone.select_set(True)
    bpy.context.view_layer.objects.active = pyramid_mesh
    bpy.ops.object.join()
    
    final_pyramid = bpy.context.active_object
    final_pyramid.name = "Egyptian_Pyramid"
    
    # === 11. CENTER AND SCALE ===
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    final_pyramid.location = (0, 0, 0)
    final_pyramid.scale = (0.7, 0.7, 0.7)
    bpy.ops.object.transform_apply(scale=True)
    
    return final_pyramid

# Create the pyramid
pyramid = create_pyramid()

# === 12. ADD LIGHTING ===
bpy.ops.object.light_add(type='SUN', location=(5, -5, 10))
sun = bpy.context.active_object
sun.data.energy = 2.5
sun.rotation_euler = (math.radians(45), 0, math.radians(45))

bpy.ops.object.light_add(type='AREA', location=(-3, 3, 5))
area = bpy.context.active_object
area.data.energy = 60
area.data.size = 6

# === 13. SETUP CAMERA ===
bpy.ops.object.camera_add(location=(5, -5, 4))
camera = bpy.context.active_object
camera.rotation_euler = (math.radians(60), 0, math.radians(45))
bpy.context.scene.camera = camera

# === 14. EXPORT AS GLB ===
import os

script_dir = bpy.path.abspath("//")
if not script_dir:
    script_dir = os.path.dirname(os.path.abspath(__file__))

export_path = os.path.join(script_dir, "pyramid.glb")

bpy.ops.object.select_all(action='DESELECT')
pyramid.select_set(True)

bpy.ops.export_scene.gltf(
    filepath=export_path,
    export_format='GLB',
    use_selection=True,
    export_materials='EXPORT'
)

print(f"✅ Egyptian Pyramid model created and exported to: {export_path}")
print("✅ Ready to use in Three.js!")

