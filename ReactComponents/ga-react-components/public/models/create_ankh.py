"""
Blender script to create a detailed 3D Ankh model for BSP DOOM Explorer
Run this in Blender's scripting workspace or via command line
"""

import bpy
import math

# Clear existing objects
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

def create_ankh():
    """Create a detailed 3D ankh model"""

    # === 1. TOP LOOP (Circle/Oval) ===
    # Create a torus for the loop with nice proportions
    bpy.ops.mesh.primitive_torus_add(
        align='WORLD',
        location=(0, 0, 2.5),
        rotation=(0, 0, 0),
        major_radius=0.8,
        minor_radius=0.15,
        major_segments=48,
        minor_segments=24
    )
    loop = bpy.context.active_object
    loop.name = "Ankh_Loop"
    
    # === 2. VERTICAL STAFF ===
    # Create cylinder for the main vertical staff
    bpy.ops.mesh.primitive_cylinder_add(
        radius=0.12,
        depth=3.0,
        location=(0, 0, 0.5),
        vertices=32
    )
    staff = bpy.context.active_object
    staff.name = "Ankh_Staff"
    
    # Add bevel to staff for smoother edges
    bevel_mod = staff.modifiers.new(name="Bevel", type='BEVEL')
    bevel_mod.width = 0.02
    bevel_mod.segments = 3
    
    # === 3. HORIZONTAL ARMS ===
    # Create cylinder for horizontal crossbar
    bpy.ops.mesh.primitive_cylinder_add(
        radius=0.12,
        depth=2.0,
        location=(0, 0, 1.5),
        rotation=(0, math.pi/2, 0),
        vertices=32
    )
    arms = bpy.context.active_object
    arms.name = "Ankh_Arms"
    
    # Add bevel to arms
    bevel_mod = arms.modifiers.new(name="Bevel", type='BEVEL')
    bevel_mod.width = 0.02
    bevel_mod.segments = 3
    
    # === 4. DECORATIVE DETAILS ===
    # Add small spheres at the ends of the arms for decoration
    arm_spheres = []
    for x_pos in [-1.0, 1.0]:
        bpy.ops.mesh.primitive_uv_sphere_add(
            radius=0.15,
            location=(x_pos, 0, 1.5),
            segments=24,
            ring_count=16
        )
        sphere = bpy.context.active_object
        sphere.name = f"Ankh_Arm_End_{x_pos}"
        arm_spheres.append(sphere)

    # Add sphere at bottom of staff
    bpy.ops.mesh.primitive_uv_sphere_add(
        radius=0.15,
        location=(0, 0, -0.95),
        segments=24,
        ring_count=16
    )
    bottom_sphere = bpy.context.active_object
    bottom_sphere.name = "Ankh_Bottom"

    # === 5. JOIN ALL PARTS ===
    # Select all ankh objects
    bpy.ops.object.select_all(action='DESELECT')
    loop.select_set(True)
    staff.select_set(True)
    arms.select_set(True)
    for sphere in arm_spheres:
        sphere.select_set(True)
    bottom_sphere.select_set(True)
    
    # Set the staff as active object
    bpy.context.view_layer.objects.active = staff
    
    # Join all parts into one mesh
    bpy.ops.object.join()
    ankh_mesh = bpy.context.active_object
    ankh_mesh.name = "Ankh"
    
    # === 6. ADD MATERIAL ===
    # Create gold material
    mat = bpy.data.materials.new(name="Gold_Material")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    
    # Clear default nodes
    nodes.clear()
    
    # Add Principled BSDF
    bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)
    
    # Gold color and properties
    bsdf.inputs['Base Color'].default_value = (0.944, 0.776, 0.373, 1.0)  # Gold color
    bsdf.inputs['Metallic'].default_value = 1.0
    bsdf.inputs['Roughness'].default_value = 0.2
    bsdf.inputs['Specular IOR Level'].default_value = 0.5
    
    # Add emission for glow effect
    bsdf.inputs['Emission Color'].default_value = (0.944, 0.776, 0.373, 1.0)
    bsdf.inputs['Emission Strength'].default_value = 0.3
    
    # Output node
    output = nodes.new(type='ShaderNodeOutputMaterial')
    output.location = (300, 0)
    
    # Link nodes
    links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    
    # Assign material to object
    if ankh_mesh.data.materials:
        ankh_mesh.data.materials[0] = mat
    else:
        ankh_mesh.data.materials.append(mat)
    
    # === 7. SMOOTH SHADING ===
    bpy.ops.object.shade_smooth()
    
    # === 8. CENTER AND SCALE ===
    # Center the ankh at origin
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    ankh_mesh.location = (0, 0, 0)
    
    # Scale to reasonable size (about 2 units tall)
    ankh_mesh.scale = (0.5, 0.5, 0.5)
    bpy.ops.object.transform_apply(scale=True)
    
    return ankh_mesh

# Create the ankh
ankh = create_ankh()

# === 9. ADD LIGHTING ===
# Add key light
bpy.ops.object.light_add(type='SUN', location=(5, -5, 10))
sun = bpy.context.active_object
sun.data.energy = 2.0
sun.rotation_euler = (math.radians(45), 0, math.radians(45))

# Add fill light
bpy.ops.object.light_add(type='AREA', location=(-3, 3, 5))
area = bpy.context.active_object
area.data.energy = 50
area.data.size = 5

# === 10. SETUP CAMERA ===
bpy.ops.object.camera_add(location=(4, -4, 3))
camera = bpy.context.active_object
camera.rotation_euler = (math.radians(65), 0, math.radians(45))
bpy.context.scene.camera = camera

# === 11. EXPORT AS GLB ===
import os

# Get the script directory
script_dir = bpy.path.abspath("//")
if not script_dir:
    # If not saved, use a default path
    script_dir = os.path.expanduser("~/Desktop/")

# Export path
export_path = os.path.join(script_dir, "ankh.glb")

# Select only the ankh for export
bpy.ops.object.select_all(action='DESELECT')
ankh.select_set(True)

# Export as GLB
bpy.ops.export_scene.gltf(
    filepath=export_path,
    export_format='GLB',
    use_selection=True,
    export_materials='EXPORT'
)

print(f"✅ Ankh model created and exported to: {export_path}")
print("✅ Ready to use in Three.js!")

