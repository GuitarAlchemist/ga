/**
 * Sunburst3D - 3D Sunburst Visualization with Slope Effect and LOD
 * 
 * Inspired by D3's zoomable sunburst (https://observablehq.com/@d3/sunburst/2)
 * but rendered in 3D with:
 * - Slope/elevation effect (inner rings higher, outer rings lower)
 * - Level of Detail (LOD) to avoid rendering too many segments
 * - Interactive zoom/navigation by clicking segments
 * - Smooth camera transitions
 */

import React, { useEffect, useRef, useState } from 'react';
import * as THREE from 'three';
import { Box, Typography, Paper, Stack, Chip } from '@mui/material';
import { PortalDoor } from './PortalDoor';

// ==================
// Types
// ==================

export interface SunburstNode {
  name: string;
  value?: number;
  children?: SunburstNode[];
  color?: number;
}

export interface Sunburst3DProps {
  data: SunburstNode;
  width?: number;
  height?: number;
  maxDepth?: number; // Maximum depth to render (LOD)
  slopeAngle?: number; // Angle of slope in degrees (0 = flat, 45 = steep)
  onNodeClick?: (node: SunburstNode, path: string[]) => void;
}

// ==================
// Helper Functions
// ==================

/**
 * Calculate total value of a node (sum of children or own value)
 */
const calculateValue = (node: SunburstNode): number => {
  if (node.children && node.children.length > 0) {
    return node.children.reduce((sum, child) => sum + calculateValue(child), 0);
  }
  return node.value || 1;
};

/**
 * Flatten hierarchy into renderable segments with angles and radii
 */
interface SunburstSegment {
  node: SunburstNode;
  depth: number;
  startAngle: number;
  endAngle: number;
  innerRadius: number;
  outerRadius: number;
  path: string[];
}

const flattenHierarchy = (
  node: SunburstNode,
  depth: number = 0,
  startAngle: number = 0,
  endAngle: number = Math.PI * 2,
  innerRadius: number = 0,
  radiusStep: number = 5,
  path: string[] = [],
  maxDepth: number = 10
): SunburstSegment[] => {
  if (depth > maxDepth) return [];

  const segments: SunburstSegment[] = [];
  const outerRadius = innerRadius + radiusStep;
  const currentPath = [...path, node.name];

  // Add current node as a segment
  segments.push({
    node,
    depth,
    startAngle,
    endAngle,
    innerRadius,
    outerRadius,
    path: currentPath,
  });

  // Process children
  if (node.children && node.children.length > 0) {
    const totalValue = calculateValue(node);
    let currentAngle = startAngle;

    node.children.forEach((child) => {
      const childValue = calculateValue(child);
      const angleSpan = ((endAngle - startAngle) * childValue) / totalValue;
      const childEndAngle = currentAngle + angleSpan;

      segments.push(
        ...flattenHierarchy(
          child,
          depth + 1,
          currentAngle,
          childEndAngle,
          outerRadius,
          radiusStep,
          currentPath,
          maxDepth
        )
      );

      currentAngle = childEndAngle;
    });
  }

  return segments;
};

// ==================
// Main Component
// ==================

