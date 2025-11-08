# Downloaded from: https://github.com/ahujasid/blender-mcp/blob/main/addon.py
# Code created by Siddharth Ahuja: www.github.com/ahujasid © 2025
# 
# INSTALLATION INSTRUCTIONS:
# 1. Open Blender
# 2. Go to Edit > Preferences > Add-ons
# 3. Click "Install..." and select this file
# 4. Enable the addon by checking the box next to "Interface: Blender MCP"
# 5. In the 3D View sidebar (press N), find the "BlenderMCP" tab
# 6. Click "Connect to MCP server"

import bpy
import mathutils
import json
import threading
import socket
import time
import requests
import tempfile
import traceback
import os
import shutil
import zipfile
from bpy.props import StringProperty, IntProperty, BoolProperty, EnumProperty
import io
from contextlib import redirect_stdout, suppress

bl_info = {
    "name": "Blender MCP",
    "author": "BlenderMCP",
    "version": (1, 2),
    "blender": (3, 0, 0),
    "location": "View3D > Sidebar > BlenderMCP",
    "description": "Connect Blender to Claude via MCP",
    "category": "Interface",
}

# NOTE: The full addon code is very long (2000+ lines)
# For the complete code, please download from:
# https://raw.githubusercontent.com/ahujasid/blender-mcp/main/addon.py
