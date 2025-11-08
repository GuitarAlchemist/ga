# Register Meshy AI MCP Server with Augment Code

## 🎯 Quick Start Guide

This guide will help you register the Meshy AI MCP server with Augment Code so you can generate 3D models using AI directly from your IDE.

---

## 📋 Prerequisites

- ✅ Python 3.9+ installed
- ✅ Meshy AI account (free tier available)
- ✅ Augment Code installed

---

## 🚀 Step-by-Step Setup

### Step 1: Get Your Meshy AI API Key

1. **Visit Meshy AI**: https://www.meshy.ai/
2. **Sign up** for a free account (or log in)
3. **Navigate to API Settings**:
   - Click on your profile icon (top right)
   - Select "API Keys" or "Developer Settings"
4. **Create/Copy your API Key**
   - Click "Create API Key" if you don't have one
   - Copy the API key (you'll need it in Step 3)

**Free Tier Includes**:
- 200 credits per month
- Text-to-3D generation
- Image-to-3D generation
- Texture generation
- Model optimization

---

### Step 2: Install Python Dependencies

Open PowerShell and run:

```powershell
# Navigate to the Meshy AI MCP server directory
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai

# Create virtual environment (recommended)
python -m venv .venv

# Activate virtual environment
.\.venv\Scripts\activate

# Install dependencies
pip install -r requirements.txt
```

**Expected output**:
```
Successfully installed mcp-1.6.0 python-dotenv-1.0.0 httpx-0.26.0 pydantic-2.6.4
```

---

### Step 3: Configure Environment Variables

Create a `.env` file in the `mcp-servers/meshy-ai` directory:

```powershell
# Copy the example file
copy .env.example .env

# Edit the .env file with your API key
notepad .env
```

**Edit `.env` file**:
```env
# Meshy AI API Key
MESHY_API_KEY=msy_YOUR_ACTUAL_API_KEY_HERE

# MCP Server Configuration
MCP_PORT=8081

# Task timeout in seconds
TASK_TIMEOUT=300
```

**Replace** `msy_YOUR_ACTUAL_API_KEY_HERE` with your actual API key from Step 1.

---

### Step 4: Test the MCP Server

Before registering with Augment, test that the server works:

```powershell
# Make sure you're in the meshy-ai directory with venv activated
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai
.\.venv\Scripts\activate

# Test the server
python src/server.py
```

**Expected output**:
```
Meshy AI MCP Server started successfully
Listening on port 8081
```

Press `Ctrl+C` to stop the server.

---

### Step 5: Register with Augment Code

**Option A: Using Augment Settings File (Recommended)**

1. **Open Augment settings file**:
   ```
   C:\Users\spare\.augment\settings.json
   ```

2. **Add the Meshy AI MCP server configuration**:

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

**Important Notes**:
- Use **forward slashes** (`/`) in the path, not backslashes
- Replace `msy_YOUR_ACTUAL_API_KEY_HERE` with your actual API key
- If you already have other MCP servers, add `"meshy-ai": { ... }` to the existing `"mcpServers"` object

**Option B: Using Virtual Environment Python (Alternative)**

If you want to use the virtual environment Python explicitly:

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

---

### Step 6: Restart Augment Code

1. **Close Augment Code** completely
2. **Reopen Augment Code**
3. The Meshy AI MCP server should now be available

---

## 🎨 Usage Examples

Once registered, you can use Meshy AI directly from Augment Code:

### Example 1: Generate a 3D Model from Text

```
Using Meshy AI, create a detailed Egyptian ankh with gold metallic texture
```

### Example 2: Convert Image to 3D

```
Using Meshy AI, convert this image to a 3D model: https://example.com/image.jpg
```

### Example 3: Apply Texture to Existing Model

```
Using Meshy AI, apply a rusty metal texture to this 3D model: https://example.com/model.glb
```

### Example 4: Optimize a Model

```
Using Meshy AI, remesh this model to 50,000 polygons: https://example.com/model.glb
```

---

## 🔧 Available Tools

The Meshy AI MCP server provides these tools:

### Creation Tools
- `create_text_to_3d_task` - Generate 3D model from text
- `create_image_to_3d_task` - Generate 3D model from image
- `create_text_to_texture_task` - Apply textures using text
- `create_remesh_task` - Optimize and clean up models

### Management Tools
- `retrieve_text_to_3d_task` - Check task status
- `list_text_to_3d_tasks` - List all tasks
- `stream_text_to_3d_task` - Real-time progress updates
- `get_balance` - Check API credits

---

## 🐛 Troubleshooting

### Issue: "MESHY_API_KEY environment variable is not set"

**Solution**: Make sure you've created the `.env` file with your API key, or added it to the Augment settings.

### Issue: "Module 'mcp' not found"

**Solution**: Install dependencies:
```powershell
cd C:\Users\spare\source\repos\ga\mcp-servers\meshy-ai
.\.venv\Scripts\activate
pip install -r requirements.txt
```

### Issue: "Connection refused" or "Server not responding"

**Solution**: 
1. Check that Python is installed and in PATH
2. Verify the path in Augment settings is correct
3. Test the server manually: `python src/server.py`

### Issue: "Invalid API key"

**Solution**: 
1. Verify your API key is correct
2. Check that you copied the entire key (starts with `msy_`)
3. Make sure there are no extra spaces or quotes

---

## 📚 Resources

- **Meshy AI Website**: https://www.meshy.ai/
- **Meshy AI Discover**: https://www.meshy.ai/discover
- **Meshy AI API Docs**: https://docs.meshy.ai/
- **MCP Server Repo**: https://github.com/pasie15/meshy-ai-mcp-server
- **Augment Code**: https://www.augmentcode.com/

---

## ✅ Verification Checklist

Before using the MCP server, verify:

- [ ] Python 3.9+ is installed
- [ ] Meshy AI account created
- [ ] API key obtained from Meshy AI
- [ ] Dependencies installed (`pip install -r requirements.txt`)
- [ ] `.env` file created with API key
- [ ] Server tested manually (`python src/server.py`)
- [ ] Augment settings updated with MCP server config
- [ ] Augment Code restarted

---

**Status**: Ready to generate amazing 3D models with AI! 🎉

