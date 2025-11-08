# ✅ Meshy AI MCP Server Registration Guide

## 🎯 Overview

This guide provides complete instructions for registering the **Meshy AI MCP Server** with **Augment Code**, enabling AI-powered 3D model generation directly from your IDE.

---

## 📦 What You Get

### Meshy AI Capabilities
- 🎨 **Text-to-3D**: Generate 3D models from text descriptions
- 🖼️ **Image-to-3D**: Convert images to 3D models
- 🎭 **Text-to-Texture**: Apply realistic textures using text prompts
- ⚙️ **Remeshing**: Optimize and clean up 3D models
- 📊 **Real-time Progress**: Stream task updates
- 💰 **Balance Tracking**: Monitor API credits

### Free Tier Benefits
- ✅ 200 credits per month
- ✅ All generation features
- ✅ High-quality outputs
- ✅ GLB/GLTF export formats

---

## 🚀 Quick Setup (3 Methods)

### Method 1: Automated Setup (Recommended)

```powershell
# Navigate to the Meshy AI directory
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai

# Run the setup script
.\setup.ps1

# Or with API key
.\setup.ps1 -ApiKey "msy_YOUR_API_KEY_HERE"
```

**What it does**:
1. ✅ Checks Python installation
2. ✅ Creates virtual environment
3. ✅ Installs dependencies
4. ✅ Creates .env file
5. ✅ Tests server configuration

---

### Method 2: Manual Setup

#### Step 1: Get API Key
1. Visit https://www.meshy.ai/
2. Sign up for free account
3. Go to API Settings
4. Copy your API key (starts with `msy_`)

#### Step 2: Install Dependencies
```powershell
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai
python -m venv .venv
.\.venv\Scripts\activate
pip install -r requirements.txt
```

#### Step 3: Configure Environment
```powershell
copy .env.example .env
notepad .env
```

Edit `.env`:
```env
MESHY_API_KEY=msy_YOUR_ACTUAL_API_KEY_HERE
MCP_PORT=8081
TASK_TIMEOUT=300
```

#### Step 4: Test Server
```powershell
python src/server.py
```

Press `Ctrl+C` to stop.

---

### Method 3: Using Pre-configured Template

Copy the configuration from `mcp-servers/augment-settings-complete.json` to your Augment settings.

---

## ⚙️ Augment Code Configuration

### Location
```
C:\Users\spare\.augment\settings.json
```

### Configuration

**Option A: Using System Python**
```json
{
  "mcpServers": {
    "meshy-ai": {
      "command": "python",
      "args": [
        "C:/Users/spare/source/repos/ga/mcp-servers/meshy-ai/src/server.py"
      ],
      "env": {
        "MESHY_API_KEY": "msy_YOUR_ACTUAL_API_KEY_HERE"
      },
      "disabled": false,
      "autoApprove": [],
      "alwaysAllow": []
    }
  }
}
```

**Option B: Using Virtual Environment Python**
```json
{
  "mcpServers": {
    "meshy-ai": {
      "command": "C:/Users/spare/source/repos/ga/mcp-servers/meshy-ai/.venv/Scripts/python.exe",
      "args": [
        "C:/Users/spare/source/repos/ga/mcp-servers/meshy-ai/src/server.py"
      ],
      "env": {
        "MESHY_API_KEY": "msy_YOUR_ACTUAL_API_KEY_HERE"
      },
      "disabled": false,
      "autoApprove": [],
      "alwaysAllow": []
    }
  }
}
```

**Important**:
- Use **forward slashes** (`/`) in paths
- Replace `msy_YOUR_ACTUAL_API_KEY_HERE` with your actual API key
- If you have other MCP servers, merge the configurations

---

## 🎨 Usage Examples

### Example 1: Generate Egyptian Ankh
```
Using Meshy AI, create a detailed Egyptian ankh with gold metallic texture and intricate hieroglyphic engravings
```

### Example 2: Generate Scarab Beetle
```
Using Meshy AI, create a turquoise scarab beetle with detailed wing covers and legs, ancient Egyptian style
```

### Example 3: Generate Pyramid
```
Using Meshy AI, create a stepped pyramid with a golden capstone and sandstone texture
```

### Example 4: Convert Image to 3D
```
Using Meshy AI, convert this image to a 3D model: https://example.com/sketch.jpg
```

### Example 5: Apply Texture
```
Using Meshy AI, apply a rusty metal texture to this model: https://example.com/model.glb
```

### Example 6: Optimize Model
```
Using Meshy AI, remesh this model to 50,000 polygons with quad topology: https://example.com/model.glb
```

---

## 🔧 Available MCP Tools

