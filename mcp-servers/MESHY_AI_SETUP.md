# Meshy AI MCP Server Setup Guide

## 🎨 **What is Meshy AI?**

**Meshy AI** is a powerful AI-powered 3D model generation service that can create high-quality 3D models from:
- **Text prompts** (Text-to-3D)
- **Images** (Image-to-3D)
- **Texture generation** (Text-to-Texture)
- **Model optimization** (Remeshing)

This MCP server allows you to use Meshy AI directly from Augment Code!

---

## 📋 **Features**

### Creation Tools
- ✅ **Text-to-3D**: Generate 3D models from text descriptions
- ✅ **Image-to-3D**: Convert images to 3D models
- ✅ **Text-to-Texture**: Apply realistic textures using text prompts
- ✅ **Remeshing**: Optimize and clean up 3D models

### Management Tools
- ✅ **Task Streaming**: Real-time progress updates
- ✅ **Task Retrieval**: Check status of generation tasks
- ✅ **Task Listing**: View all your tasks
- ✅ **Balance Checking**: Monitor your API credits

---

## 🚀 **Installation Steps**

### 1. **Get Meshy AI API Key**

1. Go to [Meshy AI](https://www.meshy.ai/)
2. Sign up for an account (free tier available!)
3. Navigate to **API Settings** or **Developer Settings**
4. Copy your **API Key**

### 2. **Install Python Dependencies**

```powershell
# Navigate to the Meshy AI MCP server directory
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai

# Create virtual environment (recommended)
python -m venv .venv

# Activate virtual environment
.\.venv\Scripts\activate

# Install MCP package
pip install mcp

# Install dependencies
pip install -r requirements.txt
```

### 3. **Configure Environment Variables**

Create a `.env` file in the `mcp-servers/meshy-ai` directory:

```bash
# Copy the example file
cp .env.example .env
```

Edit `.env` and add your API key:

```env
MESHY_API_KEY=your_api_key_here
MCP_PORT=8081
TASK_TIMEOUT=300
```

### 4. **Configure Augment Code**

Edit your Augment settings file at:
`C:\Users\spare\.augment\settings.json`

Add the Meshy AI MCP server configuration:

```json
{
  "mcpServers": {
    "meshy-ai": {
      "command": "python",
      "args": [
        "C:/Users/spare/source/repos/ga/mcp-servers/meshy-ai/src/server.py"
      ],
      "env": {
        "MESHY_API_KEY": "your_api_key_here"
      }
    }
  }
}
```

**Alternative**: Use environment variable reference:

```json
{
  "mcpServers": {
    "meshy-ai": {
      "command": "python",
      "args": [
        "C:/Users/spare/source/repos/ga/mcp-servers/meshy-ai/src/server.py"
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

### 5. **Test the Server**

```powershell
# Navigate to the server directory
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai

# Activate virtual environment
.\.venv\Scripts\activate

# Test the server
python src/server.py
```

Or use MCP dev mode for debugging:

```powershell
mcp dev src/server.py
```

This will start the MCP Inspector at `http://127.0.0.1:6274`

---

## 🎯 **Usage Examples**

### Example 1: Generate a 3D Model from Text

```
"Using Meshy AI, create a 3D model of a futuristic robot with metallic textures"
```

The AI will:
1. Call `create_text_to_3d_task` with your prompt
2. Stream progress updates
3. Return the generated 3D model URL
4. Optionally download and save the model

### Example 2: Convert Image to 3D

```
"Using Meshy AI, convert this image [attach image] to a 3D model"
```

### Example 3: Apply Textures

```
"Using Meshy AI, apply realistic wood textures to this 3D model"
```

### Example 4: Check Your Balance

```
"Check my Meshy AI account balance"
```

---

## 🛠️ **Available Tools**

### Creation Tools

1. **`create_text_to_3d_task`**
   - Generate 3D model from text prompt
   - Parameters:
     - `mode`: "preview" or "refine"
     - `prompt`: Text description
     - `art_style`: "realistic", "cartoon", "low-poly", etc.
     - `should_remesh`: Auto-optimize the model

2. **`create_image_to_3d_task`**
   - Generate 3D model from image
   - Parameters:
     - `image_url`: URL to the image
     - `enable_pbr`: Enable PBR textures

3. **`create_text_to_texture_task`**
   - Apply textures to existing model
   - Parameters:
     - `model_url`: URL to the 3D model
     - `prompt`: Texture description

4. **`create_remesh_task`**
   - Optimize and clean up model
   - Parameters:
     - `model_url`: URL to the 3D model
     - `target_triangle_count`: Desired polygon count

### Retrieval Tools

- **`retrieve_text_to_3d_task`**: Get task status
- **`retrieve_image_to_3d_task`**: Get task status
- **`retrieve_text_to_texture_task`**: Get task status
- **`retrieve_remesh_task`**: Get task status

### Streaming Tools

- **`stream_text_to_3d_task`**: Real-time progress updates
- **`stream_image_to_3d_task`**: Real-time progress updates
- **`stream_text_to_texture_task`**: Real-time progress updates
- **`stream_remesh_task`**: Real-time progress updates

### Listing Tools

- **`list_text_to_3d_tasks`**: List all text-to-3D tasks
- **`list_image_to_3d_tasks`**: List all image-to-3D tasks
- **`list_text_to_texture_tasks`**: List all texture tasks
- **`list_remesh_tasks`**: List all remesh tasks

### Utility Tools

- **`get_balance`**: Check account credits

---

## 📊 **Pricing & Limits**

Meshy AI offers:
- **Free Tier**: Limited credits per month
- **Paid Plans**: More credits and faster generation
- **API Costs**: Varies by task type and quality

Check your balance regularly with `get_balance` tool!

---

## 🔧 **Troubleshooting**

### Issue: "API Key Invalid"
**Solution**: 
- Verify your API key in `.env` file
- Check that the key is correctly set in Augment settings
- Regenerate API key from Meshy AI dashboard

### Issue: "Task Timeout"
**Solution**:
- Increase `TASK_TIMEOUT` in `.env` file
- Complex models take longer to generate
- Check your internet connection

### Issue: "Server Not Starting"
**Solution**:
- Ensure Python is installed and in PATH
- Activate virtual environment
- Install all dependencies: `pip install -r requirements.txt`
- Check for port conflicts (default: 8081)

### Issue: "Out of Credits"
**Solution**:
- Check balance with `get_balance` tool
- Upgrade your Meshy AI plan
- Wait for monthly credit reset (free tier)

---

## 🎨 **Integration with BSP DOOM Explorer**

You can use Meshy AI to generate custom 3D assets for your BSP DOOM Explorer:

### Example Workflow:

1. **Generate Ankh Model**:
   ```
   "Using Meshy AI, create a detailed Egyptian ankh with gold metallic texture"
   ```

2. **Generate Stele Models**:
   ```
   "Using Meshy AI, create an ancient Egyptian stone stele with hieroglyphics"
   ```

3. **Generate Floor Decorations**:
   ```
   "Using Meshy AI, create a low-poly Egyptian floor tile pattern"
   ```

4. **Download and Import**:
   - Models are returned as GLB/GLTF files
   - Save to `ReactComponents/ga-react-components/public/models/`
   - Update your React components to load the new models

---

## 📝 **Best Practices**

### For Text-to-3D:
- ✅ Be specific and detailed in prompts
- ✅ Specify art style (realistic, cartoon, low-poly)
- ✅ Mention materials (metal, wood, stone)
- ✅ Use "preview" mode first, then "refine" if needed
- ✅ Enable `should_remesh` for cleaner geometry

### For Image-to-3D:
- ✅ Use high-quality images
- ✅ Clear subject with good lighting
- ✅ Avoid cluttered backgrounds
- ✅ Enable PBR for realistic textures

### For Texturing:
- ✅ Describe materials clearly
- ✅ Mention surface properties (rough, smooth, metallic)
- ✅ Specify colors and patterns

---

## 🔗 **Resources**

- **Meshy AI Website**: https://www.meshy.ai/
- **Meshy AI API Docs**: https://docs.meshy.ai/
- **MCP Server GitHub**: https://github.com/pasie15/meshy-ai-mcp-server
- **Model Context Protocol**: https://modelcontextprotocol.io/

---

## ✅ **Quick Start Checklist**

- [ ] Sign up for Meshy AI account
- [ ] Get API key from dashboard
- [ ] Clone MCP server (already done!)
- [ ] Install Python dependencies
- [ ] Create `.env` file with API key
- [ ] Configure Augment settings
- [ ] Test server with `mcp dev`
- [ ] Try generating your first 3D model!

---

**Status**: ✅ **Server Cloned and Ready for Setup**  
**Location**: `C:/Users/spare/source/repos/ga/mcp-servers/meshy-ai`  
**Next Step**: Get your Meshy AI API key and configure!

