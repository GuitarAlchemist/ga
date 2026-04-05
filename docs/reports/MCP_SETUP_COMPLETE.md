# MCP Server Setup - COMPLETE ✅

## Installation Summary

### ✅ 1. UV Package Manager
- **Status**: Installed
- **Version**: 0.9.6
- **Location**: `C:\Users\spare\.local\bin`
- **Commands**: `uv`, `uvx`, `uvw`

### ✅ 2. Blender MCP Addon
- **Status**: Installed and Enabled
- **Version**: 1.2
- **Blender Version**: 4.5.3 LTS
- **Location**: `C:\Users\spare\AppData\Roaming\Blender Foundation\Blender\4.5\scripts\addons\blender_mcp_addon.py`
- **Source**: https://github.com/ahujasid/blender-mcp

### ✅ 3. Services Running
- **MongoDB**: `localhost:27017` ✅
- **Redis/Memurai**: `127.0.0.1:6379` ✅
- **Blender**: Port `9876` (after connecting in Blender)

---

## Augment MCP Server Configurations

Add these three MCP servers in your **Augment Settings** (WebStorm/VS Code):

### 1. MongoDB MCP Server

```json
{
  "name": "mongodb",
  "command": "npx",
  "args": [
    "-y",
    "@modelcontextprotocol/server-mongodb",
    "mongodb://localhost:27017"
  ]
}
```

**What it does**: Query MongoDB databases, inspect schemas, explore collections

---

### 2. Redis MCP Server

```json
{
  "name": "redis",
  "command": "npx",
  "args": [
    "-y",
    "redis-mcp-server",
    "--url",
    "redis://127.0.0.1:6379"
  ]
}
```

**What it does**: Access Redis cache, get/set keys, inspect data

---

### 3. Blender MCP Server

```json
{
  "name": "blender",
  "command": "uvx",
  "args": [
    "blender-mcp"
  ]
}
```

**Optional environment variables** (only if needed):
```json
{
  "env": {
    "BLENDER_HOST": "localhost",
    "BLENDER_PORT": "9876"
  }
}
```

**What it does**: Create 3D models, manipulate scenes, generate meshes, apply materials

---

## How to Use Blender MCP

### In Blender (ALREADY OPENED FOR YOU):

1. **Press `N`** in the 3D viewport to open the sidebar
2. **Find** the "BlenderMCP" tab
3. **Optional**: Check "Use assets from Poly Haven" for free 3D assets
4. **Optional**: Check "Use Hyper3D Rodin" for AI-generated 3D models
5. **Click**: "Connect to MCP server"

### In Augment:

Once all three MCP servers are configured and Blender is connected, you can ask me to:

**MongoDB Examples:**
- "List all databases in MongoDB"
- "Show me the collections in the guitaralchemist database"
- "Query the chords collection"

**Redis Examples:**
- "Show me all keys in Redis"
- "Get the value of key 'user:123'"
- "Set a test key in Redis"

**Blender Examples:**
- "Create a low poly dungeon scene with a dragon"
- "Make a beach scene with realistic lighting"
- "Generate a 3D model of a garden gnome"
- "Create a sphere and make it metallic red"

---

## Testing the Setup

### Test MongoDB:
```
Ask me: "List all MongoDB databases"
```

### Test Redis:
```
Ask me: "Show Redis info"
```

### Test Blender:
```
Ask me: "Create a simple cube in Blender and make it blue"
```

---

## Troubleshooting

### MongoDB not connecting:
- Check if MongoDB is running: `netstat -ano | findstr :27017`
- Try using `127.0.0.1` instead of `localhost`

### Redis not connecting:
- Check if Redis/Memurai is running: `netstat -ano | findstr :6379`
- Verify the URL format: `redis://127.0.0.1:6379`

### Blender not connecting:
- Make sure you clicked "Connect to MCP server" in Blender
- Check the BlenderMCP tab shows "Running on port 9876"
- Restart Blender if needed

### MCP servers not showing in Augment:
- Restart Augment/WebStorm
- Check the MCP server configuration syntax
- Look for errors in the Augment console

---

## Files Created

- `C:\Users\spare\source\repos\ga\blender_mcp_addon_full.py` - Full Blender addon (79KB)
- `C:\Users\spare\source\repos\ga\enable_blender_addon.py` - Auto-enable script
- `C:\Users\spare\source\repos\ga\MCP_SETUP_COMPLETE.md` - This file

---

## Next Steps

1. ✅ **Blender is now open** - Press `N` and find the "BlenderMCP" tab
2. ⏳ **Click "Connect to MCP server"** in Blender
3. ⏳ **Add the three MCP servers** to Augment settings
4. ⏳ **Restart Augment** to load the new MCP servers
5. ✅ **Test with me!**

---

## Resources

- **Blender MCP GitHub**: https://github.com/ahujasid/blender-mcp
- **MCP Documentation**: https://modelcontextprotocol.io/
- **Poly Haven** (free 3D assets): https://polyhaven.com/
- **Hyper3D Rodin** (AI 3D generation): https://hyper3d.ai/

---

**Setup completed on**: 2025-10-30
**Blender version**: 4.5.3 LTS
**UV version**: 0.9.6

🎉 **Everything is ready! Just connect Blender and configure the MCP servers in Augment!**