### Creation Tools
| Tool | Description | Parameters |
|------|-------------|------------|
| `create_text_to_3d_task` | Generate 3D from text | `prompt`, `mode`, `art_style`, `should_remesh` |
| `create_image_to_3d_task` | Generate 3D from image | `image_url`, `prompt`, `art_style` |
| `create_text_to_texture_task` | Apply textures | `model_url`, `object_prompt`, `style_prompt` |
| `create_remesh_task` | Optimize model | `input_task_id`, `target_polycount`, `topology` |

### Retrieval Tools
| Tool | Description |
|------|-------------|
| `retrieve_text_to_3d_task` | Get task status |
| `retrieve_image_to_3d_task` | Get task status |
| `retrieve_text_to_texture_task` | Get task status |
| `retrieve_remesh_task` | Get task status |

### Listing Tools
| Tool | Description |
|------|-------------|
| `list_text_to_3d_tasks` | List all Text-to-3D tasks |
| `list_image_to_3d_tasks` | List all Image-to-3D tasks |
| `list_text_to_texture_tasks` | List all Texture tasks |
| `list_remesh_tasks` | List all Remesh tasks |

### Streaming Tools
| Tool | Description |
|------|-------------|
| `stream_text_to_3d_task` | Real-time progress updates |
| `stream_image_to_3d_task` | Real-time progress updates |
| `stream_text_to_texture_task` | Real-time progress updates |
| `stream_remesh_task` | Real-time progress updates |

### Utility Tools
| Tool | Description |
|------|-------------|
| `get_balance` | Check API credits |

---

## 🐛 Troubleshooting

### Issue: "MESHY_API_KEY environment variable is not set"
**Solution**: 
- Check `.env` file exists and contains your API key
- Verify API key in Augment settings is correct
- Ensure no extra spaces or quotes around the key

### Issue: "Module 'mcp' not found"
**Solution**:
```powershell
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai
.\.venv\Scripts\activate
pip install -r requirements.txt
```

### Issue: "Connection refused"
**Solution**:
- Verify Python is installed: `python --version`
- Check path in Augment settings is correct
- Test server manually: `python src/server.py`

### Issue: "Invalid API key"
**Solution**:
- Verify API key starts with `msy_`
- Check for typos or missing characters
- Generate a new API key from Meshy AI dashboard

### Issue: Server starts but doesn't respond
**Solution**:
- Check firewall settings
- Verify port 8081 is not in use
- Try changing `MCP_PORT` in `.env` file

---

## 📁 File Structure

```
mcp-servers/
├── meshy-ai/                          # Meshy AI MCP Server
│   ├── .venv/                         # Virtual environment
│   ├── src/
│   │   ├── __init__.py
│   │   └── server.py                  # Main server file
│   ├── .env                           # Environment variables (create this)
│   ├── .env.example                   # Environment template
│   ├── config.json                    # MCP configuration
│   ├── requirements.txt               # Python dependencies
│   ├── setup.ps1                      # Automated setup script
│   └── README.md                      # Server documentation
├── REGISTER_MESHY_AI.md               # Registration guide
├── MESHY_AI_SETUP.md                  # Setup guide
└── augment-settings-complete.json     # Augment settings template
```

---

## 📚 Resources

### Documentation
- **Meshy AI Website**: https://www.meshy.ai/
- **Meshy AI Discover**: https://www.meshy.ai/discover (browse community models)
- **Meshy AI API Docs**: https://docs.meshy.ai/
- **MCP Server Repo**: https://github.com/pasie15/meshy-ai-mcp-server

### Support
- **Meshy AI Discord**: Join for community support
- **GitHub Issues**: Report bugs or request features
- **Augment Code**: https://www.augmentcode.com/

---

## ✅ Verification Checklist

Before using the MCP server:

- [ ] Python 3.9+ installed (`python --version`)
- [ ] Meshy AI account created
- [ ] API key obtained from https://www.meshy.ai/
- [ ] Virtual environment created
- [ ] Dependencies installed (`pip install -r requirements.txt`)
- [ ] `.env` file created with API key
- [ ] Server tested manually (`python src/server.py`)
- [ ] Augment settings updated
- [ ] Augment Code restarted

---

## 🎉 Success!

Once configured, you can:
- ✅ Generate 3D models from text descriptions
- ✅ Convert images to 3D models
- ✅ Apply realistic textures
- ✅ Optimize and clean up models
- ✅ Stream real-time progress
- ✅ Integrate with your existing workflow

**Example Workflow**:
1. Ask Augment: "Using Meshy AI, create a golden Egyptian ankh"
2. Wait for generation (1-3 minutes)
3. Download GLB file
4. Import into your React app or Blender
5. Use in BSP DOOM Explorer or other 3D visualizations

---

**Status**: ✅ **Ready to Generate Amazing 3D Models with AI!** 🚀

