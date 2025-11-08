import React, { useRef } from 'react';
import { Canvas } from '@react-three/fiber';
import { OrbitControls, Line } from '@react-three/drei';
import * as THREE from 'three';

interface Vector3 {
  x: number;
  y: number;
  z: number;
}

interface FingertipVisualization {
  position: Vector3;
  direction: Vector3;
  jointPositions: Vector3[];
  arcTrajectory: Vector3[];
  jointFlexionAngles: number[];
  jointAbductionAngles: number[];
}

interface FretboardGeometry {
  thicknessMm: number;
  neckThicknessAtNut: number;
  neckThicknessAt12thFret: number;
  stringHeightAtNut: number;
  stringHeightAt12th: number;
}

interface HandPoseVisualization {
  fingertips: Record<string, FingertipVisualization>;
  wristPosition: Vector3;
  palmOrientation: { x: number; y: number; z: number; w: number };
  fretboardGeometry: FretboardGeometry;
}

interface HandVisualization3DProps {
  visualization: HandPoseVisualization;
  width?: number;
  height?: number;
}

// Convert Vector3 to THREE.Vector3
const toThreeVector = (v: Vector3): THREE.Vector3 => new THREE.Vector3(v.x, v.y, v.z);

// Fretboard component
const Fretboard: React.FC<{ geometry: FretboardGeometry }> = ({ geometry }) => {
  const fretboardLength = 200; // mm (simplified - showing first few frets)
  const fretboardWidth = 50; // mm
  const thickness = geometry.thicknessMm;

  return (
    <group>
      {/* Main fretboard body */}
      <mesh position={[fretboardLength / 2, 0, 0]}>
        <boxGeometry args={[fretboardLength, fretboardWidth, thickness]} />
        <meshStandardMaterial color="#8B4513" opacity={0.7} transparent />
      </mesh>

      {/* Fretboard surface (slightly lighter) */}
      <mesh position={[fretboardLength / 2, 0, thickness / 2 + 0.1]}>
        <planeGeometry args={[fretboardLength, fretboardWidth]} />
        <meshStandardMaterial color="#A0522D" side={THREE.DoubleSide} opacity={0.5} transparent />
      </mesh>

      {/* Strings */}
      {[0, 1, 2, 3, 4, 5].map((stringIndex) => {
        const stringY = -20 + stringIndex * 8; // Spacing between strings
        const stringHeight = geometry.stringHeightAtNut + (geometry.stringHeightAt12th - geometry.stringHeightAtNut) * 0.3;
        
        return (
          <Line
            key={stringIndex}
            points={[
              [0, stringY, thickness / 2 + stringHeight],
              [fretboardLength, stringY, thickness / 2 + stringHeight]
            ]}
            color="silver"
            lineWidth={1}
          />
        );
      })}

      {/* Frets */}
      {[0, 1, 2, 3, 4, 5].map((fretIndex) => {
        const fretX = fretIndex * 35; // Simplified fret spacing
        
        return (
          <Line
            key={fretIndex}
            points={[
              [fretX, -25, thickness / 2],
              [fretX, 25, thickness / 2]
            ]}
            color="#C0C0C0"
            lineWidth={2}
          />
        );
      })}
    </group>
  );
};

// Finger bone component
const FingerBone: React.FC<{ start: Vector3; end: Vector3; color: string }> = ({ start, end, color }) => {
  const startVec = toThreeVector(start);
  const endVec = toThreeVector(end);
  const direction = new THREE.Vector3().subVectors(endVec, startVec);
  const length = direction.length();
  const midpoint = new THREE.Vector3().addVectors(startVec, endVec).multiplyScalar(0.5);

  // Create quaternion for rotation
  const quaternion = new THREE.Quaternion();
  quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction.clone().normalize());

  return (
    <group position={midpoint.toArray()} quaternion={quaternion.toArray() as [number, number, number, number]}>
      <mesh>
        <cylinderGeometry args={[1.5, 1.5, length, 8]} />
        <meshStandardMaterial color={color} />
      </mesh>
    </group>
  );
};

