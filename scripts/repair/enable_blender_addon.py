import bpy
import sys

print("Attempting to enable Blender MCP addon...")

try:
    # Refresh addon list
    bpy.ops.preferences.addon_refresh()
    
    # Enable the addon
    bpy.ops.preferences.addon_enable(module='blender_mcp_addon')
    
    # Save preferences
    bpy.ops.wm.save_userpref()
    
    print("SUCCESS: Blender MCP addon enabled and preferences saved!")
    sys.exit(0)
    
except Exception as e:
    print(f"ERROR: Failed to enable addon: {e}")
    import traceback
    traceback.print_exc()
    sys.exit(1)
