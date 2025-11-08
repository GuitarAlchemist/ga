/**
 * Intelligent BSP Level Visualizer
 * 
 * 3D visualization of intelligent BSP levels with:
 * - Floors (chord families) as separate levels
 * - Landmarks (central shapes) as glowing markers
 * - Portals (bridge chords) as teleport gates
 * - Safe zones (attractors) as protective shields
 * - Challenge paths (limit cycles) as glowing trails
 * - Learning path as animated route
 * 
 * Uses React Three Fiber for 3D rendering
 */

import React, { useRef, useState, useEffect } from 'react';
import { Canvas, useFrame } from '@react-three/fiber';
import { OrbitControls, Text, Line, Sphere, Box, Cylinder } from '@react-three/drei';
import * as THREE from 'three';
import { Box as MuiBox, Typography, Paper, Stack, Chip, LinearProgress } from '@mui/material';

// ==================
// Types
// ==================

export interface IntelligentBSPLevel {
  floors: BSPFloor[];
  landmarks: BSPLandmark[];
  portals: BSPPortal[];
  safeZones: BSPSafeZone[];
  challengePaths: BSPChallengePath[];
  learningPath: string[];
  difficulty: number;
  metadata: Record<string, any>;
}

export interface BSPFloor {
  floorId: number;
  name: string;
  shapeIds: string[];
  color: string;
}

export interface BSPLandmark {
  shapeId: string;
  name: string;
  importance: number;
  type: string;
}

export interface BSPPortal {
  shapeId: string;
  name: string;
  strength: number;
  type: string;
}

export interface BSPSafeZone {
  shapeId: string;
  name: string;
  stability: number;
  type: string;
}

export interface BSPChallengePath {
  name: string;
  shapeIds: string[];
  period: number;
  difficulty: number;
}

export interface IntelligentBSPVisualizerProps {
  level: IntelligentBSPLevel;
  width?: number;
  height?: number;
  showFloors?: boolean;
  showLandmarks?: boolean;
  showPortals?: boolean;
  showSafeZones?: boolean;
  showChallengePaths?: boolean;
  showLearningPath?: boolean;
  animateLearningPath?: boolean;
}

// ==================
// 3D Components
// ==================

const Floor: React.FC<{ floor: BSPFloor; yPosition: number }> = ({ floor, yPosition }) => {
  return (
    <group position={[0, yPosition, 0]}>
      {/* Floor platform */}
      <Box args={[20, 0.5, 20]} position={[0, 0, 0]}>
        <meshStandardMaterial color={floor.color} transparent opacity={0.3} />
      </Box>
      
      {/* Floor label */}
      <Text
        position={[0, 1, 10]}
        fontSize={0.8}
        color="#ffffff"
        anchorX="center"
        anchorY="middle"
      >
        {floor.name}
      </Text>
    </group>
  );
};

const Landmark: React.FC<{ landmark: BSPLandmark; position: THREE.Vector3 }> = ({ landmark, position }) => {
  const meshRef = useRef<THREE.Mesh>(null);
  
  useFrame((state) => {
    if (meshRef.current) {
      // Pulsing animation
      const scale = 1 + Math.sin(state.clock.elapsedTime * 2) * 0.2 * landmark.importance;
      meshRef.current.scale.set(scale, scale, scale);
    }
  });
  
  return (
    <group position={position}>
      <Sphere ref={meshRef} args={[0.5, 32, 32]}>
        <meshStandardMaterial
          color="#ffaa00"
          emissive="#ff6600"
          emissiveIntensity={landmark.importance}
        />
      </Sphere>
      
      {/* Glow effect */}
      <Sphere args={[0.7, 16, 16]}>
        <meshBasicMaterial
          color="#ffaa00"
          transparent
          opacity={0.2 * landmark.importance}
        />
      </Sphere>
    </group>
  );
};

const Portal: React.FC<{ portal: BSPPortal; position: THREE.Vector3 }> = ({ portal, position }) => {
  const meshRef = useRef<THREE.Mesh>(null);
  
  useFrame((state) => {
    if (meshRef.current) {
      // Rotation animation
      meshRef.current.rotation.y = state.clock.elapsedTime;
    }
  });
  
  return (
    <group position={position}>
      <Cylinder ref={meshRef} args={[0.5, 0.5, 2, 32]}>
        <meshStandardMaterial
          color="#00aaff"
          emissive="#0066ff"
          emissiveIntensity={portal.strength}
          transparent
          opacity={0.7}
        />
      </Cylinder>
    </group>
  );
};

const SafeZone: React.FC<{ safeZone: BSPSafeZone; position: THREE.Vector3 }> = ({ safeZone, position }) => {
  return (
    <group position={position}>
      {/* Shield sphere */}
      <Sphere args={[1.5, 32, 32]}>
        <meshStandardMaterial
          color="#00ff00"
          transparent
          opacity={0.1 * safeZone.stability}
          wireframe
        />
      </Sphere>
      
      {/* Core */}
      <Sphere args={[0.3, 16, 16]}>
        <meshStandardMaterial
          color="#00ff00"
          emissive="#00ff00"
          emissiveIntensity={safeZone.stability}
        />
      </Sphere>
    </group>
  );
};

