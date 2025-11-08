/**
 * AnkhReticle3D - 3D Ankh crosshair/reticle for BSP DOOM Explorer
 * Loads the 3D ankh model created in Blender and displays it as a reticle
 */

import React, { useEffect, useRef, useState } from 'react';
import { Box } from '@mui/material';
import * as THREE from 'three';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader';

interface AnkhReticle3DProps {
  /** Whether an element is being hovered */
  hovered?: boolean;
  /** Size of the reticle in pixels */
  size?: number;
}

export const AnkhReticle3D: React.FC<AnkhReticle3DProps> = ({
  hovered = false,
  size = 60,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const ankhRef = useRef<THREE.Group | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const [isLoaded, setIsLoaded] = useState(false);
  const [loadError, setLoadError] = useState(false);

  useEffect(() => {
    if (!canvasRef.current) return;

    // Setup scene
    const scene = new THREE.Scene();
    sceneRef.current = scene;

    // Setup camera
    const camera = new THREE.PerspectiveCamera(45, 1, 0.1, 100);
    camera.position.set(0, 0, 5);
    camera.lookAt(0, 0, 0);
    cameraRef.current = camera;

    // Setup renderer
    const renderer = new THREE.WebGLRenderer({
      canvas: canvasRef.current,
      alpha: true,
      antialias: true,
    });
    renderer.setSize(size, size);
    renderer.setPixelRatio(window.devicePixelRatio);
    rendererRef.current = renderer;

    // Add lighting
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.5);
    scene.add(ambientLight);

    const pointLight = new THREE.PointLight(0xffffff, 1);
    pointLight.position.set(5, 5, 5);
    scene.add(pointLight);

    // Load the ankh model
    const loader = new GLTFLoader();
    loader.load(
      '/models/ankh.glb',
      (gltf: any) => {
        const ankh = gltf.scene;
        ankhRef.current = ankh;

        // Center and scale the model
        const box = new THREE.Box3().setFromObject(ankh);
        const center = box.getCenter(new THREE.Vector3());
        const size = box.getSize(new THREE.Vector3());

        // Center the model
        ankh.position.sub(center);

        // Scale to fit nicely in view
        const maxDim = Math.max(size.x, size.y, size.z);
        const scale = 2.5 / maxDim;
        ankh.scale.setScalar(scale);

        // Rotate to face camera
        ankh.rotation.y = Math.PI;

        // Update material to be emissive
        ankh.traverse((child: THREE.Object3D) => {
          if (child instanceof THREE.Mesh) {
            if (child.material) {
              const material = child.material as THREE.MeshStandardMaterial;
              material.emissive = new THREE.Color(0xf0c040);
              material.emissiveIntensity = hovered ? 0.8 : 0.4;
              material.metalness = 1.0;
              material.roughness = 0.2;
            }
          }
        });

        scene.add(ankh);
        setIsLoaded(true);

        // Start animation
        animate();
      },
      undefined,
      (error: unknown) => {
        console.error('Error loading ankh model:', error);
        setLoadError(true);
      }
    );

    // Animation loop
    const animate = () => {
      if (!sceneRef.current || !cameraRef.current || !rendererRef.current) return;

      // Gentle rotation
      if (ankhRef.current) {
        ankhRef.current.rotation.y += 0.01;
      }

      rendererRef.current.render(sceneRef.current, cameraRef.current);
      animationFrameRef.current = requestAnimationFrame(animate);
    };

    // Cleanup
    return () => {
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      if (rendererRef.current) {
        rendererRef.current.dispose();
      }
    };
  }, [size]);

  // Update emissive intensity when hovered state changes
  useEffect(() => {
    if (ankhRef.current) {
      ankhRef.current.traverse((child: THREE.Object3D) => {
        if (child instanceof THREE.Mesh) {
          if (child.material) {
            const material = child.material as THREE.MeshStandardMaterial;
            material.emissiveIntensity = hovered ? 1.0 : 0.4;
            material.needsUpdate = true;
          }
        }
      });
    }
  }, [hovered]);

  // Fallback to SVG if model fails to load
  if (loadError) {
    return (
      <Box
        sx={{
          position: 'absolute',
          top: '50%',
          left: '50%',
          transform: 'translate(-50%, -50%)',
          width: size,
          height: size,
          pointerEvents: 'none',
          filter: hovered
            ? 'drop-shadow(0 0 8px #00ff00) drop-shadow(0 0 4px #00ff00)'
            : 'drop-shadow(0 0 4px #ffffff) drop-shadow(0 0 2px #ffffff)',
          transition: 'filter 0.2s ease',
        }}
      >
        <svg
          width={size}
          height={size}
          viewBox="0 0 100 100"
          xmlns="http://www.w3.org/2000/svg"
          style={{
            fill: 'none',
            stroke: hovered ? '#00ff00' : '#ffffff',
            strokeWidth: 6,
            strokeLinecap: 'round',
            strokeLinejoin: 'round',
          }}
        >
          {/* Ankh symbol - Egyptian cross of life */}
          <circle cx="50" cy="25" r="15" />
          <line x1="50" y1="40" x2="50" y2="90" />
          <line x1="30" y1="55" x2="70" y2="55" />
        </svg>
      </Box>
    );
  }

  return (
    <Box
      sx={{
        position: 'absolute',
        top: '50%',
        left: '50%',
        transform: 'translate(-50%, -50%)',
        width: size,
        height: size,
        pointerEvents: 'none',
        filter: hovered
          ? 'drop-shadow(0 0 12px #f0c040) drop-shadow(0 0 6px #f0c040)'
          : 'drop-shadow(0 0 6px #f0c040) drop-shadow(0 0 3px #f0c040)',
        transition: 'filter 0.3s ease',
        opacity: isLoaded ? 1 : 0,
      }}
    >
      <canvas
        ref={canvasRef}
        style={{
          width: '100%',
          height: '100%',
          display: 'block',
        }}
      />
    </Box>
  );
};

export default AnkhReticle3D;
