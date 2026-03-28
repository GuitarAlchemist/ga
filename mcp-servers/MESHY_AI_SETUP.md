# Meshy AI MCP Server Setup Guide

## What is Meshy AI?

**Meshy AI** is an AI-powered 3D model generation service that can create high-quality 3D models from:
- **Text prompts** (Text-to-3D)
- **Images** (Image-to-3D)
- **Texture generation** (Text-to-Texture)
- **Model optimization** (Remeshing)
- **Rigging** and **Animation**

This MCP server allows you to use Meshy AI directly from Claude Code, Cursor, or any MCP client.

---

## Features

### Creation Tools
- **Text-to-3D**: Generate 3D models from text descriptions
- **Image-to-3D**: Convert images to 3D models
- **Text-to-Texture**: Apply realistic textures using text prompts
- **Remeshing**: Optimize and clean up 3D models
- **Rigging**: Auto-rig characters for animation
- **Animation**: Apply animations to rigged models

### Management Tools
- **Task Streaming**: Real-time progress updates
- **Task Retrieval**: Check status of generation tasks
- **Task Listing**: View all your tasks
- **Balance Checking**: Monitor your API credits

---

## Installation Steps

### 1. Get Meshy AI API Key

1. Go to [Meshy AI](https://www.meshy.ai/)
2. Sign up for an account (free tier available)
3. Navigate to [API Settings](https://app.meshy.ai/settings/api)
4. Copy your **API Key**

### 2. Install Node.js Dependencies

```powershell
# Navigate to the Meshy AI MCP server directory
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai

# Install dependencies
npm ci

# Build the TypeScript source
npm run build
```

### 3. Configure Environment Variables

Create a `.env` file in the `mcp-servers/meshy-ai` directory:

```env
MESHY_API_KEY=your_api_key_here
```

Optional variables:

```env
# Override API base URL (default: https://api.meshy.ai/openapi)
MESHY_API_BASE=https://api.meshy.ai/openapi

# Streaming timeout in milliseconds (default: 300000 = 5 minutes)
MESHY_STREAM_TIMEOUT_MS=300000
```

### 4. Configure Claude Code

Add the server to your project `.mcp.json`:

```json
{
  "mcpServers": {
    "meshy-ai": {
      "command": "node",
      "args": [
        "mcp-servers/meshy-ai/dist/index.js"
      ],
      "env": {
        "MESHY_API_KEY": "your_api_key_here"
      }
    }
  }
}
```

**Alternative**: Use an environment variable reference so the key is not hardcoded:

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

Then set the environment variable in PowerShell:

```powershell
[System.Environment]::SetEnvironmentVariable('MESHY_API_KEY', 'your_api_key_here', 'User')
```

### 5. Test the Server

```powershell
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai

# Quick smoke test (should print MCP initialize response)
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"0.1"}}}' | node dist/index.js
```

---

## Usage Examples

### Example 1: Generate a 3D Model from Text

```
Using Meshy AI, create a 3D model of a futuristic robot with metallic textures
```

The AI will:
1. Call `create_text_to_3d_task` with your prompt
2. Stream progress updates
3. Return the generated 3D model URL

### Example 2: Convert Image to 3D

```
Using Meshy AI, convert this image to a 3D model: https://example.com/image.jpg
```

### Example 3: Apply Textures

```
Using Meshy AI, apply realistic wood textures to this 3D model
```

### Example 4: Check Your Balance

```
Check my Meshy AI account balance
```

---

## Available Tools

### Creation Tools

1. **`create_text_to_3d_task`** -- Generate 3D model from text prompt
   - Parameters: `mode`, `prompt`, `art_style` (optional), `should_remesh` (optional)

2. **`create_image_to_3d_task`** -- Generate 3D model from image
   - Parameters: `image_url`, `prompt` (optional), `art_style` (optional)

3. **`create_text_to_texture_task`** -- Apply textures to existing model
   - Parameters: `model_url`, `object_prompt`, `style_prompt` (optional), `enable_pbr` (optional), etc.

4. **`create_remesh_task`** -- Optimize and clean up model
   - Parameters: `input_task_id`, `target_formats` (optional), `target_polycount` (optional), etc.

5. **`create_rigging_task`** -- Auto-rig a 3D character

6. **`create_animation_task`** -- Animate a rigged model
   - Parameters: `action_id` (required)

### Retrieval, Listing, and Streaming Tools

Each creation category has matching `retrieve_*`, `list_*`, and `stream_*` tools.

### Utility Tools

- **`get_balance`**: Check account credits

---

## Pricing and Limits

Meshy AI offers:
- **Free Tier**: Limited credits per month
- **Paid Plans**: More credits and faster generation
- **API Costs**: Varies by task type and quality

Check your balance regularly with `get_balance` tool.

---

## Troubleshooting

### Issue: "MESHY_API_KEY environment variable is not set"
**Solution**: Create a `.env` file with your API key, or pass it via the MCP client config `env` block.

### Issue: "Cannot find module" or build errors
**Solution**:
```powershell
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai
npm ci
npm run build
```

### Issue: "API Key Invalid"
**Solution**: Verify your API key in `.env` file or regenerate from the Meshy AI dashboard.

### Issue: "Task Timeout"
**Solution**: Increase `MESHY_STREAM_TIMEOUT_MS` in `.env` file. Complex models take longer.

### Issue: "Out of Credits"
**Solution**: Check balance with `get_balance` tool, upgrade plan, or wait for monthly credit reset.

---

## Integration with Guitar Alchemist

You can use Meshy AI to generate custom 3D assets for the Guitar Alchemist frontend:

1. Generate a model via any `create_*` tool
2. Download the GLB/GLTF output
3. Save to `ReactComponents/ga-react-components/public/models/`
4. Reference from React Three Fiber components

---

## Best Practices

### For Text-to-3D:
- Be specific and detailed in prompts
- Specify art style (realistic, cartoon, low-poly)
- Mention materials (metal, wood, stone)
- Use "preview" mode first, then "refine" if needed
- Enable `should_remesh` for cleaner geometry

### For Image-to-3D:
- Use high-quality images with clear subjects and good lighting
- Avoid cluttered backgrounds
- Enable PBR for realistic textures

### For Texturing:
- Describe materials clearly
- Mention surface properties (rough, smooth, metallic)
- Specify colors and patterns

---

## Resources

- **Meshy AI Website**: https://www.meshy.ai/
- **Meshy AI API Docs**: https://docs.meshy.ai/
- **MCP Server Source**: https://github.com/pasie15/meshy-ai-mcp-server
- **Model Context Protocol**: https://modelcontextprotocol.io/

---

## Quick Start Checklist

- [ ] Sign up for Meshy AI account
- [ ] Get API key from dashboard
- [ ] Run `npm ci && npm run build` in `mcp-servers/meshy-ai`
- [ ] Create `.env` file with API key
- [ ] Add entry to `.mcp.json`
- [ ] Test server with smoke test command
- [ ] Try generating your first 3D model

**Location**: `mcp-servers/meshy-ai`
**Runtime**: Node.js (TypeScript, compiled to `dist/`)
**Transport**: stdio (MCP standard)

