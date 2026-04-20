import React, { useEffect, useRef, useState } from 'react';
import { Box, Typography, Paper, Chip, Stack } from '@mui/material';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';

/**
 * Harmonic Nebula — Phase 2 scaffold.
 *
 * Renders the OPTIC-K voicing corpus as a two-level visualization:
 * nebular clouds (cluster centroids) positioned on a sphere, with
 * individual voicings as instanced stars around each cloud centroid.
 *
 * Phase 2 is running against SYNTHETIC data matching the locked JSON
 * schema from `ix-voicings viz-precompute`. Phase 1.5 swaps the
 * generator for a fetch of `state/viz/{cluster-layout,voicing-layout}.json`.
 * Phase 3 wires interactions (click, search, filter).
 *
 * Design doc: ga/docs/plans/2026-04-20-optick-corpus-viz-plan.md
 */

// ---------------------------------------------------------------------------
// Schema mirrors — must stay in sync with ix-voicings/src/viz_precompute.rs
// ---------------------------------------------------------------------------

interface ClusterLayout {
  id: string;
  instrument: string;
  local_cluster_id: number;
  position: [number, number, number];
  voicing_count: number;
  representative_voicing_idx: number;
}

interface VoicingLayout {
  global_id: string;
  cluster_id: string;
  position: [number, number, number];
  chord_family_id: number;
  rarity_rank: number;
  instrument: string;
}

// Chord-family palette — contract with ix-voicings::viz_precompute::chord_family.
const CHORD_FAMILY_COLOR: Record<number, number> = {
  0: 0xf5c542, // major  — warm yellow
  1: 0x4285f4, // minor  — cool blue
  2: 0xea4335, // dominant — red
  3: 0x9c27b0, // diminished — violet
  4: 0x34a853, // suspended — green
  5: 0xff6f00, // altered — orange
  6: 0x9e9e9e, // other/unclassified — neutral gray
};

const INSTRUMENT_LABEL_PREFIX: Record<string, string> = {
  guitar: 'Guitar',
  bass: 'Bass',
  ukulele: 'Ukulele',
};

// ---------------------------------------------------------------------------
// Synthetic data generator (matches Rust schema + layout algorithm exactly)
// ---------------------------------------------------------------------------

function goldenSpiralPoint(i: number, n: number, r: number): [number, number, number] {
  if (n === 0) return [0, 0, 0];
  const phi = Math.PI * (3 - Math.sqrt(5));
  const y = 1 - (i / Math.max(n - 1, 1)) * 2;
  const radiusAtY = Math.sqrt(Math.max(0, 1 - y * y));
  const theta = phi * i;
  return [
    r * Math.cos(theta) * radiusAtY,
    r * y,
    r * Math.sin(theta) * radiusAtY,
  ];
}

function seededOffset(globalId: string, scale: number): [number, number, number] {
  // FNV-1a hash of the id; same algorithm as the Rust precompute.
  let h = 0xcbf29ce484222325n;
  for (const c of globalId) {
    h ^= BigInt(c.charCodeAt(0));
    h = BigInt.asUintN(64, h * 0x100000001b3n);
  }
  const n = Number(h);
  const fx = (((n & 0xffff) / 0x10000) - 0.5) * scale;
  const fy = ((((n >>> 16) & 0xffff) / 0x10000) - 0.5) * scale;
  const fz = ((((n >>> 32) & 0xffff) / 0x10000) - 0.5) * scale;
  return [fx, fy, fz];
}

interface SyntheticDataset {
  clusters: ClusterLayout[];
  voicings: VoicingLayout[];
}

