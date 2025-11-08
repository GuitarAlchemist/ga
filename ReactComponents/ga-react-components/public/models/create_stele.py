"""
Blender script to create a detailed Egyptian Stele (stone monument) for BSP DOOM Explorer
Run this in Blender's scripting workspace or via command line:
blender --background --python create_stele.py
"""

import bpy
import math
import random

# Clear existing objects
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

def create_hieroglyphic_pattern(obj, num_glyphs=20):
    """Add hieroglyphic-like patterns using displacement"""
    # Add array of small indentations to simulate hieroglyphics
    for i in range(num_glyphs):
        # Random position on front face
        x = random.uniform(-0.3, 0.3)
        y = random.uniform(-0.8, 0.8)
        z = 0.52  # Just in front of the stele
        
        # Create small cube for glyph
        bpy.ops.mesh.primitive_cube_add(
            size=0.08,
            location=(x, y, z)
        )
        glyph = bpy.context.active_object
        glyph.name = f"Glyph_{i}"
        
        # Random rotation for variety
        glyph.rotation_euler = (0, 0, random.uniform(0, math.pi/4))
        
        # Scale to make it more like a hieroglyph
        glyph.scale = (random.uniform(0.5, 1.5), random.uniform(0.8, 1.2), 0.3)

def create_stele():
    """Create a detailed Egyptian stele model"""
    
    # === 1. MAIN STONE BODY ===
    # Create rounded rectangular stone tablet
    bpy.ops.mesh.primitive_cube_add(
        size=2,
        location=(0, 0, 0)
    )
    stele_body = bpy.context.active_object
    stele_body.name = "Stele_Body"
    
    # Scale to make it a tall tablet
    stele_body.scale = (0.4, 1.0, 0.5)
    bpy.ops.object.transform_apply(scale=True)
    
    # Add subdivision for smooth surface
    subdiv_mod = stele_body.modifiers.new(name="Subdivision", type='SUBSURF')
    subdiv_mod.levels = 2
    subdiv_mod.render_levels = 3
    
    # Add bevel for rounded edges
    bevel_mod = stele_body.modifiers.new(name="Bevel", type='BEVEL')
    bevel_mod.width = 0.05
    bevel_mod.segments = 4
    
    # === 2. ROUNDED TOP ===
    # Create cylinder for rounded top
    bpy.ops.mesh.primitive_cylinder_add(
        radius=0.4,
        depth=0.5,
        location=(0, 1.25, 0),
        rotation=(math.pi/2, 0, 0),
        vertices=32
    )
    top_round = bpy.context.active_object
    top_round.name = "Stele_Top"
    
    # Scale to match width
    top_round.scale = (1.0, 1.0, 1.0)
    
    # === 3. BASE PLATFORM ===
    # Create stepped base
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=(0, -1.2, 0)
    )
    base = bpy.context.active_object
    base.name = "Stele_Base"
    base.scale = (0.5, 0.15, 0.6)
    bpy.ops.object.transform_apply(scale=True)
    
    # === 4. HIEROGLYPHIC PATTERNS ===
    # Create hieroglyphic-like indentations
    create_hieroglyphic_pattern(stele_body, num_glyphs=25)
    
    # === 5. JOIN ALL PARTS ===
    # Select all stele objects
    bpy.ops.object.select_all(action='DESELECT')
    stele_body.select_set(True)
    top_round.select_set(True)
    base.select_set(True)
    
    # Select all glyphs
    for obj in bpy.data.objects:
        if obj.name.startswith("Glyph_"):
            obj.select_set(True)
    
    # Set the body as active object
    bpy.context.view_layer.objects.active = stele_body
    
    # Join all parts into one mesh
    bpy.ops.object.join()
    stele_mesh = bpy.context.active_object
    stele_mesh.name = "Egyptian_Stele"
    
    # === 6. ADD MATERIAL ===
    # Create sandstone material
    mat = bpy.data.materials.new(name="Sandstone_Material")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    
    # Clear default nodes
    nodes.clear()
    
    # Add Principled BSDF
    bsdf = nodes.new(type='ShaderNodeBsdfPrincipled')
    bsdf.location = (0, 0)
    
    # Sandstone color and properties
    bsdf.inputs['Base Color'].default_value = (0.76, 0.70, 0.50, 1.0)  # Sandy beige
    bsdf.inputs['Metallic'].default_value = 0.0
    bsdf.inputs['Roughness'].default_value = 0.9  # Very rough stone
    bsdf.inputs['Specular IOR Level'].default_value = 0.3
    
    # Add noise texture for surface variation
    noise_tex = nodes.new(type='ShaderNodeTexNoise')
    noise_tex.location = (-400, 0)
    noise_tex.inputs['Scale'].default_value = 5.0
    noise_tex.inputs['Detail'].default_value = 8.0
    noise_tex.inputs['Roughness'].default_value = 0.6
    
    # Color ramp for noise
    color_ramp = nodes.new(type='ShaderNodeValToRGB')
    color_ramp.location = (-200, 0)
    color_ramp.color_ramp.elements[0].color = (0.65, 0.60, 0.45, 1.0)  # Darker sand
    color_ramp.color_ramp.elements[1].color = (0.85, 0.78, 0.55, 1.0)  # Lighter sand
    
    # Output node
    output = nodes.new(type='ShaderNodeOutputMaterial')
    output.location = (300, 0)
    
    # Link nodes
    links.new(noise_tex.outputs['Fac'], color_ramp.inputs['Fac'])
    links.new(color_ramp.outputs['Color'], bsdf.inputs['Base Color'])
    links.new(bsdf.outputs['BSDF'], output.inputs['Surface'])
    
    # Assign material to object
    if stele_mesh.data.materials:
        stele_mesh.data.materials[0] = mat
    else:
        stele_mesh.data.materials.append(mat)
    
    # === 7. SMOOTH SHADING ===
    bpy.ops.object.shade_smooth()
    
    # === 8. CENTER AND SCALE ===
    # Center the stele at origin
    bpy.ops.object.origin_set(type='ORIGIN_GEOMETRY', center='BOUNDS')
    stele_mesh.location = (0, 0, 0)
    
    # Scale to reasonable size (about 3 units tall)
    stele_mesh.scale = (0.8, 0.8, 0.8)
    bpy.ops.object.transform_apply(scale=True)
    
    return stele_mesh

# Create the stele
stele = create_stele()

# === 9. ADD LIGHTING ===
# Add key light (sun)
bpy.ops.object.light_add(type='SUN', location=(5, -5, 10))
sun = bpy.context.active_object
sun.data.energy = 2.0
sun.rotation_euler = (math.radians(45), 0, math.radians(45))

# Add fill light (area)
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
    # If not saved, use the current directory
    script_dir = os.path.dirname(os.path.abspath(__file__))

# Export path
export_path = os.path.join(script_dir, "stele.glb")

# Select only the stele for export
bpy.ops.object.select_all(action='DESELECT')
stele.select_set(True)

# Export as GLB
bpy.ops.export_scene.gltf(
    filepath=export_path,
    export_format='GLB',
    use_selection=True,
    export_materials='EXPORT'
)

print(f"✅ Egyptian Stele model created and exported to: {export_path}")
print("✅ Ready to use in Three.js!")