export const Sunburst3D: React.FC<Sunburst3DProps> = ({
  data,
  width = 1200,
  height = 800,
  maxDepth = 5,
  slopeAngle = 30,
  onNodeClick,
}) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const sceneRef = useRef<THREE.Scene | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const animationIdRef = useRef<number | null>(null);
  const raycasterRef = useRef<THREE.Raycaster>(new THREE.Raycaster());
  const mouseRef = useRef<THREE.Vector2>(new THREE.Vector2());

  const [hoveredSegment, setHoveredSegment] = useState<SunburstSegment | null>(null);
  const [selectedPath, setSelectedPath] = useState<string[]>([data.name]);
  const [currentDepth, setCurrentDepth] = useState<number>(0);

  // Segment meshes for raycasting
  const segmentMeshesRef = useRef<Map<THREE.Mesh, SunburstSegment>>(new Map());
  const portalsRef = useRef<PortalDoor[]>([]);
  const hoverLabelRef = useRef<THREE.Sprite | null>(null);

  /**
   * Create or update floating hover label
   */
  const updateHoverLabel = (scene: THREE.Scene, segment: SunburstSegment | null) => {
    // Remove existing label
    if (hoverLabelRef.current) {
      scene.remove(hoverLabelRef.current);
      hoverLabelRef.current.material.map?.dispose();
      hoverLabelRef.current.material.dispose();
      hoverLabelRef.current = null;
    }

    if (!segment) return;

    // Create canvas for label
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    if (!context) return;

    canvas.width = 512;
    canvas.height = 128;

    // Background
    context.fillStyle = 'rgba(0, 0, 0, 0.9)';
    context.fillRect(0, 0, canvas.width, canvas.height);

    // Border
    context.strokeStyle = '#00ffff';
    context.lineWidth = 4;
    context.strokeRect(2, 2, canvas.width - 4, canvas.height - 4);

    // Text
    context.font = 'bold 48px Arial';
    context.fillStyle = '#00ffff';
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.fillText(segment.node.name, canvas.width / 2, canvas.height / 2);

    // Create sprite
    const texture = new THREE.CanvasTexture(canvas);
    const material = new THREE.SpriteMaterial({
      map: texture,
      transparent: true,
      depthTest: false, // Always show on top
    });
    const sprite = new THREE.Sprite(material);

    // Position at segment center - HORIZONTAL LEVEL based on depth
    const midAngle = (segment.startAngle + segment.endAngle) / 2;
    const midRadius = (segment.innerRadius + segment.outerRadius) / 2;
    const levelHeight = 3; // Same as in segment creation
    const elevation = -segment.depth * levelHeight;

    sprite.position.set(
      Math.cos(midAngle) * midRadius,
      elevation + 2, // Slightly above the segment
      Math.sin(midAngle) * midRadius
    );
    sprite.scale.set(8, 2, 1);

    scene.add(sprite);
    hoverLabelRef.current = sprite;
  };

  /**
   * Add portal doors to other visualizations
   */
  const addPortals = (scene: THREE.Scene) => {
    // Clear existing portals
    portalsRef.current.forEach(portal => {
      portal.dispose();
      scene.remove(portal.mesh);
    });
    portalsRef.current = [];

    // Portal to BSP DOOM Explorer (SMALLER and less obtrusive - positioned above the sunburst)
    const doomPortal = new PortalDoor({
      position: new THREE.Vector3(0, 30, 0),
      rotation: new THREE.Euler(Math.PI / 2, 0, 0),
      scale: 0.8, // REDUCED from 2 to 0.8
      color: 0x00ff00,
      targetUrl: '/test/bsp-doom-explorer',
      label: 'BSP DOOM EXPLORER',
      glowIntensity: 0.3, // REDUCED from 0.7 to 0.3
    });
    scene.add(doomPortal.mesh);
    portalsRef.current.push(doomPortal);

    // Portal to Immersive Musical World (SMALLER and less obtrusive - positioned below the sunburst)
    const immersivePortal = new PortalDoor({
      position: new THREE.Vector3(0, -30, 0),
      rotation: new THREE.Euler(-Math.PI / 2, 0, 0),
      scale: 0.8, // REDUCED from 2 to 0.8
      color: 0xff00ff,
      targetUrl: '/test/immersive-musical-world',
      label: 'IMMERSIVE WORLD',
      glowIntensity: 0.3, // REDUCED from 0.7 to 0.3
    });
    scene.add(immersivePortal.mesh);
    portalsRef.current.push(immersivePortal);

    console.log('✅ Added portals to Sunburst3D:', portalsRef.current.length);
  };

  /**
   * Create RADIAL text label for a segment (follows the curve)
   */
  const createTextLabel = (text: string, segment: SunburstSegment, elevation: number): THREE.Sprite | null => {
    const { startAngle, endAngle, innerRadius, outerRadius } = segment;

    // Create canvas for text
    const canvas = document.createElement('canvas');
    const context = canvas.getContext('2d');
    if (!context) return null;

    canvas.width = 1024; // INCREASED from 512 for more text visibility
    canvas.height = 256; // INCREASED from 128 for better quality

    // Clear canvas
    context.clearRect(0, 0, canvas.width, canvas.height);

    // Set font - LARGER for better visibility
    context.font = 'bold 72px Arial'; // INCREASED from 48px
    context.textAlign = 'center';
    context.textBaseline = 'middle';

    // Draw text with stronger glow for visibility
    context.shadowColor = '#00ffff'; // Cyan glow
    context.shadowBlur = 20; // INCREASED from 10
    context.fillStyle = '#ffffff';
    context.fillText(text, canvas.width / 2, canvas.height / 2);

    // Create sprite
    const texture = new THREE.CanvasTexture(canvas);
    const spriteMaterial = new THREE.SpriteMaterial({
      map: texture,
      transparent: true,
      opacity: 1.0, // INCREASED from 0.9 for full visibility
      depthTest: false, // Always visible
    });

    const sprite = new THREE.Sprite(spriteMaterial);

    // Position at center of segment with RADIAL rotation
    const midAngle = (startAngle + endAngle) / 2;
    const midRadius = (innerRadius + outerRadius) / 2;
    const x = Math.cos(midAngle) * midRadius;
    const z = Math.sin(midAngle) * midRadius;

    // Position ON THE PLATE - Since mesh is rotated -90° on X, we need to use (x, z, y) coordinates
    // The sprite should sit just above the plate surface
    sprite.position.set(x, 0, z); // Position in local mesh coordinates (XZ plane after rotation)

    // RADIAL ROTATION - rotate sprite to follow the curve
    // Since the mesh is rotated -90° on X axis, we need to rotate around Y axis instead of Z
    sprite.rotation.y = midAngle; // Rotate to face outward radially

    // Scale based on segment size - SMALLER for tighter fit
    const scale = Math.min((outerRadius - innerRadius) * 0.8, 3); // REDUCED multiplier from 1.2 to 0.8
    sprite.scale.set(scale * 2, scale * 0.6, 1); // REDUCED width from 3 to 2, height from 0.8 to 0.6

    return sprite;
  };

  /**
   * Create a 3D segment (ring slice) with slope effect
   */
  const createSegment = (segment: SunburstSegment, slopeRadians: number): THREE.Mesh => {
    const { startAngle, endAngle, innerRadius, outerRadius, depth, node } = segment;

    // Create ring geometry with slope applied to vertices
    const shape = new THREE.Shape();
    const angleSpan = endAngle - startAngle;
    const segments = Math.max(3, Math.ceil(angleSpan * 32)); // More segments for smoother curves

    // Outer arc
    for (let i = 0; i <= segments; i++) {
      const angle = startAngle + (angleSpan * i) / segments;
      const x = Math.cos(angle) * outerRadius;
      const y = Math.sin(angle) * outerRadius;
      if (i === 0) {
        shape.moveTo(x, y);
      } else {
        shape.lineTo(x, y);
      }
    }

    // Inner arc (reverse)
    for (let i = segments; i >= 0; i--) {
      const angle = startAngle + (angleSpan * i) / segments;
      const x = Math.cos(angle) * innerRadius;
      const y = Math.sin(angle) * innerRadius;
      shape.lineTo(x, y);
    }

    shape.closePath();

    // Extrude to create 3D shape
    const extrudeSettings = {
      depth: 0.5,
      bevelEnabled: true,
      bevelThickness: 0.1,
      bevelSize: 0.1,
      bevelSegments: 2,
    };

    const geometry = new THREE.ExtrudeGeometry(shape, extrudeSettings);

    // ELEGANT HORIZONTAL LEVELS - Each depth level on its own horizontal plane
    // Instead of continuous slope, we have discrete stepped levels
    const levelHeight = 3; // Height between each level
    const levelElevation = -depth * levelHeight; // Each depth gets its own horizontal level

    const positionAttribute = geometry.attributes.position;
    for (let i = 0; i < positionAttribute.count; i++) {
      const z = positionAttribute.getZ(i);

      // Set all vertices at this depth to the same horizontal level
      positionAttribute.setZ(i, z + levelElevation);
    }
    positionAttribute.needsUpdate = true;
    geometry.computeVertexNormals(); // Recompute normals after vertex modification

    // Color based on depth or node color
    const baseColor = node.color || (0x4488ff - depth * 0x111111);
    const material = new THREE.MeshPhysicalMaterial({
      color: baseColor,
      emissive: baseColor,
      emissiveIntensity: 0.2,
      metalness: 0.3,
      roughness: 0.6,
      transparent: true,
      opacity: 0.9,
      side: THREE.DoubleSide,
    });

    const mesh = new THREE.Mesh(geometry, material);
    mesh.rotation.x = -Math.PI / 2; // Lay flat
    mesh.castShadow = true;
    mesh.receiveShadow = true;

    // Store segment data
    mesh.userData = {
      segment,
      baseColor,
    };

    // Add text label for segments - REDUCED thresholds for more labels
    // angleSpan already declared above at line 254
    if (angleSpan > 0.05 && (outerRadius - innerRadius) > 0.5) { // REDUCED from 0.2 and 2 to 0.05 and 0.5
      // Calculate elevation for label position - HORIZONTAL LEVEL based on depth
      const levelHeight = 3; // Same as in segment creation
      const labelElevation = -depth * levelHeight;

      const label = createTextLabel(node.name, segment, labelElevation);
      if (label) {
        mesh.add(label);
      }
    }

    return mesh;
  };

  /**
   * Build the sunburst visualization
   */
  const buildSunburst = (scene: THREE.Scene, rootNode: SunburstNode, depth: number) => {
    // Clear existing segments
    segmentMeshesRef.current.forEach((_, mesh) => {
      scene.remove(mesh);
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
    });
    segmentMeshesRef.current.clear();

    // Flatten hierarchy (INCREASED radiusStep from 5 to 10 for larger sunburst)
    const segments = flattenHierarchy(rootNode, 0, 0, Math.PI * 2, 0, 10, [], maxDepth);

    // Filter segments based on current depth (LOD)
    const visibleSegments = segments.filter((seg) => seg.depth <= depth + maxDepth);

    // Create meshes
    const slopeRadians = (slopeAngle * Math.PI) / 180;
    visibleSegments.forEach((segment) => {
      const mesh = createSegment(segment, slopeRadians);
      scene.add(mesh);
      segmentMeshesRef.current.set(mesh, segment);
    });

    console.log(`✅ Sunburst3D: Rendered ${visibleSegments.length} segments (depth ${depth})`);
  };

  // Initialize Three.js scene
  useEffect(() => {
    if (!canvasRef.current) return;

    // Scene
    const scene = new THREE.Scene();
    sceneRef.current = scene;

    // Create skybox with gradient (inspired by Poly Haven skies)
    const skyboxGeometry = new THREE.SphereGeometry(500, 32, 32);
    const skyboxMaterial = new THREE.ShaderMaterial({
      uniforms: {
        topColor: { value: new THREE.Color(0x0077ff) }, // Sky blue
        bottomColor: { value: new THREE.Color(0x000033) }, // Deep blue
        offset: { value: 33 },
        exponent: { value: 0.6 }
      },
      vertexShader: `
        varying vec3 vWorldPosition;
        void main() {
          vec4 worldPosition = modelMatrix * vec4(position, 1.0);
          vWorldPosition = worldPosition.xyz;
          gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
        }
      `,
      fragmentShader: `
        uniform vec3 topColor;
        uniform vec3 bottomColor;
        uniform float offset;
        uniform float exponent;
        varying vec3 vWorldPosition;
        void main() {
          float h = normalize(vWorldPosition + offset).y;
          gl_FragColor = vec4(mix(bottomColor, topColor, max(pow(max(h, 0.0), exponent), 0.0)), 1.0);
        }
      `,
      side: THREE.BackSide
    });
    const skybox = new THREE.Mesh(skyboxGeometry, skyboxMaterial);
    scene.add(skybox);

    // Camera (INCREASED distance from 50,50 to 80,80 for larger sunburst)
    const camera = new THREE.PerspectiveCamera(60, width / height, 0.1, 1000);
    camera.position.set(0, 80, 80);
    camera.lookAt(0, 0, 0);
    cameraRef.current = camera;

    // Renderer
    const renderer = new THREE.WebGLRenderer({
      canvas: canvasRef.current,
      antialias: true,
    });
    renderer.setSize(width, height);
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.VSMShadowMap;
    rendererRef.current = renderer;

    // Lighting
    const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
    scene.add(ambientLight);

    const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
    directionalLight.position.set(10, 20, 10);
    directionalLight.castShadow = true;
    directionalLight.shadow.camera.near = 0.1;
    directionalLight.shadow.camera.far = 100;
    directionalLight.shadow.camera.left = -50;
    directionalLight.shadow.camera.right = 50;
    directionalLight.shadow.camera.top = 50;
    directionalLight.shadow.camera.bottom = -50;
    directionalLight.shadow.mapSize.width = 512;
    directionalLight.shadow.mapSize.height = 512;
    directionalLight.shadow.radius = 4;
    directionalLight.shadow.bias = -0.0005;
    scene.add(directionalLight);

    // Add subtle point lights for atmosphere
    const pointLight1 = new THREE.PointLight(0x4488ff, 0.3, 100);
    pointLight1.position.set(30, 30, 30);
    scene.add(pointLight1);

    const pointLight2 = new THREE.PointLight(0xff8844, 0.3, 100);
    pointLight2.position.set(-30, 30, -30);
    scene.add(pointLight2);

    // Build initial sunburst
    buildSunburst(scene, data, currentDepth);

    // Add portals
    addPortals(scene);

    // Animation loop
    let lastTime = Date.now();
    const animate = () => {
      if (!rendererRef.current || !sceneRef.current || !cameraRef.current) return;

      const currentTime = Date.now();
      const delta = (currentTime - lastTime) / 1000;
      lastTime = currentTime;

      // Update portals
      portalsRef.current.forEach(portal => {
        portal.update(delta);
      });

      // Rotate camera slowly
      const time = currentTime * 0.0001;
      cameraRef.current.position.x = Math.cos(time) * 50;
      cameraRef.current.position.z = Math.sin(time) * 50;
      cameraRef.current.lookAt(0, 0, 0);

      rendererRef.current.render(sceneRef.current, cameraRef.current);
      animationIdRef.current = requestAnimationFrame(animate);
    };

    animate();

    return () => {
      if (animationIdRef.current) {
        cancelAnimationFrame(animationIdRef.current);
      }
      renderer.dispose();
    };
  }, [data, width, height, currentDepth, maxDepth, slopeAngle]);

  // Mouse interaction
  useEffect(() => {
    const handleMouseMove = (event: MouseEvent) => {
      if (!containerRef.current || !cameraRef.current || !sceneRef.current) return;

      const rect = containerRef.current.getBoundingClientRect();
      mouseRef.current.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
      mouseRef.current.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

      // Raycast
      raycasterRef.current.setFromCamera(mouseRef.current, cameraRef.current);
      const meshes = Array.from(segmentMeshesRef.current.keys());
      const intersects = raycasterRef.current.intersectObjects(meshes);

      if (intersects.length > 0) {
        const mesh = intersects[0].object as THREE.Mesh;
        const segment = segmentMeshesRef.current.get(mesh);
        setHoveredSegment(segment || null);

        // Update floating hover label
        updateHoverLabel(sceneRef.current, segment || null);

        // Highlight
        (mesh.material as THREE.MeshPhysicalMaterial).emissiveIntensity = 0.6;
      } else {
        setHoveredSegment(null);
        updateHoverLabel(sceneRef.current, null);
      }

      // Reset all other meshes
      segmentMeshesRef.current.forEach((seg, mesh) => {
        if (intersects.length === 0 || mesh !== intersects[0].object) {
          (mesh.material as THREE.MeshPhysicalMaterial).emissiveIntensity = 0.2;
        }
      });
    };

    const handleClick = () => {
      // Check if clicking on a portal
      if (cameraRef.current && sceneRef.current) {
        raycasterRef.current.setFromCamera(mouseRef.current, cameraRef.current);
        const portalMeshes = portalsRef.current.map(p => p.mesh);
        const portalIntersects = raycasterRef.current.intersectObjects(portalMeshes, true);

        if (portalIntersects.length > 0) {
          // Find the portal that was clicked
          for (const portal of portalsRef.current) {
            if (portalIntersects[0].object === portal.mesh || portal.mesh.children.includes(portalIntersects[0].object as any)) {
              console.log(`🌀 Portal activated: ${portal.label} → ${portal.targetUrl}`);
              window.location.href = portal.targetUrl;
              return;
            }
          }
        }
      }

      // Otherwise, handle segment click
      if (hoveredSegment) {
        setSelectedPath(hoveredSegment.path);
        setCurrentDepth(hoveredSegment.depth);
        if (onNodeClick) {
          onNodeClick(hoveredSegment.node, hoveredSegment.path);
        }
      }
    };

    containerRef.current?.addEventListener('mousemove', handleMouseMove);
    containerRef.current?.addEventListener('click', handleClick);

    const container = containerRef.current;
    return () => {
      container?.removeEventListener('mousemove', handleMouseMove);
      container?.removeEventListener('click', handleClick);
    };
  }, [hoveredSegment, onNodeClick]);

  return (
    <Box ref={containerRef} sx={{ position: 'relative', width, height }}>
      <canvas ref={canvasRef} />

      {/* HUD */}
      <Paper
        sx={{
          position: 'absolute',
          top: 16,
          left: 16,
          padding: 2,
          backgroundColor: 'rgba(0, 0, 0, 0.8)',
          border: '1px solid #0f0',
        }}
      >
        <Typography variant="h6" sx={{ color: '#0f0', fontFamily: 'monospace' }}>
          3D Sunburst
        </Typography>
        
        {/* Breadcrumb */}
        <Stack direction="row" spacing={0.5} sx={{ mt: 1, flexWrap: 'wrap' }}>
          {selectedPath.map((name, index) => (
            <React.Fragment key={index}>
              <Chip
                label={name}
                size="small"
                sx={{
                  backgroundColor: index === selectedPath.length - 1 ? 'rgba(0, 255, 255, 0.2)' : 'rgba(0, 255, 0, 0.1)',
                  color: index === selectedPath.length - 1 ? '#0ff' : '#0f0',
                  fontSize: '10px',
                }}
              />
              {index < selectedPath.length - 1 && <Typography sx={{ color: '#0f0' }}>→</Typography>}
            </React.Fragment>
          ))}
        </Stack>

        {hoveredSegment && (
          <Box sx={{ mt: 2 }}>
            <Typography sx={{ color: '#0ff', fontSize: '12px' }}>
              {hoveredSegment.node.name}
            </Typography>
            <Typography sx={{ color: '#888', fontSize: '10px' }}>
              Depth: {hoveredSegment.depth} | Value: {calculateValue(hoveredSegment.node)}
            </Typography>
          </Box>
        )}
      </Paper>
    </Box>
  );
};

