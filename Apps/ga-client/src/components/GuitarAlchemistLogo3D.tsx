import { useRef, useMemo } from 'react';
import { Canvas, useFrame } from '@react-three/fiber';
import { OrbitControls, Center, Float, MeshDistortMaterial, Sparkles } from '@react-three/drei';
import * as THREE from 'three';

// Guitar shape component
function GuitarShape() {
  const meshRef = useRef<THREE.Mesh>(null);

  useFrame((state) => {
    if (meshRef.current) {
      meshRef.current.rotation.y = state.clock.elapsedTime * 0.5;
      meshRef.current.rotation.x = Math.sin(state.clock.elapsedTime * 0.3) * 0.1;
    }
  });

  // Create guitar body shape
  const guitarShape = useMemo(() => {
    const shape = new THREE.Shape();

    // Guitar body (simplified electric guitar shape)
    shape.moveTo(0, 2);
    shape.bezierCurveTo(1.5, 2, 2, 1.5, 2, 0);
    shape.bezierCurveTo(2, -1.5, 1.5, -2, 0, -2);
    shape.bezierCurveTo(-1.5, -2, -2, -1.5, -2, 0);
    shape.bezierCurveTo(-2, 1.5, -1.5, 2, 0, 2);

    return shape;
  }, []);

  const extrudeSettings = {
    steps: 2,
    depth: 0.3,
    bevelEnabled: true,
    bevelThickness: 0.1,
    bevelSize: 0.05,
    bevelSegments: 3
  };

  return (
    <mesh ref={meshRef} position={[0, 0, 0]}>
      <extrudeGeometry args={[guitarShape, extrudeSettings]} />
      <MeshDistortMaterial
        color="#ff6600"
        attach="material"
        distort={0.3}
        speed={2}
        roughness={0.2}
        metalness={0.8}
      />
    </mesh>
  );
}

// Alchemist symbol (golden ratio spiral)
function AlchemySymbol() {
  const meshRef = useRef<THREE.Mesh>(null);

  useFrame((state) => {
    if (meshRef.current) {
      meshRef.current.rotation.z = state.clock.elapsedTime * 0.8;
      meshRef.current.rotation.y = Math.sin(state.clock.elapsedTime * 0.5) * 0.2;
    }
  });

  const spiralCurve = useMemo(() => {
    const points: THREE.Vector3[] = [];
    const segments = 100;
    const goldenRatio = 1.618033988749895;

    for (let i = 0; i < segments; i++) {
      const t = i / segments;
      const angle = t * Math.PI * 4;
      const radius = Math.pow(goldenRatio, t * 2) * 0.3;
      points.push(new THREE.Vector3(
        Math.cos(angle) * radius,
        Math.sin(angle) * radius,
        t * 0.5 - 0.25
      ));
    }

    return new THREE.CatmullRomCurve3(points);
  }, []);

  return (
    <mesh ref={meshRef}>
      <tubeGeometry args={[spiralCurve, 100, 0.05, 8, false]} />
      <meshStandardMaterial
        color="#ffd700"
        emissive="#ff8800"
        emissiveIntensity={0.5}
        metalness={1}
        roughness={0.1}
      />
    </mesh>
  );
}

// Floating musical notes as geometric shapes
function MusicalNotes() {
  return (
    <>
      {[0, 1, 2, 3, 4, 5].map((i) => (
        <Float
          key={i}
          speed={2 + i * 0.5}
          rotationIntensity={0.5}
          floatIntensity={1}
        >
          <mesh
            position={[
              Math.cos((i / 6) * Math.PI * 2) * 4,
              Math.sin((i / 6) * Math.PI * 2) * 4,
              Math.sin(i) * 0.5
            ]}
          >
            <octahedronGeometry args={[0.2]} />
            <meshStandardMaterial
              color="#00ffff"
              emissive="#0088ff"
              emissiveIntensity={0.5}
              metalness={0.9}
              roughness={0.1}
            />
          </mesh>
        </Float>
      ))}
      {/* Add torus rings for musical staff lines effect */}
      {[0, 1, 2].map((i) => (
        <Float
          key={`torus-${i}`}
          speed={1 + i * 0.3}
          rotationIntensity={0.3}
          floatIntensity={0.5}
        >
          <mesh
            position={[
              Math.cos((i / 3) * Math.PI * 2 + Math.PI / 3) * 3.5,
              Math.sin((i / 3) * Math.PI * 2 + Math.PI / 3) * 3.5,
              0
            ]}
            rotation={[Math.PI / 2, 0, i * 0.5]}
          >
            <torusGeometry args={[0.3, 0.05, 16, 32]} />
            <meshStandardMaterial
              color="#ff00ff"
              emissive="#ff0088"
              emissiveIntensity={0.3}
              metalness={0.8}
              roughness={0.2}
            />
          </mesh>
        </Float>
      ))}
    </>
  );
}

// Main logo component
function LogoScene() {
  return (
    <>
      <ambientLight intensity={0.5} />
      <pointLight position={[10, 10, 10]} intensity={1} color="#ffffff" />
      <pointLight position={[-10, -10, -10]} intensity={0.5} color="#ff6600" />
      <spotLight
        position={[0, 5, 5]}
        angle={0.3}
        penumbra={1}
        intensity={1}
        castShadow
        color="#ffd700"
      />

      <Center>
        <group>
          <GuitarShape />
          <AlchemySymbol />
        </group>
      </Center>

      <MusicalNotes />

      <Sparkles
        count={100}
        scale={10}
        size={2}
        speed={0.4}
        color="#ffd700"
      />

      <OrbitControls
        enableZoom={true}
        enablePan={false}
        minDistance={5}
        maxDistance={15}
        autoRotate
        autoRotateSpeed={0.5}
      />
    </>
  );
}

// Main exported component
export default function GuitarAlchemistLogo3D() {
  return (
    <div style={{ width: '100%', height: '500px', background: 'linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%)' }}>
      <Canvas
        camera={{ position: [0, 0, 8], fov: 50 }}
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
