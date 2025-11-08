import { useRef } from 'react';
import { Canvas, useFrame } from '@react-three/fiber';
import { OrbitControls, Float, Sparkles } from '@react-three/drei';
import * as THREE from 'three';

// Simple rotating guitar pick shape
function GuitarPick() {
  const meshRef = useRef<THREE.Mesh>(null);

  useFrame((state) => {
    if (meshRef.current) {
      meshRef.current.rotation.y = state.clock.elapsedTime * 0.8;
      meshRef.current.rotation.z = Math.sin(state.clock.elapsedTime * 0.5) * 0.2;
    }
  });

  return (
    <mesh ref={meshRef}>
      <coneGeometry args={[1.5, 3, 3]} />
      <meshStandardMaterial
        color="#ff6600"
        emissive="#ff3300"
        emissiveIntensity={0.5}
        metalness={0.9}
        roughness={0.1}
      />
    </mesh>
  );
}

// Orbiting string spheres
function Strings() {
  return (
    <>
      {[0, 1, 2, 3, 4, 5].map((i) => (
        <Float
          key={i}
          speed={1 + i * 0.2}
          rotationIntensity={0.5}
          floatIntensity={0.8}
        >
          <mesh
            position={[
              Math.cos((i / 6) * Math.PI * 2) * 3,
              Math.sin((i / 6) * Math.PI * 2) * 3,
              0
            ]}
          >
            <sphereGeometry args={[0.15, 32, 32]} />
            <meshStandardMaterial
              color={`hsl(${i * 60}, 100%, 50%)`}
              emissive={`hsl(${i * 60}, 100%, 30%)`}
              emissiveIntensity={0.5}
              metalness={0.8}
              roughness={0.2}
            />
          </mesh>
        </Float>
      ))}
    </>
  );
}

// Central alchemist symbol
function AlchemyCore() {
  const meshRef = useRef<THREE.Mesh>(null);

  useFrame((state) => {
    if (meshRef.current) {
      meshRef.current.rotation.x = state.clock.elapsedTime * 0.3;
      meshRef.current.rotation.y = state.clock.elapsedTime * 0.5;
    }
  });

  return (
    <mesh ref={meshRef}>
      <octahedronGeometry args={[0.8, 0]} />
      <meshStandardMaterial
        color="#ffd700"
        emissive="#ffaa00"
        emissiveIntensity={0.8}
        metalness={1}
        roughness={0}
        wireframe={true}
      />
    </mesh>
  );
}

// Scene
function LogoScene() {
  return (
    <>
      <ambientLight intensity={0.4} />
      <pointLight position={[10, 10, 10]} intensity={1.5} color="#ffffff" />
      <pointLight position={[-10, -10, -5]} intensity={0.8} color="#ff6600" />
      <spotLight
        position={[0, 0, 8]}
        angle={0.5}
        penumbra={1}
        intensity={1.2}
        color="#ffd700"
      />

      <group>
        <GuitarPick />
        <AlchemyCore />
        <Strings />
      </group>

      <Sparkles
        count={150}
        scale={12}
        size={3}
        speed={0.6}
        color="#ffd700"
      />

      <OrbitControls
        enableZoom={true}
        enablePan={false}
        minDistance={6}
        maxDistance={20}
        autoRotate
        autoRotateSpeed={1}
      />
    </>
  );
}

// Main component
export default function GuitarAlchemistLogoSimple() {
  return (
    <div style={{
      width: '100%',
      height: '400px',
      background: 'linear-gradient(135deg, #0f0c29 0%, #302b63 50%, #24243e 100%)',
      borderRadius: '8px'
    }}>
      <Canvas
        camera={{ position: [0, 0, 10], fov: 50 }}
        gl={{
          antialias: true,
          alpha: true,
          powerPreference: 'high-performance'
        }}
      >
        <LogoScene />
      </Canvas>
    </div>
  );
}
