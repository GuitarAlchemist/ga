/**
 * 3D Models Gallery - Three.js Scene
 * Loads and displays Blender models with interactive controls
 */

import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.170.0/build/three.module.js';
import {GLTFLoader} from 'https://cdn.jsdelivr.net/npm/three@0.170.0/examples/jsm/loaders/GLTFLoader.js';
import {OrbitControls} from 'https://cdn.jsdelivr.net/npm/three@0.170.0/examples/jsm/controls/OrbitControls.js';

// Global state
let scene, camera, renderer, controls;
let currentModel = null;
let autoRotate = true;
let wireframeMode = false;
let animationId = null;
let fpsCounter = {frames: 0, lastTime: performance.now()};

// Model metadata
const models = {
    ankh: {
        name: 'Ankh',
        path: '/models/ankh.glb',
        size: '114.93 KB',
        scale: 1.5,
        position: [0, 0, 0],
        rotation: [0, 0, 0]
    },
    guitar: {
        name: 'Guitar 1',
        path: '/models/guitar.glb',
        size: '376.89 KB',
        scale: 2.0,
        position: [0, -1, 0],
        rotation: [0, Math.PI / 4, 0]
    },
    guitar2: {
        name: 'Guitar 2',
        path: '/models/guitar2.glb',
        size: '785.53 KB',
        scale: 2.0,
        position: [0, -1, 0],
        rotation: [0, Math.PI / 4, 0]
    }
};

let currentModelKey = 'ankh';

/**
 * Initialize the 3D scene
 */
window.initModels3DScene = async function () {
    console.log('🎨 Initializing 3D Models Gallery...');

    const canvas = document.getElementById('webgpu-canvas');
    if (!canvas) {
        console.error('Canvas not found!');
        return;
    }

    // Create scene
    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x0a0a0a);
    scene.fog = new THREE.Fog(0x0a0a0a, 10, 50);

    // Create camera
    const aspect = canvas.clientWidth / canvas.clientHeight;
    camera = new THREE.PerspectiveCamera(50, aspect, 0.1, 1000);
    camera.position.set(0, 2, 8);

    // Use WebGL renderer (WebGPU removed for compatibility)
    console.log('🎨 Initializing WebGL renderer...');
    renderer = new THREE.WebGLRenderer({canvas, antialias: true});
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    document.getElementById('rendererBackend').textContent = 'WebGL';

    renderer.setSize(canvas.clientWidth, canvas.clientHeight);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.2;

    // Create orbit controls
    controls = new OrbitControls(camera, canvas);
    controls.enableDamping = true;
    controls.dampingFactor = 0.05;
    controls.minDistance = 2;
    controls.maxDistance = 20;
    controls.autoRotate = autoRotate;
    controls.autoRotateSpeed = 2.0;

    // Add lights
    setupLighting();

    // Add ground plane
    addGroundPlane();

    // Load initial model
    await loadModel(currentModelKey);

    // Handle window resize
    window.addEventListener('resize', onWindowResize);

    // Start animation loop
    animate();

    console.log('✅ 3D Models Gallery initialized');
};

/**
 * Setup scene lighting
 */
function setupLighting() {
    // Ambient light
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.4);
    scene.add(ambientLight);

    // Main directional light (key light)
    const keyLight = new THREE.DirectionalLight(0xffffff, 1.5);
    keyLight.position.set(5, 10, 5);
    keyLight.castShadow = true;
    keyLight.shadow.mapSize.width = 2048;
    keyLight.shadow.mapSize.height = 2048;
    keyLight.shadow.camera.near = 0.5;
    keyLight.shadow.camera.far = 50;
    keyLight.shadow.camera.left = -10;
    keyLight.shadow.camera.right = 10;
    keyLight.shadow.camera.top = 10;
    keyLight.shadow.camera.bottom = -10;
    scene.add(keyLight);

    // Fill light
    const fillLight = new THREE.DirectionalLight(0x4ecca3, 0.5);
    fillLight.position.set(-5, 5, -5);
    scene.add(fillLight);

    // Rim light
    const rimLight = new THREE.DirectionalLight(0xf0c040, 0.8);
    rimLight.position.set(0, 5, -10);
    scene.add(rimLight);

    // Point lights for accent
    const pointLight1 = new THREE.PointLight(0xf0c040, 1, 20);
    pointLight1.position.set(3, 3, 3);
    scene.add(pointLight1);

    const pointLight2 = new THREE.PointLight(0x4ecca3, 1, 20);
    pointLight2.position.set(-3, 3, -3);
    scene.add(pointLight2);
}

/**
 * Add ground plane
 */
