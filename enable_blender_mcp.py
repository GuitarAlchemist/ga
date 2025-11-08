import bpy
import sys

print("Enabling Blender MCP addon...")

try:
    # Refresh addon list
    bpy.ops.preferences.addon_refresh()
    
    # Enable the addon with correct module name
    bpy.ops.preferences.addon_enable(module='blender_mcp')
    
    # Save preferences
    bpy.ops.wm.save_userpref()
    
    print("SUCCESS: Blender MCP addon enabled!")
    sys.exit(0)
    
except Exception as e:
    print(f"ERROR: {e}")
    import traceback
    traceback.print_exc()
    sys.exit(1)
