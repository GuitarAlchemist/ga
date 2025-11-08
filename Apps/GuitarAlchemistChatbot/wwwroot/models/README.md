# 3D Models Gallery - Blazor + Three.js WebGPU

## Overview

This is a test page that showcases all Blender-created 3D models in an interactive Three.js WebGPU scene within a Blazor
application. It demonstrates the integration of modern 3D graphics with .NET web technologies.

## Features

### 🎨 **Interactive 3D Viewer**

- **WebGPU Rendering**: Uses Three.js WebGPU renderer for maximum performance
- **Fallback Support**: Automatically falls back to WebGL if WebGPU is not available
- **Orbit Controls**: Mouse/touch controls for rotating, panning, and zooming
- **Auto-Rotation**: Optional automatic model rotation

### 🎸 **Available Models**

1. **Ankh (☥)** - 114.93 KB
    - Egyptian symbol of life
    - Used as crosshair in BSP DOOM Explorer
    - Gold PBR material with emission

2. **Guitar 1** - 376.89 KB
    - Detailed guitar model
    - Full geometry with strings and frets

3. **Guitar 2** - 785.53 KB
    - Alternative guitar model
    - Higher detail version

### 🎮 **Controls**

- **🎥 Reset Camera**: Return camera to default position
- **🔄 Toggle Rotation**: Enable/disable auto-rotation
- **🔲 Toggle Wireframe**: Switch between solid and wireframe rendering
- **➡️ Next Model**: Cycle through available models
- **Mouse Controls**:
    - Left click + drag: Rotate camera
    - Right click + drag: Pan camera
    - Scroll wheel: Zoom in/out

### 📊 **Real-time Information**

The page displays:

- Current model name
- Vertex count
- Triangle count
- File size
- Renderer backend (WebGPU or WebGL)
- FPS (Frames Per Second)

## Technical Stack

### Frontend

- **Blazor Server**: .NET 9 web framework
- **Three.js**: 3D graphics library (v0.170.0)
- **WebGPU**: Next-generation graphics API
- **ES6 Modules**: Modern JavaScript module system

### 3D Pipeline

1. **Blender 4.5.3 LTS**: Model creation
2. **GLB Export**: Optimized binary format
3. **GLTFLoader**: Three.js loader for GLB files
4. **PBR Materials**: Physically Based Rendering

## File Structure

```
Apps/GuitarAlchemistChatbot/
├── Components/
│   └── Pages/
│       └── Models3D.razor          # Blazor page component
├── wwwroot/
│   ├── js/
│   │   └── models3d.js             # Three.js scene logic
│   └── models/
│       ├── ankh.glb                # Ankh model
│       ├── guitar.glb              # Guitar model 1
│       ├── guitar2.glb             # Guitar model 2
│       └── README.md               # This file
```

## Usage

### Accessing the Page

1. **Run the Blazor application**:
   ```bash
   cd Apps/GuitarAlchemistChatbot
   dotnet run
   ```

2. **Navigate to**: `https://localhost:7002/models3d`

3. **Or click**: "3D Models" in the navigation menu

### Interacting with Models

1. **Select a Model**: Click one of the model buttons at the bottom
2. **Rotate View**: Click and drag with left mouse button
3. **Pan View**: Click and drag with right mouse button
4. **Zoom**: Use mouse scroll wheel
5. **Toggle Features**: Use the control buttons at the top

## Code Examples

### Loading a Model (JavaScript)

```javascript
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

const loader = new GLTFLoader();
const gltf = await loader.loadAsync('/models/ankh.glb');
const model = gltf.scene;

// Apply transformations
model.scale.setScalar(1.5);
model.position.set(0, 0, 0);

// Enable shadows
model.traverse((child) => {
    if (child.isMesh) {
        child.castShadow = true;
        child.receiveShadow = true;
    }
});

scene.add(model);
```

### Blazor Component Integration

```razor
@page "/models3d"

<div id="canvas-container">
    <canvas id="webgpu-canvas"></canvas>
</div>

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JSRuntime.InvokeVoidAsync("initModels3DScene");
        }
    }
}
```

