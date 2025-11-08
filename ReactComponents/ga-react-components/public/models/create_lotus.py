"""
Blender script to create a detailed Egyptian Lotus Flower for BSP DOOM Explorer
Run this in Blender's scripting workspace or via command line:
blender --background --python create_lotus.py
"""

import bpy
import math

# Clear existing objects
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

def create_lotus():
    """Create a detailed Egyptian lotus flower model"""
    
    # === 1. FLOWER CENTER (Yellow) ===
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=0.3,
        location=(0, 0, 0.5),
        segments=24,
        ring_count=12
    )
    center = bpy.context.active_object
    center.name = "Lotus_Center"
    center.scale = (1.0, 1.0, 0.6)
    bpy.ops.object.transform_apply(scale=True)
    
    # === 2. INNER PETALS (White/Pink) ===
    inner_petals = []
    num_inner_petals = 8
    
    for i in range(num_inner_petals):
        angle = (i / num_inner_petals) * 2 * math.pi
        x = math.cos(angle) * 0.4
        y = math.sin(angle) * 0.4
        
        # Create petal using scaled sphere
        bpy.ops.mesh.primitive_uv_sphere_add(
            radius=0.5,
            location=(x, y, 0.4),
            segments=16,
            ring_count=12
        )
        petal = bpy.context.active_object
        petal.name = f"Lotus_Inner_Petal_{i}"
        
        # Scale to make petal shape
        petal.scale = (0.3, 0.6, 0.8)
        
        # Rotate to point outward and upward
        petal.rotation_euler = (
            math.radians(30),
            0,
            angle
        )
        bpy.ops.object.transform_apply(scale=True, rotation=True)
        
        inner_petals.append(petal)
    
    # === 3. OUTER PETALS (Larger, more open) ===
    outer_petals = []
    num_outer_petals = 12
    
    for i in range(num_outer_petals):
        angle = (i / num_outer_petals) * 2 * math.pi
        x = math.cos(angle) * 0.8
        y = math.sin(angle) * 0.8
        
        # Create larger petal
        bpy.ops.mesh.primitive_uv_sphere_add(
            radius=0.7,
            location=(x, y, 0.2),
            segments=16,
            ring_count=12
        )
        petal = bpy.context.active_object
        petal.name = f"Lotus_Outer_Petal_{i}"
        
        # Scale to make petal shape
        petal.scale = (0.35, 0.8, 0.6)
        
        # Rotate to point outward and slightly down
        petal.rotation_euler = (
            math.radians(60),
            0,
            angle
        )
        bpy.ops.object.transform_apply(scale=True, rotation=True)
        
        outer_petals.append(petal)
    
    # === 4. STEM ===
    bpy.ops.mesh.primitive_cylinder_add(
        radius=0.1,
        depth=2.0,
        location=(0, 0, -0.8),
        vertices=16
    )
    stem = bpy.context.active_object
    stem.name = "Lotus_Stem"
    
    # Add slight curve to stem
    bevel_mod = stem.modifiers.new(name="Bevel", type='BEVEL')
    bevel_mod.width = 0.02
    bevel_mod.segments = 2
    
    # === 5. LILY PAD (Optional base) ===
    bpy.ops.mesh.primitive_cylinder_add(
        radius=1.5,
        depth=0.1,
        location=(0, 0, -1.8),
        vertices=32
    )
    lily_pad = bpy.context.active_object
    lily_pad.name = "Lotus_Lily_Pad"
    
    # Add notch to lily pad (Egyptian style)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
    
    # === 6. JOIN FLOWER PARTS ===
    bpy.ops.object.select_all(action='DESELECT')
    center.select_set(True)
    
    for petal in inner_petals:
        petal.select_set(True)
    
    for petal in outer_petals:
        petal.select_set(True)
    
    stem.select_set(True)
    lily_pad.select_set(True)
    
    # Set center as active
    bpy.context.view_layer.objects.active = center
    
    # Join all parts
    bpy.ops.object.join()
    lotus_mesh = bpy.context.active_object
    lotus_mesh.name = "Egyptian_Lotus"
    
    # === 7. ADD MATERIALS ===
    # Create petal material (white with pink tint)
    mat_petal = bpy.data.materials.new(name="Lotus_Petal_Material")
    mat_petal.use_nodes = True
    nodes = mat_petal.node_tree.nodes
    links = mat_petal.node_tree.links
    
    nodes.clear()
    
    bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)
    
    # White with pink tint
    bsdf.inputs['Base Color'].default_value = (0.95, 0.85, 0.90, 1.0)
    bsdf.inputs['Metallic'].default_value = 0.0
    bsdf.inputs['Roughness'].default_value = 0.4
    bsdf.inputs['Specular IOR Level'].default_value = 0.5

    # Subsurface scattering for translucent petals (Blender 4.5+ compatible)
    if 'Subsurface Weight' in bsdf.inputs:
        bsdf.inputs['Subsurface Weight'].default_value = 0.3
    if 'Subsurface Radius' in bsdf.inputs:
        bsdf.inputs['Subsurface Radius'].default_value = (1.0, 0.7, 0.8)
    
    # Add slight emission for glow
    bsdf.inputs['Emission Color'].default_value = (1.0, 0.9, 0.95, 1.0)
    bsdf.inputs['Emission Strength'].default_value = 0.1
    
    output = nodes.new(type='ShaderNodeOutputMaterial')
    output.location = (300, 0)
    
    links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    
    if lotus_mesh.data.materials:
        lotus_mesh.data.materials[0] = mat_petal
    else:
        lotus_mesh.data.materials.append(mat_petal)
    
    # === 8. SMOOTH SHADING ===
    bpy.ops.object.shade_smooth()
    
    # === 9. CENTER AND SCALE ===
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    lotus_mesh.location = (0, 0, 0)
    lotus_mesh.scale = (0.8, 0.8, 0.8)
    bpy.ops.object.transform_apply(scale=True)
    
    return lotus_mesh

# Create the lotus
lotus = create_lotus()

# === 10. ADD LIGHTING ===
# Soft lighting for flower
bpy.ops.object.light_add(type='SUN', location=(3, -3, 8))
sun = bpy.context.active_object
sun.data.energy = 1.5
sun.rotation_euler = (math.radians(50), 0, math.radians(30))

bpy.ops.object.light_add(type='AREA', location=(-2, 2, 4))
area = bpy.context.active_object
area.data.energy = 40
area.data.size = 4

# Add rim light for petal translucency
bpy.ops.object.light_add(type='AREA', location=(0, -3, 2))
rim = bpy.context.active_object
rim.data.energy = 30
rim.data.size = 3
rim.data.color = (1.0, 0.9, 0.95)

# === 11. SETUP CAMERA ===
bpy.ops.object.camera_add(location=(3, -3, 2))
camera = bpy.context.active_object
camera.rotation_euler = (math.radians(70), 0, math.radians(45))
bpy.context.scene.camera = camera

# === 12. EXPORT AS GLB ===
import os

script_dir = bpy.path.abspath("//")
if not script_dir:
    script_dir = os.path.dirname(os.path.abspath(__file__))

export_path = os.path.join(script_dir, "lotus.glb")

bpy.ops.object.select_all(action='DESELECT')
lotus.select_set(True)

bpy.ops.export_scene.gltf(
    filepath=export_path,
    export_format='GLB',
    use_selection=True,
    export_materials='EXPORT'
)

print(f"✅ Egyptian Lotus model created and exported to: {export_path}")
print("✅ Ready to use in Three.js!")

