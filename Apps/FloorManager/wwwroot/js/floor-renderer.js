// ===== 2D Canvas Rendering =====
window.renderFloor = function (floorData) {
    renderFloor2D('floorCanvas', floorData);
};

window.renderFloor2D = function (canvasId, floorData) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        console.error('Canvas not found:', canvasId);
        return;
    }

    const ctx = canvas.getContext('2d');
    const canvasWidth = canvas.width;
    const canvasHeight = canvas.height;

    // Calculate scale to fit the dungeon in the canvas
    const scaleX = canvasWidth / floorData.width;
    const scaleY = canvasHeight / floorData.height;
    const scale = Math.min(scaleX, scaleY) * 0.9;

    // Clear canvas
    ctx.fillStyle = '#2d3748';
    ctx.fillRect(0, 0, canvasWidth, canvasHeight);

    // Center the dungeon
    const offsetX = (canvasWidth - floorData.width * scale) / 2;
    const offsetY = (canvasHeight - floorData.height * scale) / 2;

    // Draw corridors first (different color - blue-gray)
    ctx.fillStyle = '#3b82f6'; // Blue color for corridors
    floorData.corridors.forEach(corridor => {
        const x1 = Math.min(corridor.start.x, corridor.end.x);
        const y1 = Math.min(corridor.start.y, corridor.end.y);
        const width = Math.abs(corridor.end.x - corridor.start.x) || corridor.width;
        const height = Math.abs(corridor.end.y - corridor.start.y) || corridor.width;

        ctx.fillRect(
            offsetX + x1 * scale,
            offsetY + y1 * scale,
            width * scale,
            height * scale
        );
    });

    // Draw rooms
    floorData.rooms.forEach(room => {
        const x = offsetX + room.x * scale;
        const y = offsetY + room.y * scale;
        const w = room.width * scale;
        const h = room.height * scale;

        // Room background
        if (room.musicItem) {
            ctx.fillStyle = '#764ba2';
        } else {
            ctx.fillStyle = '#718096';
        }
        ctx.fillRect(x, y, w, h);

        // Room border
        ctx.strokeStyle = '#e2e8f0';
        ctx.lineWidth = 2;
        ctx.strokeRect(x, y, w, h);

        // Room number
        ctx.fillStyle = 'white';
        ctx.font = `${Math.max(10, scale * 2)}px Arial`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(room.id.toString(), x + w / 2, y + h / 2);

        // Music item indicator
        if (room.musicItem) {
            ctx.fillStyle = '#ffd700';
            ctx.font = `${Math.max(12, scale * 2.5)}px Arial`;
            ctx.fillText('♪', x + w / 2, y + h / 4);
        }
    });

    // Draw grid
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
    ctx.lineWidth = 1;
    const gridSize = 10;
    for (let i = 0; i <= floorData.width; i += gridSize) {
        ctx.beginPath();
        ctx.moveTo(offsetX + i * scale, offsetY);
        ctx.lineTo(offsetX + i * scale, offsetY + floorData.height * scale);
        ctx.stroke();
    }
    for (let i = 0; i <= floorData.height; i += gridSize) {
        ctx.beginPath();
        ctx.moveTo(offsetX, offsetY + i * scale);
        ctx.lineTo(offsetX + floorData.width * scale, offsetY + i * scale);
        ctx.stroke();
    }

    // Add legend
    ctx.textAlign = 'left';
    ctx.fillStyle = 'white';
    ctx.font = '14px Arial';
    ctx.fillText('🟪 Room with music item', 10, canvasHeight - 40);
    ctx.fillText('🟦 Empty room', 10, canvasHeight - 20);
};

// ===== 3D WebGL Rendering with Three.js =====
let scene3D, camera3D, renderer3D, controls3D, raycaster3D, mouse3D;
let roomMeshes3D = [];
let dotNetHelper3D = null;

