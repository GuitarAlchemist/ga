import React, { useRef, useEffect, useState } from 'react';
import { Box, Container, Typography, Paper, Button, Stack, Slider, FormControlLabel, Switch, Alert } from '@mui/material';
import * as THREE from 'three';
import { WebGPURenderer } from 'three/webgpu';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';

interface FingerJoint {
  bone: THREE.Bone;
  restQuaternion: THREE.Quaternion;
  restRotation: THREE.Euler;
  minRotation: number;
  maxRotation: number;
  axis: THREE.Vector3; // Primary rotation axis
}

interface Finger {
  name: string;
  joints: FingerJoint[];
  color: number;
}

interface HandPose {
  name: string;
  fingerFlexions: number[]; // [thumb, index, middle, ring, pinky]
  description: string;
}

// Predefined hand poses
const HAND_POSES: HandPose[] = [
  { name: 'open', fingerFlexions: [0, 0, 0, 0, 0], description: 'Open hand' },
  { name: 'fist', fingerFlexions: [1, 1, 1, 1, 1], description: 'Closed fist' },
  { name: 'point', fingerFlexions: [0.3, 0, 1, 1, 1], description: 'Pointing gesture' },
  { name: 'peace', fingerFlexions: [0.5, 0, 0, 1, 1], description: 'Peace sign' },
  { name: 'rock', fingerFlexions: [0, 0, 1, 1, 0], description: 'Rock on' },
  { name: 'ok', fingerFlexions: [0.8, 0.8, 0, 0, 0], description: 'OK sign' },
  { name: 'thumbsup', fingerFlexions: [0, 1, 1, 1, 1], description: 'Thumbs up' },
];

