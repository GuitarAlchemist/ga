"""
Blender script to create a detailed Egyptian Scarab Beetle for BSP DOOM Explorer
Run this in Blender's scripting workspace or via command line:
blender --background --python create_scarab.py
"""

import bpy
import math

# Clear existing objects
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

def create_scarab():
    """Create a detailed Egyptian scarab beetle model"""
    
    # === 1. MAIN BODY (Oval) ===
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=1,
        location=(0, 0, 0),
        segments=32,
        ring_count=16
    )
    body = bpy.context.active_object
    body.name = "Scarab_Body"
    
    # Scale to make it beetle-shaped (wider, flatter)
    body.scale = (0.8, 1.2, 0.5)
    bpy.ops.object.transform_apply(scale=True)
    
    # === 2. HEAD (Small sphere) ===
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=0.4,
        location=(0, 1.4, 0),
        segments=24,
        ring_count=12
    )
    head = bpy.context.active_object
    head.name = "Scarab_Head"
    head.scale = (0.9, 0.7, 0.6)
    bpy.ops.object.transform_apply(scale=True)
    
    # === 3. WING COVERS (Elytra) ===
    # Left wing cover
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=0.9,
        location=(-0.5, 0, 0.3),
        segments=24,
        ring_count=12
    )
    left_wing = bpy.context.active_object
    left_wing.name = "Scarab_Left_Wing"
    left_wing.scale = (0.6, 1.1, 0.4)
    left_wing.rotation_euler = (0, 0, math.radians(-10))
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    
    # Right wing cover
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=0.9,
        location=(0.5, 0, 0.3),
        segments=24,
        ring_count=12
    )
    right_wing = bpy.context.active_object
    right_wing.name = "Scarab_Right_Wing"
    right_wing.scale = (0.6, 1.1, 0.4)
    right_wing.rotation_euler = (0, 0, math.radians(10))
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    
    # === 4. LEGS (6 legs, 3 per side) ===
    legs = []
    leg_positions = [
        # Left legs (x, y, z)
        (-0.7, 0.6, -0.3),
        (-0.8, 0.0, -0.3),
        (-0.7, -0.6, -0.3),
        # Right legs
        (0.7, 0.6, -0.3),
        (0.8, 0.0, -0.3),
        (0.7, -0.6, -0.3),
    ]
    
    for i, pos in enumerate(leg_positions):
        # Upper leg segment
        bpy.ops.mesh.primitive_cylinder_add(
            radius=0.08,
            depth=0.5,
            location=pos,
            vertices=12
        )
        leg_upper = bpy.context.active_object
        leg_upper.name = f"Scarab_Leg_{i}_Upper"
        
        # Angle legs outward
        if i < 3:  # Left side
            leg_upper.rotation_euler = (math.radians(45), 0, math.radians(-45))
        else:  # Right side
            leg_upper.rotation_euler = (math.radians(45), 0, math.radians(45))
        
        legs.append(leg_upper)
        
        # Lower leg segment
        lower_pos = (
            pos[0] + (0.3 if i < 3 else -0.3),
            pos[1],
            pos[2] - 0.4
        )
        bpy.ops.mesh.primitive_cylinder_add(
            radius=0.06,
            depth=0.4,
            location=lower_pos,
            vertices=12
        )
        leg_lower = bpy.context.active_object
        leg_lower.name = f"Scarab_Leg_{i}_Lower"
        
        # Angle lower legs down
        if i < 3:  # Left side
            leg_lower.rotation_euler = (math.radians(60), 0, math.radians(-30))
        else:  # Right side
            leg_lower.rotation_euler = (math.radians(60), 0, math.radians(30))
        
        legs.append(leg_lower)
    
    # === 5. ANTENNAE ===
    # Left antenna
    bpy.ops.mesh.primitive_cylinder_add(
        radius=0.04,
        depth=0.6,
        location=(-0.2, 1.6, 0.1),
        vertices=8
    )
    left_antenna = bpy.context.active_object
    left_antenna.name = "Scarab_Left_Antenna"
    left_antenna.rotation_euler = (math.radians(-30), 0, math.radians(-20))
    
    # Right antenna
    bpy.ops.mesh.primitive_cylinder_add(
        radius=0.04,
        depth=0.6,
        location=(0.2, 1.6, 0.1),
        vertices=8
    )
    right_antenna = bpy.context.active_object
    right_antenna.name = "Scarab_Right_Antenna"
    right_antenna.rotation_euler = (math.radians(-30), 0, math.radians(20))
    
    # === 6. JOIN ALL PARTS ===
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True)
    head.select_set(True)
    left_wing.select_set(True)
    right_wing.select_set(True)
    left_antenna.select_set(True)
    right_antenna.select_set(True)
    
    for leg in legs:
        leg.select_set(True)
    
    # Set body as active
    bpy.context.view_layer.objects.active = body
    
    # Join all parts
    bpy.ops.object.join()
    scarab_mesh = bpy.context.active_object
    scarab_mesh.name = "Egyptian_Scarab"
    
    # === 7. ADD MATERIAL ===
    # Create metallic turquoise material (Egyptian faience)
    mat = bpy.data.materials.new(name="Turquoise_Faience")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    
    # Clear default nodes
    nodes.clear()
    
    # Add Principled BSDF
    bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)
    
    # Turquoise/cyan color with metallic sheen
    bsdf.inputs['Base Color'].default_value = (0.0, 0.6, 0.7, 1.0)  # Turquoise
    bsdf.inputs['Metallic'].default_value = 0.8
    bsdf.inputs['Roughness'].default_value = 0.3
    bsdf.inputs['Specular IOR Level'].default_value = 0.7
    
    # Add slight emission for magical glow
    bsdf.inputs['Emission Color'].default_value = (0.0, 0.8, 0.9, 1.0)
    bsdf.inputs['Emission Strength'].default_value = 0.2
    
    # Output node
    output = nodes.new(type='ShaderNodeOutputMaterial')
    output.location = (300, 0)
    
    # Link nodes
    links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    
    # Assign material
    if scarab_mesh.data.materials:
        scarab_mesh.data.materials[0] = mat
    else:
        scarab_mesh.data.materials.append(mat)
    
    # === 8. SMOOTH SHADING ===
    bpy.ops.object.shade_smooth()
    
    # === 9. CENTER AND SCALE ===
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    scarab_mesh.location = (0, 0, 0)
    scarab_mesh.scale = (0.6, 0.6, 0.6)
    bpy.ops.object.transform_apply(scale=True)
    
    return scarab_mesh

# Create the scarab
scarab = create_scarab()

# === 10. ADD LIGHTING ===
bpy.ops.object.light_add(type='SUN', location=(5, -5, 10))
sun = bpy.context.active_object
sun.data.energy = 2.0
sun.rotation_euler = (math.radians(45), 0, math.radians(45))

bpy.ops.object.light_add(type='AREA', location=(-3, 3, 5))
area = bpy.context.active_object
area.data.energy = 50
area.data.size = 5

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

export_path = os.path.join(script_dir, "scarab.glb")

bpy.ops.object.select_all(action='DESELECT')
scarab.select_set(True)

bpy.ops.export_scene.gltf(
    filepath=export_path,
    export_format='GLB',
    use_selection=True,
    export_materials='EXPORT'
)

print(f"✅ Egyptian Scarab model created and exported to: {export_path}")
print("✅ Ready to use in Three.js!")