function addGroundPlane() {
    const geometry = new THREE.PlaneGeometry(50, 50);
    const material = new THREE.MeshStandardMaterial({
        color: 0x1a1a2e,
        roughness: 0.8,
        metalness: 0.2
    });
    const ground = new THREE.Mesh(geometry, material);
    ground.rotation.x = -Math.PI / 2;
    ground.position.y = -2;
    ground.receiveShadow = true;
    scene.add(ground);

    // Add grid
    const gridHelper = new THREE.GridHelper(50, 50, 0x0f3460, 0x0f3460);
    gridHelper.position.y = -1.99;
    scene.add(gridHelper);
}

/**
 * Load a 3D model
 */
window.loadModel = async function (modelKey) {
    if (!models[modelKey]) {
        console.error(`Model ${modelKey} not found`);
        return;
    }

    console.log(`📦 Loading model: ${modelKey}`);
    currentModelKey = modelKey;
    const modelData = models[modelKey];

    // Remove current model
    if (currentModel) {
        scene.remove(currentModel);
        currentModel.traverse((child) => {
            if (child.geometry) child.geometry.dispose();
            if (child.material) {
                if (Array.isArray(child.material)) {
                    child.material.forEach(mat => mat.dispose());
                } else {
                    child.material.dispose();
                }
            }
        });
    }

    // Load new model
    const loader = new GLTFLoader();
    try {
        const gltf = await loader.loadAsync(modelData.path);
        currentModel = gltf.scene;

        // Apply transformations
        currentModel.scale.setScalar(modelData.scale);
        currentModel.position.set(...modelData.position);
        currentModel.rotation.set(...modelData.rotation);

        // Enable shadows
        currentModel.traverse((child) => {
            if (child.isMesh) {
                child.castShadow = true;
                child.receiveShadow = true;
            }
        });

        scene.add(currentModel);

        // Update UI
        updateModelInfo(gltf, modelData);

        console.log(`✅ Model loaded: ${modelKey}`);
    } catch (error) {
        console.error(`❌ Error loading model ${modelKey}:`, error);
        document.getElementById('currentModelName').textContent = 'Error loading model';
    }
};

/**
 * Update model information in UI
 */
function updateModelInfo(gltf, modelData) {
    let vertexCount = 0;
    let triangleCount = 0;

    gltf.scene.traverse((child) => {
        if (child.isMesh && child.geometry) {
            const positions = child.geometry.attributes.position;
            if (positions) {
                vertexCount += positions.count;
                if (child.geometry.index) {
                    triangleCount += child.geometry.index.count / 3;
                } else {
                    triangleCount += positions.count / 3;
                }
            }
        }
    });

    document.getElementById('currentModelName').textContent = modelData.name;
    document.getElementById('vertexCount').textContent = vertexCount.toLocaleString();
    document.getElementById('triangleCount').textContent = Math.floor(triangleCount).toLocaleString();
    document.getElementById('fileSize').textContent = modelData.size;
}

/**
 * Animation loop
 */
function animate() {
    animationId = requestAnimationFrame(animate);

    // Update controls
    controls.update();

    // Update FPS counter
    fpsCounter.frames++;
    const now = performance.now();
    if (now >= fpsCounter.lastTime + 1000) {
        const fps = Math.round((fpsCounter.frames * 1000) / (now - fpsCounter.lastTime));
        document.getElementById('fps').textContent = fps;
        fpsCounter.frames = 0;
        fpsCounter.lastTime = now;
    }

    // Render scene
    renderer.render(scene, camera);
}

/**
 * Handle window resize
 */
function onWindowResize() {
    const canvas = document.getElementById('webgpu-canvas');
    camera.aspect = canvas.clientWidth / canvas.clientHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(canvas.clientWidth, canvas.clientHeight);
}

/**
 * Reset camera position
 */
window.resetCamera = function () {
    camera.position.set(0, 2, 8);
    controls.target.set(0, 0, 0);
    controls.update();
};

/**
 * Toggle auto-rotation
 */
window.toggleRotation = function () {
    autoRotate = !autoRotate;
    controls.autoRotate = autoRotate;
    console.log(`Auto-rotation: ${autoRotate ? 'ON' : 'OFF'}`);
};

/**
 * Toggle wireframe mode
 */
window.toggleWireframe = function () {
    wireframeMode = !wireframeMode;
    if (currentModel) {
        currentModel.traverse((child) => {
            if (child.isMesh && child.material) {
                child.material.wireframe = wireframeMode;
            }
        });
    }
    console.log(`Wireframe mode: ${wireframeMode ? 'ON' : 'OFF'}`);
};

/**
 * Cycle through models
 */
window.cycleModel = function () {
    const modelKeys = Object.keys(models);
    const currentIndex = modelKeys.indexOf(currentModelKey);
    const nextIndex = (currentIndex + 1) % modelKeys.length;
    loadModel(modelKeys[nextIndex]);
};