export const HandAnimationTest: React.FC = () => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rendererRef = useRef<WebGPURenderer | THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const controlsRef = useRef<OrbitControls | null>(null);
  const handModelRef = useRef<THREE.Group | null>(null);
  const skeletonHelperRef = useRef<THREE.SkeletonHelper | null>(null);
  const fingersRef = useRef<Finger[]>([]);
  const animationFrameRef = useRef<number | null>(null);
  const mixerRef = useRef<THREE.AnimationMixer | null>(null);
  const clockRef = useRef<THREE.Clock>(new THREE.Clock());

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [useRiggedModel, setUseRiggedModel] = useState(true);
  const [animationSpeed, setAnimationSpeed] = useState(1.0);
  const [autoAnimate, setAutoAnimate] = useState(false);
  const [showSkeleton, setShowSkeleton] = useState(false);
  const [useQuaternions, setUseQuaternions] = useState(true);
  const [transitionDuration, setTransitionDuration] = useState(0.5);
  const [boneCount, setBoneCount] = useState(0);

  // Finger flexion controls (0 = open, 1 = closed)
  const [thumbFlex, setThumbFlex] = useState(0);
  const [indexFlex, setIndexFlex] = useState(0);
  const [middleFlex, setMiddleFlex] = useState(0);
  const [ringFlex, setRingFlex] = useState(0);
  const [pinkyFlex, setPinkyFlex] = useState(0);

  // Target flexions for smooth interpolation
  const targetFlexRef = useRef<number[]>([0, 0, 0, 0, 0]);
  const currentFlexRef = useRef<number[]>([0, 0, 0, 0, 0]);

  // Initialize Three.js scene
  useEffect(() => {
    if (!canvasRef.current) return;

    let isMounted = true;
    const canvas = canvasRef.current;

    const initScene = async () => {
      try {
        // Create scene
        const scene = new THREE.Scene();
        scene.background = new THREE.Color(0x1a1a2e);
        sceneRef.current = scene;

        // Create camera
        const camera = new THREE.PerspectiveCamera(50, 800 / 600, 0.1, 1000);
        camera.position.set(0, 0.5, 2);
        camera.lookAt(0, 0.5, 0);
        cameraRef.current = camera;

        // Try WebGPU first, fallback to WebGL
        let renderer: WebGPURenderer | THREE.WebGLRenderer;
        const hasWebGPU = 'gpu' in navigator;

        if (hasWebGPU) {
          try {
            renderer = new WebGPURenderer({ canvas, antialias: true });
            await renderer.init();
            console.log('✅ Using WebGPU renderer');
          } catch (err) {
            console.warn('WebGPU failed, falling back to WebGL:', err);
            renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
          }
        } else {
          renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
          console.log('✅ Using WebGL renderer');
        }

        renderer.setSize(800, 600);
        renderer.setPixelRatio(window.devicePixelRatio);
        rendererRef.current = renderer;

        // Lighting
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
        scene.add(ambientLight);

        const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
        directionalLight.position.set(5, 10, 7.5);
        scene.add(directionalLight);

        const fillLight = new THREE.DirectionalLight(0x4488ff, 0.3);
        fillLight.position.set(-5, 5, -5);
        scene.add(fillLight);

        // Orbit controls
        const controls = new OrbitControls(camera, canvas);
        controls.enableDamping = true;
        controls.dampingFactor = 0.05;
        controls.target.set(0, 0.5, 0);
        controls.update();
        controlsRef.current = controls;

        // Load hand model
        if (useRiggedModel) {
          await loadRiggedHandModel(scene);
        } else {
          createSimpleHandModel(scene);
        }

        setLoading(false);

        // Animation loop
        const animate = () => {
          if (!isMounted) return;
          animationFrameRef.current = requestAnimationFrame(animate);

          const delta = clockRef.current.getDelta();

          controls.update();

          // Update animation mixer
          if (mixerRef.current) {
            mixerRef.current.update(delta);
          }

          if (autoAnimate) {
            animateHandAutomatically();
          }
          // TODO: Implement smooth interpolation to target flexions
          // updateFingerFlexionsSmooth(delta);

          renderer.render(scene, camera);
        };
        animate();

      } catch (err) {
        console.error('Error initializing scene:', err);
        setError(err instanceof Error ? err.message : 'Failed to initialize 3D scene');
        setLoading(false);
      }
    };

    initScene();

    return () => {
      isMounted = false;
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      controlsRef.current?.dispose();
      rendererRef.current?.dispose();
    };
  }, [useRiggedModel]);

  // Load rigged hand model from GLB file
  const loadRiggedHandModel = async (scene: THREE.Scene) => {
    console.log('🖐️ Loading rigged hand model...');

    const loader = new GLTFLoader();

    return new Promise<void>((resolve, reject) => {
      loader.load(
        '/Dorchester3D_com_rigged_hand.glb',
        (gltf) => {
          const handModel = gltf.scene;
          handModel.scale.set(0.01, 0.01, 0.01);
          handModel.position.set(0, 0, 0);

          // Find bones and skeleton
          const bones: THREE.Bone[] = [];
          let skeleton: THREE.Skeleton | null = null;

          handModel.traverse((child) => {
            if (child instanceof THREE.Bone) {
              bones.push(child);
              console.log('Found bone:', child.name, 'parent:', child.parent?.name || 'none');
            }
            if (child instanceof THREE.SkinnedMesh && child.skeleton) {
              skeleton = child.skeleton;
            }
          });

          setBoneCount(bones.length);

          // Create skeleton helper for visualization
          if (skeleton) {
            const skeletonHelper = new THREE.SkeletonHelper(handModel);
            skeletonHelper.visible = showSkeleton;
            scene.add(skeletonHelper);
            skeletonHelperRef.current = skeletonHelper;
          }

          // Map bones to fingers with proper hierarchy
          fingersRef.current = extractFingerBones(bones);

          // Create animation mixer for smooth transitions
          mixerRef.current = new THREE.AnimationMixer(handModel);

          scene.add(handModel);
          handModelRef.current = handModel;

          console.log('✅ Rigged hand model loaded');
          console.log('   Bones:', bones.length);
          console.log('   Fingers detected:', fingersRef.current.length);
          fingersRef.current.forEach(finger => {
            console.log(`   ${finger.name}: ${finger.joints.length} joints`);
          });

          resolve();
        },
        (progress) => {
          const percent = progress.total > 0 ? (progress.loaded / progress.total * 100).toFixed(2) : '0';
          console.log('Loading:', percent + '%');
        },
        (error) => {
          console.error('Error loading hand model:', error);
          reject(error);
        }
      );
    });
  };

  // Extract finger bones from skeleton with improved detection
  const extractFingerBones = (bones: THREE.Bone[]): Finger[] => {
    const fingers: Finger[] = [];

    // Finger detection patterns (case-insensitive)
    const fingerPatterns = [
      { name: 'thumb', patterns: ['thumb', 'pollex'], color: 0x4CAF50 },
      { name: 'index', patterns: ['index', 'pointer'], color: 0x2196F3 },
      { name: 'middle', patterns: ['middle'], color: 0xFF9800 },
      { name: 'ring', patterns: ['ring'], color: 0xE91E63 },
      { name: 'pinky', patterns: ['pinky', 'little'], color: 0x9C27B0 },
    ];

    fingerPatterns.forEach(({ name, patterns, color }) => {
      const fingerBones = bones.filter(bone => {
        const boneName = bone.name.toLowerCase();
        return patterns.some(pattern => boneName.includes(pattern));
      });

      if (fingerBones.length > 0) {
        // Sort bones by hierarchy (parent to child)
        const sortedBones = sortBonesByHierarchy(fingerBones);

        fingers.push({
          name,
          color,
          joints: sortedBones.map(bone => {
            // Store rest pose
            const restQuaternion = bone.quaternion.clone();
            const restRotation = bone.rotation.clone();

            // Determine primary rotation axis (usually X for finger flexion)
            const axis = new THREE.Vector3(1, 0, 0);

            return {
              bone,
              restQuaternion,
              restRotation,
              minRotation: 0,
              maxRotation: Math.PI / 2, // 90 degrees max flexion
              axis,
            };
          })
        });
      }
    });

    return fingers;
  };

  // Sort bones by parent-child hierarchy
  const sortBonesByHierarchy = (bones: THREE.Bone[]): THREE.Bone[] => {
    const sorted: THREE.Bone[] = [];
    const boneSet = new Set(bones);

    // Find root bone (bone whose parent is not in the set)
    const roots = bones.filter(bone => !bone.parent || !boneSet.has(bone.parent as THREE.Bone));

    // Traverse from root to leaves
    const traverse = (bone: THREE.Bone) => {
      sorted.push(bone);
      bone.children.forEach(child => {
        if (child instanceof THREE.Bone && boneSet.has(child)) {
          traverse(child);
        }
      });
    };

    roots.forEach(traverse);
    return sorted;
  };

  // Create simple procedural hand model
  const createSimpleHandModel = (scene: THREE.Scene) => {
    console.log('🖐️ Creating simple hand model...');
    
    const handGroup = new THREE.Group();
    const skinColor = 0xffdbac;
    
    // Palm
    const palmGeometry = new THREE.BoxGeometry(0.4, 0.1, 0.6);
    const palmMaterial = new THREE.MeshStandardMaterial({ 
      color: skinColor,
      roughness: 0.7,
      metalness: 0.1,
    });
    const palm = new THREE.Mesh(palmGeometry, palmMaterial);
    palm.position.set(0, 0.5, 0);
    handGroup.add(palm);
    
    // Create fingers
    const fingerPositions = [
      { x: -0.15, name: 'thumb', length: 0.25 },
      { x: -0.075, name: 'index', length: 0.35 },
      { x: -0.025, name: 'middle', length: 0.38 },
      { x: 0.025, name: 'ring', length: 0.35 },
      { x: 0.075, name: 'pinky', length: 0.30 },
    ];
    
    fingerPositions.forEach(({ x, name, length }) => {
      const fingerGroup = new THREE.Group();
      fingerGroup.position.set(x, 0.5, 0.3);
      
      // Create 3 phalanges per finger
      for (let i = 0; i < 3; i++) {
        const phalanxLength = length / 3;
        const phalanxGeometry = new THREE.CylinderGeometry(0.02, 0.02, phalanxLength, 8);
        const phalanx = new THREE.Mesh(phalanxGeometry, palmMaterial);
        phalanx.position.y = i * phalanxLength;
        fingerGroup.add(phalanx);
      }
      
      handGroup.add(fingerGroup);
    });
    
    scene.add(handGroup);
    handModelRef.current = handGroup;
  };

  // Update finger flexion based on slider values
  useEffect(() => {
    if (!handModelRef.current || fingersRef.current.length === 0) return;
    
    const flexValues = [thumbFlex, indexFlex, middleFlex, ringFlex, pinkyFlex];
    
    fingersRef.current.forEach((finger, fingerIndex) => {
      const flex = flexValues[fingerIndex];
      
      finger.joints.forEach((joint, jointIndex) => {
        if (joint.bone) {
          // Apply rotation based on flex value
          const targetRotation = joint.minRotation + (joint.maxRotation - joint.minRotation) * flex;
          joint.bone.rotation.x = targetRotation;
        }
      });
    });
  }, [thumbFlex, indexFlex, middleFlex, ringFlex, pinkyFlex]);

  // Auto-animate hand (wave, fist, etc.)
  const animateHandAutomatically = () => {
    const time = Date.now() * 0.001 * animationSpeed;
    
    // Wave animation
    setThumbFlex(Math.sin(time) * 0.5 + 0.5);
    setIndexFlex(Math.sin(time + 0.2) * 0.5 + 0.5);
    setMiddleFlex(Math.sin(time + 0.4) * 0.5 + 0.5);
    setRingFlex(Math.sin(time + 0.6) * 0.5 + 0.5);
    setPinkyFlex(Math.sin(time + 0.8) * 0.5 + 0.5);
  };

  // Preset hand poses
  const applyPose = (pose: 'open' | 'fist' | 'point' | 'peace' | 'rock') => {
    setAutoAnimate(false);
    
    switch (pose) {
      case 'open':
        [setThumbFlex, setIndexFlex, setMiddleFlex, setRingFlex, setPinkyFlex].forEach(fn => fn(0));
        break;
      case 'fist':
        [setThumbFlex, setIndexFlex, setMiddleFlex, setRingFlex, setPinkyFlex].forEach(fn => fn(1));
        break;
      case 'point':
        setThumbFlex(0.3);
        setIndexFlex(0);
        setMiddleFlex(1);
        setRingFlex(1);
        setPinkyFlex(1);
        break;
      case 'peace':
        setThumbFlex(0.5);
        setIndexFlex(0);
        setMiddleFlex(0);
        setRingFlex(1);
        setPinkyFlex(1);
        break;
      case 'rock':
        setThumbFlex(0);
        setIndexFlex(0);
        setMiddleFlex(1);
        setRingFlex(1);
        setPinkyFlex(0);
        break;
    }
  };

  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" gutterBottom>
          Hand Animation Test (Three.js)
        </Typography>
        <Typography variant="body1" color="text.secondary" paragraph>
          Interactive hand animation using rigged 3D model with skeletal animation.
          Control individual finger joints or use preset poses.
        </Typography>
      </Box>

      {error && (
        <Paper elevation={2} sx={{ p: 3, mb: 4, bgcolor: 'error.dark' }}>
          <Typography color="error.contrastText">Error: {error}</Typography>
        </Paper>
      )}

      <Paper elevation={2} sx={{ p: 3, mb: 4 }}>
        <Stack direction="row" spacing={2} alignItems="center" flexWrap="wrap">
          <FormControlLabel
            control={
              <Switch
                checked={useRiggedModel}
                onChange={(e) => setUseRiggedModel(e.target.checked)}
              />
            }
            label="Use Rigged Model"
          />
          <FormControlLabel
            control={
              <Switch
                checked={autoAnimate}
                onChange={(e) => setAutoAnimate(e.target.checked)}
              />
            }
            label="Auto Animate"
          />
        </Stack>
      </Paper>

      <Paper elevation={2} sx={{ p: 3, mb: 4, display: 'flex', justifyContent: 'center' }}>
        <canvas
          ref={canvasRef}
          style={{
            width: '800px',
            height: '600px',
            display: loading ? 'none' : 'block',
          }}
        />
        {loading && (
          <Box sx={{ width: 800, height: 600, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Typography>Loading hand model...</Typography>
          </Box>
        )}
      </Paper>

      <Paper elevation={2} sx={{ p: 3, mb: 4 }}>
        <Typography variant="h6" gutterBottom>Preset Poses</Typography>
        <Stack direction="row" spacing={2} flexWrap="wrap">
          <Button variant="outlined" onClick={() => applyPose('open')}>✋ Open Hand</Button>
          <Button variant="outlined" onClick={() => applyPose('fist')}>✊ Fist</Button>
          <Button variant="outlined" onClick={() => applyPose('point')}>👆 Point</Button>
          <Button variant="outlined" onClick={() => applyPose('peace')}>✌️ Peace</Button>
          <Button variant="outlined" onClick={() => applyPose('rock')}>🤘 Rock</Button>
        </Stack>
      </Paper>

      <Paper elevation={2} sx={{ p: 3 }}>
        <Typography variant="h6" gutterBottom>Manual Finger Control</Typography>
        
        <Box sx={{ mb: 3 }}>
          <Typography gutterBottom>Thumb Flexion</Typography>
          <Slider value={thumbFlex} onChange={(_, v) => setThumbFlex(v as number)} min={0} max={1} step={0.01} />
        </Box>
        
        <Box sx={{ mb: 3 }}>
          <Typography gutterBottom>Index Finger Flexion</Typography>
          <Slider value={indexFlex} onChange={(_, v) => setIndexFlex(v as number)} min={0} max={1} step={0.01} />
        </Box>
        
        <Box sx={{ mb: 3 }}>
          <Typography gutterBottom>Middle Finger Flexion</Typography>
          <Slider value={middleFlex} onChange={(_, v) => setMiddleFlex(v as number)} min={0} max={1} step={0.01} />
        </Box>
        
        <Box sx={{ mb: 3 }}>
          <Typography gutterBottom>Ring Finger Flexion</Typography>
          <Slider value={ringFlex} onChange={(_, v) => setRingFlex(v as number)} min={0} max={1} step={0.01} />
        </Box>
        
        <Box sx={{ mb: 3 }}>
          <Typography gutterBottom>Pinky Finger Flexion</Typography>
          <Slider value={pinkyFlex} onChange={(_, v) => setPinkyFlex(v as number)} min={0} max={1} step={0.01} />
        </Box>

        {autoAnimate && (
          <Box sx={{ mb: 3 }}>
            <Typography gutterBottom>Animation Speed</Typography>
            <Slider 
              value={animationSpeed} 
              onChange={(_, v) => setAnimationSpeed(v as number)} 
              min={0.1} 
              max={3} 
              step={0.1} 
            />
          </Box>
        )}
      </Paper>
    </Container>
  );
};

export default HandAnimationTest;