function generateSyntheticNebula(): SyntheticDataset {
  const instruments = ['guitar', 'bass', 'ukulele'];
  const clustersPerInstrument = 5;
  const voicingsPerCluster = 22;
  const radius = 10;

  const clusters: ClusterLayout[] = [];
  const voicings: VoicingLayout[] = [];

  instruments.forEach((inst) => {
    for (let k = 0; k < clustersPerInstrument; k++) {
      clusters.push({
        id: `${inst}-C${k}`,
        instrument: inst,
        local_cluster_id: k,
        position: [0, 0, 0],
        voicing_count: voicingsPerCluster,
        representative_voicing_idx: 0,
      });
    }
  });

  const total = clusters.length;
  clusters.forEach((c, i) => {
    c.position = goldenSpiralPoint(i, total, radius);
  });

  const clusterLookup = new Map<string, [number, number, number]>();
  clusters.forEach((c) => clusterLookup.set(c.id, c.position));

  clusters.forEach((c) => {
    for (let v = 0; v < c.voicing_count; v++) {
      const globalId = `${c.instrument}_v${(c.local_cluster_id * 1000 + v).toString().padStart(4, '0')}`;
      const offset = seededOffset(globalId, 1.8);
      const centroid = clusterLookup.get(c.id)!;
      voicings.push({
        global_id: globalId,
        cluster_id: c.id,
        position: [
          centroid[0] + offset[0],
          centroid[1] + offset[1],
          centroid[2] + offset[2],
        ],
        // Synthetic variation so we can see color — real data from
        // Phase 1.5 comes from ChordIdentification.
        chord_family_id: (c.local_cluster_id + v) % 7,
        rarity_rank: v / c.voicing_count,
        instrument: c.instrument,
      });
    }
  });

  return { clusters, voicings };
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

const HarmonicNebulaDemo: React.FC = () => {
  const containerRef = useRef<HTMLDivElement>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const frameRef = useRef<number | null>(null);

  const [stats, setStats] = useState({ clusters: 0, voicings: 0 });

  useEffect(() => {
    if (!containerRef.current) return;
    const container = containerRef.current;

    const { clusters, voicings } = generateSyntheticNebula();
    setStats({ clusters: clusters.length, voicings: voicings.length });

    // -----------------------------------------------------------------------
    // Scene, camera, renderer
    // -----------------------------------------------------------------------
    const scene = new THREE.Scene();
    scene.background = new THREE.Color(0x05060a);
    scene.fog = new THREE.FogExp2(0x05060a, 0.015);

    const camera = new THREE.PerspectiveCamera(
      60,
      container.clientWidth / container.clientHeight,
      0.1,
      200,
    );
    camera.position.set(16, 12, 22);

    const renderer = new THREE.WebGLRenderer({ antialias: true });
    renderer.setPixelRatio(window.devicePixelRatio);
    renderer.setSize(container.clientWidth, container.clientHeight);
    container.appendChild(renderer.domElement);
    rendererRef.current = renderer;

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.autoRotate = true;
    controls.autoRotateSpeed = 0.35;

    // Lights (subtle — most of the look comes from emissive materials)
    scene.add(new THREE.AmbientLight(0x334155, 1.0));
    const dir = new THREE.DirectionalLight(0xffffff, 0.4);
    dir.position.set(10, 15, 8);
    scene.add(dir);

    // -----------------------------------------------------------------------
    // Cluster cloud markers — large semi-transparent spheres
    // -----------------------------------------------------------------------
    const cloudGeom = new THREE.IcosahedronGeometry(1.1, 2);
    const cloudMat = new THREE.MeshBasicMaterial({
      color: 0xffffff,
      transparent: true,
      opacity: 0.08,
      depthWrite: false,
    });
    const cloudMesh = new THREE.InstancedMesh(cloudGeom, cloudMat, clusters.length);
    const dummy = new THREE.Object3D();
    clusters.forEach((c, i) => {
      dummy.position.set(c.position[0], c.position[1], c.position[2]);
      dummy.scale.setScalar(1);
      dummy.updateMatrix();
      cloudMesh.setMatrixAt(i, dummy.matrix);
    });
    cloudMesh.instanceMatrix.needsUpdate = true;
    scene.add(cloudMesh);

    // -----------------------------------------------------------------------
    // Voicing stars — small emissive spheres, colored by chord family
    // -----------------------------------------------------------------------
    const starGeom = new THREE.IcosahedronGeometry(0.08, 0);
    const starMat = new THREE.MeshBasicMaterial({ vertexColors: false });
    const starMesh = new THREE.InstancedMesh(starGeom, starMat, voicings.length);
    starMesh.instanceColor = new THREE.InstancedBufferAttribute(
      new Float32Array(voicings.length * 3),
      3,
    );
    const color = new THREE.Color();
    voicings.forEach((v, i) => {
      dummy.position.set(v.position[0], v.position[1], v.position[2]);
      // Size micro-variation by rarity so rare voicings pop slightly.
      dummy.scale.setScalar(0.9 + 0.6 * v.rarity_rank);
      dummy.updateMatrix();
      starMesh.setMatrixAt(i, dummy.matrix);

      const hex = CHORD_FAMILY_COLOR[v.chord_family_id] ?? CHORD_FAMILY_COLOR[6];
      color.setHex(hex);
      // Dim rare voicings slightly so common ones read as "mass".
      const b = 0.55 + 0.45 * (1 - v.rarity_rank);
      starMesh.instanceColor.setXYZ(i, color.r * b, color.g * b, color.b * b);
    });
    starMesh.instanceMatrix.needsUpdate = true;
    starMesh.instanceColor.needsUpdate = true;
    scene.add(starMesh);

    // -----------------------------------------------------------------------
    // Starfield backdrop — cheap parallax for depth
    // -----------------------------------------------------------------------
    const starfieldCount = 1500;
    const starfieldGeom = new THREE.BufferGeometry();
    const starfieldPos = new Float32Array(starfieldCount * 3);
    for (let i = 0; i < starfieldCount; i++) {
      const r = 80 + Math.random() * 40;
      const t = Math.random() * Math.PI * 2;
      const p = Math.acos(2 * Math.random() - 1);
      starfieldPos[i * 3 + 0] = r * Math.sin(p) * Math.cos(t);
      starfieldPos[i * 3 + 1] = r * Math.sin(p) * Math.sin(t);
      starfieldPos[i * 3 + 2] = r * Math.cos(p);
    }
    starfieldGeom.setAttribute('position', new THREE.BufferAttribute(starfieldPos, 3));
    const starfield = new THREE.Points(
      starfieldGeom,
      new THREE.PointsMaterial({ color: 0x888888, size: 0.12, sizeAttenuation: true }),
    );
    scene.add(starfield);

    // -----------------------------------------------------------------------
    // Animation loop
    // -----------------------------------------------------------------------
    const animate = () => {
      controls.update();
      renderer.render(scene, camera);
      frameRef.current = requestAnimationFrame(animate);
    };
    animate();

    // -----------------------------------------------------------------------
    // Resize
    // -----------------------------------------------------------------------
    const onResize = () => {
      if (!container) return;
      camera.aspect = container.clientWidth / container.clientHeight;
      camera.updateProjectionMatrix();
      renderer.setSize(container.clientWidth, container.clientHeight);
    };
    const ro = new ResizeObserver(onResize);
    ro.observe(container);

    return () => {
      if (frameRef.current !== null) cancelAnimationFrame(frameRef.current);
      ro.disconnect();
      controls.dispose();
      cloudGeom.dispose();
      cloudMat.dispose();
      starGeom.dispose();
      starMat.dispose();
      starfieldGeom.dispose();
      renderer.dispose();
      if (renderer.domElement.parentNode === container) {
        container.removeChild(renderer.domElement);
      }
    };
  }, []);

  return (
    <Box sx={{ position: 'relative', width: '100%', height: '100%', overflow: 'hidden' }}>
      <div
        ref={containerRef}
        style={{ position: 'absolute', inset: 0, background: '#05060a' }}
      />
      <Paper
        elevation={4}
        sx={{
          position: 'absolute',
          top: 16,
          left: 16,
          padding: 2,
          maxWidth: 360,
          backgroundColor: 'rgba(10,12,18,0.78)',
          backdropFilter: 'blur(8px)',
          color: '#e5e7eb',
        }}
      >
        <Typography variant="h6" sx={{ mb: 0.5 }}>
          Harmonic Nebula
        </Typography>
        <Typography variant="caption" sx={{ display: 'block', opacity: 0.7, mb: 1 }}>
          OPTIC-K voicing corpus · Phase 2 scaffold (synthetic data)
        </Typography>
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 0.5 }}>
          <Chip
            label={`${stats.clusters} clouds`}
            size="small"
            sx={{ backgroundColor: 'rgba(255,255,255,0.1)', color: '#e5e7eb' }}
          />
          <Chip
            label={`${stats.voicings} voicings`}
            size="small"
            sx={{ backgroundColor: 'rgba(255,255,255,0.1)', color: '#e5e7eb' }}
          />
          {Object.keys(INSTRUMENT_LABEL_PREFIX).map((k) => (
            <Chip
              key={k}
              label={INSTRUMENT_LABEL_PREFIX[k]}
              size="small"
              sx={{ backgroundColor: 'rgba(255,255,255,0.06)', color: '#9ca3af' }}
            />
          ))}
        </Stack>
        <Typography variant="caption" sx={{ display: 'block', opacity: 0.5, mt: 1 }}>
          Drag to orbit · scroll to zoom · auto-rotating
        </Typography>
      </Paper>
      <Paper
        elevation={0}
        sx={{
          position: 'absolute',
          bottom: 16,
          left: 16,
          padding: 1,
          backgroundColor: 'rgba(10,12,18,0.6)',
          color: '#9ca3af',
          fontSize: 11,
        }}
      >
        Phase 1.5 will swap this for <code>state/viz/*.json</code> from{' '}
        <code>ix-voicings viz-precompute</code>.
      </Paper>
    </Box>
  );
};

export default HarmonicNebulaDemo;