const ChallengePath: React.FC<{ path: BSPChallengePath; positions: THREE.Vector3[] }> = ({ path, positions }) => {
  const lineRef = useRef<any>(null);
  
  useFrame((state) => {
    if (lineRef.current) {
      // Animate line opacity
      const opacity = 0.5 + Math.sin(state.clock.elapsedTime * 2) * 0.3;
      lineRef.current.material.opacity = opacity;
    }
  });
  
  return (
    <Line
      ref={lineRef}
      points={positions}
      color="#ff0000"
      lineWidth={2}
      transparent
      opacity={0.5}
    />
  );
};

const LearningPath: React.FC<{ positions: THREE.Vector3[]; animate: boolean }> = ({ positions, animate }) => {
  const [progress, setProgress] = useState(0);
  
  useEffect(() => {
    if (animate) {
      const interval = setInterval(() => {
        setProgress((prev) => (prev + 0.01) % 1);
      }, 50);
      return () => clearInterval(interval);
    }
  }, [animate]);
  
  const visiblePositions = animate
    ? positions.slice(0, Math.floor(positions.length * progress) + 1)
    : positions;
  
  return (
    <Line
      points={visiblePositions}
      color="#00ffff"
      lineWidth={3}
      transparent
      opacity={0.8}
    />
  );
};

// ==================
// Main Component
// ==================

const Scene: React.FC<{ level: IntelligentBSPLevel; options: IntelligentBSPVisualizerProps }> = ({ level, options }) => {
  // Calculate positions for shapes (simplified - in real implementation, use actual shape positions)
  const getShapePosition = (shapeId: string, floorId: number): THREE.Vector3 => {
    const hash = shapeId.split('').reduce((acc, char) => acc + char.charCodeAt(0), 0);
    const x = ((hash % 20) - 10) * 0.8;
    const z = ((Math.floor(hash / 20) % 20) - 10) * 0.8;
    const y = floorId * 5;
    return new THREE.Vector3(x, y, z);
  };
  
  return (
    <>
      <ambientLight intensity={0.5} />
      <pointLight position={[10, 10, 10]} intensity={1} />
      <pointLight position={[-10, -10, -10]} intensity={0.5} />
      
      {/* Floors */}
      {options.showFloors && level.floors.map((floor) => (
        <Floor key={floor.floorId} floor={floor} yPosition={floor.floorId * 5} />
      ))}
      
      {/* Landmarks */}
      {options.showLandmarks && level.landmarks.map((landmark) => {
        const floor = level.floors.find(f => f.shapeIds.includes(landmark.shapeId));
        const position = getShapePosition(landmark.shapeId, floor?.floorId ?? 0);
        return <Landmark key={landmark.shapeId} landmark={landmark} position={position} />;
      })}
      
      {/* Portals */}
      {options.showPortals && level.portals.map((portal) => {
        const floor = level.floors.find(f => f.shapeIds.includes(portal.shapeId));
        const position = getShapePosition(portal.shapeId, floor?.floorId ?? 0);
        return <Portal key={portal.shapeId} portal={portal} position={position} />;
      })}
      
      {/* Safe Zones */}
      {options.showSafeZones && level.safeZones.map((safeZone) => {
        const floor = level.floors.find(f => f.shapeIds.includes(safeZone.shapeId));
        const position = getShapePosition(safeZone.shapeId, floor?.floorId ?? 0);
        return <SafeZone key={safeZone.shapeId} safeZone={safeZone} position={position} />;
      })}
      
      {/* Challenge Paths */}
      {options.showChallengePaths && level.challengePaths.map((path) => {
        const positions = path.shapeIds.map(shapeId => {
          const floor = level.floors.find(f => f.shapeIds.includes(shapeId));
          return getShapePosition(shapeId, floor?.floorId ?? 0);
        });
        return <ChallengePath key={path.name} path={path} positions={positions} />;
      })}
      
      {/* Learning Path */}
      {options.showLearningPath && level.learningPath.length > 0 && (
        <LearningPath
          positions={level.learningPath.map(shapeId => {
            const floor = level.floors.find(f => f.shapeIds.includes(shapeId));
            return getShapePosition(shapeId, floor?.floorId ?? 0);
          })}
          animate={options.animateLearningPath ?? false}
        />
      )}
      
      <OrbitControls />
    </>
  );
};

export const IntelligentBSPVisualizer: React.FC<IntelligentBSPVisualizerProps> = (props) => {
  const {
    level,
    width = 800,
    height = 600,
    showFloors = true,
    showLandmarks = true,
    showPortals = true,
    showSafeZones = true,
    showChallengePaths = true,
    showLearningPath = true,
    animateLearningPath = false,
  } = props;
  
  return (
    <MuiBox sx={{ width, height }}>
      {/* Stats Panel */}
      <Paper sx={{ p: 2, mb: 2 }}>
        <Stack direction="row" spacing={2} alignItems="center">
          <Typography variant="h6">Intelligent BSP Level</Typography>
          <Chip label={`Difficulty: ${(level.difficulty * 100).toFixed(0)}%`} color="primary" />
          <Chip label={`${level.floors.length} Floors`} />
          <Chip label={`${level.landmarks.length} Landmarks`} />
          <Chip label={`${level.portals.length} Portals`} />
          <Chip label={`${level.safeZones.length} Safe Zones`} />
        </Stack>
        <LinearProgress
          variant="determinate"
          value={level.difficulty * 100}
          sx={{ mt: 1 }}
        />
      </Paper>
      
      {/* 3D Canvas */}
      <Canvas camera={{ position: [15, 15, 15], fov: 60 }}>
        <Scene level={level} options={props} />
      </Canvas>
    </MuiBox>
  );
};