window.renderFloor3D = async function (containerId, floorData, dotNetHelper) {
    dotNetHelper3D = dotNetHelper;

    // Load Three.js if not already loaded
    if (typeof THREE === 'undefined') {
        await loadThreeJS();
    }

    const container = document.getElementById(containerId);
    if (!container) {
        console.error('Container not found:', containerId);
        return;
    }

    // Clear previous scene
    if (renderer3D) {
        container.removeChild(renderer3D.domElement);
        renderer3D.dispose();
    }

    // Setup scene
    scene3D = new THREE.Scene();
    scene3D.background = new THREE.Color(0x1a202c);
    scene3D.fog = new THREE.Fog(0x1a202c, 50, 200);

    // Setup camera
    const aspect = container.clientWidth / container.clientHeight;
    camera3D = new THREE.PerspectiveCamera(60, aspect, 0.1, 1000);
    camera3D.position.set(floorData.width / 2, floorData.height * 1.5, floorData.width);
    camera3D.lookAt(floorData.width / 2, 0, floorData.height / 2);

    // Setup renderer
    renderer3D = new THREE.WebGLRenderer({antialias: true});
    renderer3D.setSize(container.clientWidth, container.clientHeight);
    renderer3D.shadowMap.enabled = true;
    renderer3D.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(renderer3D.domElement);

    // Setup controls
    controls3D = new THREE.OrbitControls(camera3D, renderer3D.domElement);
    controls3D.enableDamping = true;
    controls3D.dampingFactor = 0.05;
    controls3D.target.set(floorData.width / 2, 0, floorData.height / 2);

    // Setup raycaster for mouse picking
    raycaster3D = new THREE.Raycaster();
    mouse3D = new THREE.Vector2();

    // Add lights
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.4);
    scene3D.add(ambientLight);

    const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
    directionalLight.position.set(floorData.width / 2, floorData.height * 2, floorData.width / 2);
    directionalLight.castShadow = true;
    directionalLight.shadow.camera.left = -floorData.width;
    directionalLight.shadow.camera.right = floorData.width;
    directionalLight.shadow.camera.top = floorData.height;
    directionalLight.shadow.camera.bottom = -floorData.height;
    scene3D.add(directionalLight);

    // Add floor plane
    const floorGeometry = new THREE.PlaneGeometry(floorData.width, floorData.height);
    const floorMaterial = new THREE.MeshStandardMaterial({
        color: 0x2d3748,
        roughness: 0.8,
        metalness: 0.2
    });
    const floorMesh = new THREE.Mesh(floorGeometry, floorMaterial);
    floorMesh.rotation.x = -Math.PI / 2;
    floorMesh.position.set(floorData.width / 2, -0.1, floorData.height / 2);
    floorMesh.receiveShadow = true;
    scene3D.add(floorMesh);

    // Add grid helper
    const gridHelper = new THREE.GridHelper(Math.max(floorData.width, floorData.height), 20, 0x4a5568, 0x2d3748);
    gridHelper.position.set(floorData.width / 2, 0, floorData.height / 2);
    scene3D.add(gridHelper);

    roomMeshes3D = [];

    // Draw corridors
    floorData.corridors.forEach(corridor => {
        const x1 = Math.min(corridor.start.x, corridor.end.x);
        const z1 = Math.min(corridor.start.y, corridor.end.y);
        const width = Math.abs(corridor.end.x - corridor.start.x) || corridor.width;
        const depth = Math.abs(corridor.end.y - corridor.start.y) || corridor.width;

        const corridorGeometry = new THREE.BoxGeometry(width, 0.5, depth);
        const corridorMaterial = new THREE.MeshStandardMaterial({
            color: 0x3b82f6, // Blue color for corridors
            roughness: 0.7,
            emissive: 0x1e40af,
            emissiveIntensity: 0.1
        });
        const corridorMesh = new THREE.Mesh(corridorGeometry, corridorMaterial);
        corridorMesh.position.set(x1 + width / 2, 0.25, z1 + depth / 2);
        corridorMesh.receiveShadow = true;
        scene3D.add(corridorMesh);
    });

    // Draw rooms
    floorData.rooms.forEach(room => {
        const roomHeight = room.musicItem ? 4 : 2;
        const roomGeometry = new THREE.BoxGeometry(room.width, roomHeight, room.height);
        const roomColor = room.musicItem ? 0x764ba2 : 0x718096;
        const roomMaterial = new THREE.MeshStandardMaterial({
            color: roomColor,
            roughness: 0.6,
            metalness: 0.3,
            emissive: room.musicItem ? 0x764ba2 : 0x000000,
            emissiveIntensity: room.musicItem ? 0.2 : 0
        });

        const roomMesh = new THREE.Mesh(roomGeometry, roomMaterial);
        roomMesh.position.set(
            room.x + room.width / 2,
            roomHeight / 2,
            room.y + room.height / 2
        );
        roomMesh.castShadow = true;
        roomMesh.receiveShadow = true;
        roomMesh.userData = {roomId: room.id, room: room};

        scene3D.add(roomMesh);
        roomMeshes3D.push(roomMesh);

        // Add room number text (using sprite)
        if (room.musicItem) {
            const sprite = createTextSprite(room.id.toString(), 0xffd700);
            sprite.position.set(
                room.x + room.width / 2,
                roomHeight + 1,
                room.y + room.height / 2
            );
            scene3D.add(sprite);
        }
    });

    // Mouse click handler
    renderer3D.domElement.addEventListener('click', onMouseClick3D, false);

    // Animation loop
    animate3D();
};