## Performance Optimization

### WebGPU Benefits

- **Compute Shaders**: Parallel processing on GPU
- **Lower CPU Overhead**: More efficient than WebGL
- **Modern API**: Better performance on supported browsers

### Optimization Techniques

1. **Shadow Mapping**: 2048x2048 resolution for quality
2. **Tone Mapping**: ACES Filmic for realistic lighting
3. **Damped Controls**: Smooth camera movement
4. **Efficient Disposal**: Proper cleanup of geometries and materials

## Browser Compatibility

### WebGPU Support

- ✅ Chrome 113+ (Windows, macOS, ChromeOS)
- ✅ Edge 113+ (Windows, macOS)
- ⚠️ Firefox (experimental, behind flag)
- ⚠️ Safari (experimental, behind flag)

### WebGL Fallback

- ✅ All modern browsers
- ✅ Mobile browsers (iOS Safari, Chrome Mobile)

## Lighting Setup

The scene uses a multi-light setup for professional rendering:

1. **Ambient Light**: Base illumination (0.4 intensity)
2. **Key Light**: Main directional light with shadows (1.5 intensity)
3. **Fill Light**: Secondary light to reduce harsh shadows (0.5 intensity)
4. **Rim Light**: Back light for edge definition (0.8 intensity)
5. **Point Lights**: Accent lights for highlights (2x 1.0 intensity)

## Future Enhancements

### Planned Features

- [ ] Model comparison view (side-by-side)
- [ ] Animation playback for animated models
- [ ] Material editor (adjust PBR properties)
- [ ] Screenshot/export functionality
- [ ] VR/AR support
- [ ] Model upload functionality
- [ ] Texture swapping
- [ ] Environment map selection

### Additional Models

- [ ] Fretboard visualization
- [ ] Music notation symbols
- [ ] Chord shape representations
- [ ] Scale diagrams

## Troubleshooting

### Model Not Loading

1. Check browser console for errors
2. Verify model file exists in `/wwwroot/models/`
3. Check network tab for 404 errors
4. Ensure GLTFLoader is properly imported

### WebGPU Not Available

- The page automatically falls back to WebGL
- Check browser compatibility
- Update browser to latest version
- Enable WebGPU in browser flags (experimental)

### Performance Issues

1. Reduce shadow map resolution
2. Disable auto-rotation
3. Use wireframe mode
4. Close other browser tabs
5. Check GPU drivers are up to date

## Development

### Adding New Models

1. **Create model in Blender**:
   ```python
   # Export as GLB
   bpy.ops.export_scene.gltf(
       filepath="model.glb",
       export_format='GLB'
   )
   ```

2. **Copy to wwwroot**:
   ```bash
   cp model.glb Apps/GuitarAlchemistChatbot/wwwroot/models/
   ```

3. **Add to models object** in `models3d.js`:
   ```javascript
   const models = {
       mymodel: {
           name: 'My Model',
           path: '/models/model.glb',
           size: '123.45 KB',
           scale: 1.0,
           position: [0, 0, 0],
           rotation: [0, 0, 0]
       }
   };
   ```

4. **Add button** in `Models3D.razor`:
   ```html
   <button class="model-btn" onclick="loadModel('mymodel')">
       🎵 My Model
   </button>
   ```

## Resources

- **Three.js Documentation**: https://threejs.org/docs/
- **WebGPU Specification**: https://www.w3.org/TR/webgpu/
- **Blender Manual**: https://docs.blender.org/
- **GLB Format**: https://www.khronos.org/gltf/
- **Blazor Documentation**: https://learn.microsoft.com/aspnet/core/blazor/

## Credits

- **3D Models**: Created with Blender 4.5.3 LTS
- **Rendering**: Three.js WebGPU Renderer
- **Framework**: Blazor Server (.NET 9)
- **Design**: Custom CSS with gradient themes

## License

Part of the Guitar Alchemist project. See main repository for license information.

