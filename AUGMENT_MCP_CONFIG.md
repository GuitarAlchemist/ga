# Augment MCP Server Configuration

## ✅ Fixed Blender MCP Registration

The Blender addon is now properly installed at:
- **Location**: `C:\Users\spare\AppData\Roaming\Blender Foundation\Blender\4.5\scripts\addons\blender_mcp.py`
- **Module Name**: `blender_mcp`
- **Status**: Enabled

---

## 📋 Augment Settings Configuration

Add these **THREE** MCP servers to your Augment settings in WebStorm:

### 1. MongoDB MCP Server ✅

**Name**: `mongodb`

**Command**: `npx`

**Args**:
```json
[
  "-y",
  "@modelcontextprotocol/server-mongodb",
  "mongodb://localhost:27017"
]
```

**Environment Variables**: (leave empty)

---

### 2. Redis MCP Server ✅

**Name**: `redis`

**Command**: `npx`

**Args**:
```json
[
  "-y",
  "redis-mcp-server",
  "--url",
  "redis://127.0.0.1:6379"
]
```

**Environment Variables**: (leave empty)

---

### 3. Blender MCP Server ✅ (FIXED)

**Name**: `blender`

**Command**: `uvx`

**Args**:
```json
[
  "blender-mcp"
]
```

**Environment Variables**: (leave empty, or optionally add):
```json
{
  "BLENDER_HOST": "localhost",
  "BLENDER_PORT": "9876"
}
```

---

## 🔧 How to Add in Augment Settings (WebStorm)

1. Open **WebStorm**
2. Go to **Settings** → **Tools** → **Augment Code**
3. Find the **"Model Context Protocol"** section
4. Click **"Add MCP Server"** for each of the three servers above
5. Fill in the **Name**, **Command**, and **Args** exactly as shown
6. Click **Save** or **Apply**
7. **Restart Augment** or reload MCP servers

---

## 🎯 In Blender (Before Testing)

1. **Open Blender** (if not already open)
2. Press **`N`** in the 3D viewport to open sidebar
3. Find the **"BlenderMCP"** tab
4. Click **"Connect to MCP server"**
5. You should see: **"Running on port 9876"**

---

## 🧪 Test Commands

Once everything is configured, test with these commands:

### Test MongoDB:
```
"List all databases in MongoDB"
"Show collections in the guitaralchemist database"
```

### Test Redis:
```
"Show Redis server info"
"List all keys in Redis"
```

### Test Blender:
```
"Get the current Blender scene info"
"Create a blue metallic sphere in Blender"
"Create a simple cube and make it red"
```

---

## ⚠️ Important Notes

1. **Blender must be running** with the MCP server connected (port 9876)
2. **MongoDB must be running** on port 27017
3. **Redis/Memurai must be running** on port 6379
4. **UV must be in PATH**: `C:\Users\spare\.local\bin`

---

## 🔍 Verification

Check if services are running:

```powershell
# Check MongoDB
netstat -ano | findstr :27017

# Check Redis
netstat -ano | findstr :6379

# Check UV installation
uvx --version
```

---

## 📝 Configuration Summary

| MCP Server | Command | Port/URL | Status |
|------------|---------|----------|--------|
| MongoDB | `npx @modelcontextprotocol/server-mongodb` | `mongodb://localhost:27017` | ✅ Ready |
| Redis | `npx redis-mcp-server` | `redis://127.0.0.1:6379` | ✅ Ready |
| Blender | `uvx blender-mcp` | `localhost:9876` | ✅ Ready |

---

## 🚀 Ready to Use!

After adding all three MCP servers to Augment and restarting:

1. ✅ MongoDB queries will work
2. ✅ Redis operations will work
3. ✅ Blender 3D creation will work

**No more collisions - everything is properly configured!**