// Joint sphere component
const Joint: React.FC<{ position: Vector3; color: string }> = ({ position, color }) => {
  return (
    <mesh position={[position.x, position.y, position.z]}>
      <sphereGeometry args={[2, 16, 16]} />
      <meshStandardMaterial color={color} />
    </mesh>
  );
};

// Arc trajectory component
const ArcTrajectory: React.FC<{ points: Vector3[]; color: string }> = ({ points, color }) => {
  if (points.length < 2) return null;

  const threePoints = points.map(p => [p.x, p.y, p.z] as [number, number, number]);

  return (
    <Line
      points={threePoints}
      color={color}
      lineWidth={2}
      dashed={true}
      dashScale={2}
    />
  );
};

// Single finger component
const Finger: React.FC<{ name: string; data: FingertipVisualization; color: string }> = ({ name, data, color }) => {
  const joints = data.jointPositions;

  return (
    <group>
      {/* Draw bones between joints */}
      {joints.map((joint, index) => {
        if (index === 0) return null; // Skip first joint (no bone before it)
        return (
          <FingerBone
            key={`bone-${index}`}
            start={joints[index - 1]}
            end={joint}
            color={color}
          />
        );
      })}

      {/* Draw joints */}
      {joints.map((joint, index) => (
        <Joint key={`joint-${index}`} position={joint} color={color} />
      ))}

      {/* Draw arc trajectory */}
      {data.arcTrajectory.length > 0 && (
        <ArcTrajectory points={data.arcTrajectory} color={color} />
      )}
    </group>
  );
};

// Main hand component
const Hand: React.FC<{ visualization: HandPoseVisualization }> = ({ visualization }) => {
  const fingerColors: Record<string, string> = {
    'Index': '#FF6B6B',
    'Middle': '#4ECDC4',
    'Ring': '#45B7D1',
    'Pinky': '#FFA07A',
    'Thumb': '#98D8C8'
  };

  return (
    <group>
      {/* Wrist marker */}
      <mesh position={[visualization.wristPosition.x, visualization.wristPosition.y, visualization.wristPosition.z]}>
        <sphereGeometry args={[4, 16, 16]} />
        <meshStandardMaterial color="#FFD700" />
      </mesh>

      {/* Fingers */}
      {Object.entries(visualization.fingertips).map(([fingerName, fingerData]) => (
        <Finger
          key={fingerName}
          name={fingerName}
          data={fingerData}
          color={fingerColors[fingerName] || '#FFFFFF'}
        />
      ))}
    </group>
  );
};

// Main 3D visualization component
export const HandVisualization3D: React.FC<HandVisualization3DProps> = ({ 
  visualization, 
  width = 800, 
  height = 600 
}) => {
  return (
    <div style={{ width, height, background: '#1a1a1a', borderRadius: '8px', overflow: 'hidden' }}>
      <Canvas
        camera={{ position: [150, 100, 150], fov: 50 }}
        style={{ width: '100%', height: '100%' }}
      >
        {/* Lighting */}
        <ambientLight intensity={0.5} />
        <directionalLight position={[10, 10, 5]} intensity={1} />
        <directionalLight position={[-10, -10, -5]} intensity={0.5} />
        <pointLight position={[0, 50, 50]} intensity={0.5} />

        {/* Fretboard */}
        <Fretboard geometry={visualization.fretboardGeometry} />

        {/* Hand */}
        <Hand visualization={visualization} />

        {/* Grid helper */}
        <gridHelper args={[300, 30, '#444444', '#222222']} position={[0, 0, -20]} />

        {/* Controls */}
        <OrbitControls 
          enableDamping
          dampingFactor={0.05}
          minDistance={50}
          maxDistance={500}
        />
      </Canvas>
    </div>
  );
};

export default HandVisualization3D;

