# Register Meshy AI MCP Server with Claude Code

## Quick Start Guide

This guide registers the Meshy AI MCP server with Claude Code so you can generate 3D models using AI directly from your IDE.

---

## Prerequisites

- Node.js 18+ installed
- Meshy AI account (free tier available)
- Claude Code (or another MCP-compatible client)

---

## Step-by-Step Setup

### Step 1: Get Your Meshy AI API Key

1. **Visit Meshy AI**: https://www.meshy.ai/
2. **Sign up** for a free account (or log in)
3. **Navigate to API Settings**: https://app.meshy.ai/settings/api
4. **Create/Copy your API Key**

**Free Tier Includes**:
- 200 credits per month
- Text-to-3D, Image-to-3D, Texture, Remeshing, Rigging, Animation

---

### Step 2: Install and Build

Open a terminal and run:

```powershell
# Navigate to the Meshy AI MCP server directory
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai

# Install dependencies
npm ci

# Build TypeScript to dist/
npm run build
```

**Expected output** (build):
```
> meshy-ai-mcp-server@1.0.0 build
> tsc
```

---

### Step 3: Configure Environment Variables

Create a `.env` file in the `mcp-servers/meshy-ai` directory:

```env
MESHY_API_KEY=msy_YOUR_ACTUAL_API_KEY_HERE
```

**Replace** `msy_YOUR_ACTUAL_API_KEY_HERE` with your actual API key from Step 1.

Optional variables:

```env
# Override API base URL (default: https://api.meshy.ai/openapi)
MESHY_API_BASE=https://api.meshy.ai/openapi

# Streaming timeout in milliseconds (default: 300000 = 5 minutes)
MESHY_STREAM_TIMEOUT_MS=300000
```

---

### Step 4: Test the MCP Server

Before registering with Claude Code, verify the server starts:

```powershell
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai

# Smoke test -- should print a JSON-RPC initialize response
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"0.1"}}}' | node dist/index.js
```

**Expected**: A JSON response containing `"serverInfo":{"name":"Meshy AI MCP Server (Node)","version":"1.0.0"}`.

---

### Step 5: Register with Claude Code

Add a `meshy-ai` entry to your project's `.mcp.json` file (at the repo root):

```json
{
  "mcpServers": {
    "meshy-ai": {
      "command": "node",
      "args": [
        "mcp-servers/meshy-ai/dist/index.js"
      ],
      "env": {
        "MESHY_API_KEY": "msy_YOUR_ACTUAL_API_KEY_HERE"
      }
    }
  }
}
```

**Important Notes**:
- Use **forward slashes** (`/`) in the path
- Replace `msy_YOUR_ACTUAL_API_KEY_HERE` with your actual API key
- If you already have other MCP servers in `.mcp.json`, add `"meshy-ai": { ... }` to the existing `"mcpServers"` object

**Alternative**: Reference an environment variable instead of hardcoding the key:

```json
{
  "mcpServers": {
    "meshy-ai": {
      "command": "node",
      "args": [
        "mcp-servers/meshy-ai/dist/index.js"
      ],
      "env": {
        "MESHY_API_KEY": "${MESHY_API_KEY}"
      }
    }
  }
}
```

Then set the variable:

```powershell
[System.Environment]::SetEnvironmentVariable('MESHY_API_KEY', 'msy_YOUR_KEY', 'User')
```

---

### Step 6: Restart Claude Code

1. Close Claude Code completely
2. Reopen Claude Code
3. The Meshy AI MCP server tools should now be available

---

## Usage Examples

Once registered, you can use Meshy AI directly from Claude Code:

### Generate a 3D Model from Text

```
Using Meshy AI, create a detailed Egyptian ankh with gold metallic texture
```

### Convert Image to 3D

```
Using Meshy AI, convert this image to a 3D model: https://example.com/image.jpg
```

### Apply Texture to Existing Model

```
Using Meshy AI, apply a rusty metal texture to this 3D model: https://example.com/model.glb
```

### Optimize a Model

```
Using Meshy AI, remesh this model to 50,000 polygons: https://example.com/model.glb
```

---

## Available Tools

The Meshy AI MCP server provides these tools:

### Creation Tools
- `create_text_to_3d_task` -- Generate 3D model from text
- `create_image_to_3d_task` -- Generate 3D model from image
- `create_text_to_texture_task` -- Apply textures using text
- `create_remesh_task` -- Optimize and clean up models
- `create_rigging_task` -- Auto-rig a 3D character
- `create_animation_task` -- Animate a rigged model

### Management Tools
- `retrieve_*_task` -- Check task status
- `list_*_tasks` -- List all tasks
- `stream_*_task` -- Real-time progress updates
- `get_balance` -- Check API credits

---

## Troubleshooting

### Issue: "MESHY_API_KEY environment variable is not set"

**Solution**: Make sure you have created the `.env` file with your API key, or that the key is in the `.mcp.json` env block.

### Issue: "Cannot find module" or TypeScript errors

**Solution**: Rebuild the project:
```powershell
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai
npm ci
npm run build
```

### Issue: "Connection refused" or "Server not responding"

**Solution**:
1. Check that Node.js 18+ is installed: `node --version`
2. Verify the path in `.mcp.json` is correct
3. Test the server manually with the smoke test in Step 4

### Issue: "Invalid API key"

**Solution**:
1. Verify your API key is correct
2. Check that you copied the entire key (starts with `msy_`)
3. Make sure there are no extra spaces

---

## Resources

- **Meshy AI Website**: https://www.meshy.ai/
- **Meshy AI Discover**: https://www.meshy.ai/discover
- **Meshy AI API Docs**: https://docs.meshy.ai/
- **MCP Server Repo**: https://github.com/pasie15/meshy-ai-mcp-server
- **Model Context Protocol**: https://modelcontextprotocol.io/

---

## Verification Checklist

- [ ] Node.js 18+ installed
- [ ] Meshy AI account created
- [ ] API key obtained from Meshy AI
- [ ] Dependencies installed (`npm ci`)
- [ ] TypeScript built (`npm run build`)
- [ ] `.env` file created with API key
- [ ] Server tested with smoke test
- [ ] `.mcp.json` updated with meshy-ai entry
- [ ] Claude Code restarted

**Runtime**: Node.js (TypeScript, compiled to `dist/index.js`)
**Transport**: stdio (MCP standard)