function animate3D() {
    if (!renderer3D) return;
    requestAnimationFrame(animate3D);
    controls3D.update();
    renderer3D.render(scene3D, camera3D);
}

function onMouseClick3D(event) {
    const rect = renderer3D.domElement.getBoundingClientRect();
    mouse3D.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    mouse3D.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    raycaster3D.setFromCamera(mouse3D, camera3D);
    const intersects = raycaster3D.intersectObjects(roomMeshes3D);

    if (intersects.length > 0) {
        const roomId = intersects[0].object.userData.roomId;
        if (dotNetHelper3D) {
            dotNetHelper3D.invokeMethodAsync('OnRoomClicked', roomId);
        }
    }
}


function createTextSprite(text, color = 0xffffff) {
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    canvas.width = 128;
    canvas.height = 64;

    context.fillStyle = `#${color.toString(16).padStart(6, '0')}`;
    context.font = 'Bold 48px Arial';
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.fillText(text, 64, 32);

    const texture = new THREE.CanvasTexture(canvas);
    const spriteMaterial = new THREE.SpriteMaterial({map: texture});
    const sprite = new THREE.Sprite(spriteMaterial);
    sprite.scale.set(2, 1, 1);

    return sprite;
}

window.resetCamera3D = function () {
    if (camera3D && controls3D) {
        camera3D.position.set(40, 60, 40);
        controls3D.target.set(40, 0, 30);
        controls3D.update();
    }
};

async function loadThreeJS() {
    return new Promise((resolve, reject) => {
        // Load Three.js from CDN
        const script = document.createElement('script');
        script.src = 'https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js';
        script.onload = () => {
            // Load OrbitControls
            const controlsScript = document.createElement('script');
            controlsScript.src = 'https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js';
            controlsScript.onload = resolve;
            controlsScript.onerror = reject;
            document.head.appendChild(controlsScript);
        };
        script.onerror = reject;
        document.head.appendChild(script);
    });
}

// ===== Web Audio API for Music Playback =====
let audioContext = null;

function getAudioContext() {
    if (!audioContext) {
        audioContext = new (window.AudioContext || window.webkitAudioContext)();
    }
    return audioContext;
}

window.playPitchClasses = function (pitchClasses) {
    const ctx = getAudioContext();
    const now = ctx.currentTime;

    // Convert pitch classes to frequencies (using equal temperament, A4 = 440Hz)
    const baseFreq = 440; // A4
    const semitoneRatio = Math.pow(2, 1 / 12);

    pitchClasses.forEach((pc, index) => {
        // Map pitch class to frequency (C4 = 0, C#4 = 1, etc.)
        const semitonesFromA4 = pc - 9; // A is pitch class 9
        const frequency = baseFreq * Math.pow(semitoneRatio, semitonesFromA4);

        // Create oscillator for each note
        const oscillator = ctx.createOscillator();
        const gainNode = ctx.createGain();

        oscillator.type = 'sine';
        oscillator.frequency.setValueAtTime(frequency, now);

        // Envelope: attack, sustain, release
        gainNode.gain.setValueAtTime(0, now);
        gainNode.gain.linearRampToValueAtTime(0.3, now + 0.05); // Attack
        gainNode.gain.linearRampToValueAtTime(0.2, now + 0.5); // Sustain
        gainNode.gain.linearRampToValueAtTime(0, now + 1.5); // Release

        oscillator.connect(gainNode);
        gainNode.connect(ctx.destination);

        // Play note with slight delay for arpeggio effect
        oscillator.start(now + index * 0.1);
        oscillator.stop(now + 1.5 + index * 0.1);
    });
};

// Handle window resize for 3D canvas
window.addEventListener('resize', () => {
    if (renderer3D && camera3D) {
        const container = renderer3D.domElement.parentElement;
        if (container) {
            camera3D.aspect = container.clientWidth / container.clientHeight;
            camera3D.updateProjectionMatrix();
            renderer3D.setSize(container.clientWidth, container.clientHeight);
        }
    }
});

