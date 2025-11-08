import React, { useState } from 'react';
import { 
  Container, 
  Typography, 
  Paper, 
  Box, 
  Button, 
  TextField,
  Grid,
  Chip,
  Alert,
  Divider
} from '@mui/material';
import * as THREE from 'three';
import { HarmonicNavigator3D, HarmonicRegion, PluckerLine } from '../components/BSP';

/**
 * HarmonicNavigator3DTest
 * 
 * Test page for the HarmonicNavigator3D component.
 * Demonstrates:
 * - BSP-like tetrahedral cell visualization
 * - Quaternion-based modulation
 * - Plücker voice-leading paths
 * - Integration with musical data
 */

// Sample harmonic regions (modes/scales)
const SAMPLE_REGIONS: HarmonicRegion[] = [
  {
    id: 'ionian_c',
    name: 'C Ionian (Major)',
    pcs: [0, 2, 4, 5, 7, 9, 11], // C D E F G A B
    tonic: 0,
    family: 'Ionian',
    cell: [
      new THREE.Vector3(1.0, 0, 0.2),
      new THREE.Vector3(1.25, 0, -0.15),
      new THREE.Vector3(1.0, 0.22, -0.15),
      new THREE.Vector3(0.82, -0.12, -0.05),
    ],
    color: 0x6aa3ff,
  },
  {
    id: 'dorian_d',
    name: 'D Dorian',
    pcs: [2, 4, 5, 7, 9, 11, 0], // D E F G A B C
    tonic: 2,
    family: 'Dorian',
    cell: [
      new THREE.Vector3(0.5, 0.87, 0.2),
      new THREE.Vector3(0.75, 0.87, -0.15),
      new THREE.Vector3(0.5, 1.09, -0.15),
      new THREE.Vector3(0.32, 0.75, -0.05),
    ],
    color: 0xff8b6e,
  },
  {
    id: 'phrygian_e',
    name: 'E Phrygian',
    pcs: [4, 5, 7, 9, 11, 0, 2], // E F G A B C D
    tonic: 4,
    family: 'Phrygian',
    cell: [
      new THREE.Vector3(-0.5, 0.87, 0.2),
      new THREE.Vector3(-0.25, 0.87, -0.15),
      new THREE.Vector3(-0.5, 1.09, -0.15),
      new THREE.Vector3(-0.68, 0.75, -0.05),
    ],
    color: 0x8affc1,
  },
  {
    id: 'lydian_f',
    name: 'F Lydian',
    pcs: [5, 7, 9, 11, 0, 2, 4], // F G A B C D E
    tonic: 5,
    family: 'Lydian',
    cell: [
      new THREE.Vector3(-1.0, 0, 0.2),
      new THREE.Vector3(-0.75, 0, -0.15),
      new THREE.Vector3(-1.0, 0.22, -0.15),
      new THREE.Vector3(-1.18, -0.12, -0.05),
    ],
    color: 0xffe66e,
  },
  {
    id: 'mixolydian_g',
    name: 'G Mixolydian',
    pcs: [7, 9, 11, 0, 2, 4, 5], // G A B C D E F
    tonic: 7,
    family: 'Mixolydian',
    cell: [
      new THREE.Vector3(-0.5, -0.87, 0.2),
      new THREE.Vector3(-0.25, -0.87, -0.15),
      new THREE.Vector3(-0.5, -0.65, -0.15),
      new THREE.Vector3(-0.68, -0.99, -0.05),
    ],
    color: 0xd7aaff,
  },
  {
    id: 'aeolian_a',
    name: 'A Aeolian (Natural Minor)',
    pcs: [9, 11, 0, 2, 4, 5, 7], // A B C D E F G
    tonic: 9,
    family: 'Aeolian',
    cell: [
      new THREE.Vector3(0.5, -0.87, 0.2),
      new THREE.Vector3(0.75, -0.87, -0.15),
      new THREE.Vector3(0.5, -0.65, -0.15),
      new THREE.Vector3(0.32, -0.99, -0.05),
    ],
    color: 0x7bdff2,
  },
];

// Sample Plücker voice-leading paths
const SAMPLE_PATHS: PluckerLine[] = [
  {
    L: new THREE.Vector3(1, 0, 0),
    M: new THREE.Vector3(0, 1, 0),
    fromChord: [0, 4, 7], // C major
    toChord: [7, 11, 2], // G major
    color: 0xff7a7a,
  },
  {
    L: new THREE.Vector3(0, 1, 0),
    M: new THREE.Vector3(0, 0, 1),
    fromChord: [7, 11, 2], // G major
    toChord: [9, 0, 4], // A minor
    color: 0x7aff7a,
  },
  {
    L: new THREE.Vector3(0, 0, 1),
    M: new THREE.Vector3(1, 0, 0),
    fromChord: [9, 0, 4], // A minor
    toChord: [5, 9, 0], // F major
    color: 0x7a7aff,
  },
];

export const HarmonicNavigator3DTest: React.FC = () => {
  const [regions, setRegions] = useState<HarmonicRegion[]>(SAMPLE_REGIONS);
  const [chordPaths, setChordPaths] = useState<PluckerLine[]>(SAMPLE_PATHS);
  const [selectedRegion, setSelectedRegion] = useState<string | null>(null);
  const [showPaths, setShowPaths] = useState(true);

  const handleSelectRegion = (id: string) => {
    setSelectedRegion(id);
    console.log('Selected region:', id);
  };

  const handleTogglePaths = () => {
    setShowPaths(!showPaths);
  };

  const handleResetView = () => {
    setRegions([...SAMPLE_REGIONS]);
    setChordPaths([...SAMPLE_PATHS]);
    setSelectedRegion(null);
  };

  const selectedRegionData = regions.find(r => r.id === selectedRegion);

  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Typography variant="h3" gutterBottom>
        🎸 Harmonic Navigator 3D Test
      </Typography>
      
      <Alert severity="info" sx={{ mb: 3 }}>
        <Typography variant="body2">
          <strong>Harmonic Navigator 3D</strong> visualizes musical spaces using:
        </Typography>
        <ul style={{ marginTop: 8, marginBottom: 0 }}>
          <li><strong>BSP Tetrahedral Cells</strong> - Each mode/scale is a 3D region</li>
          <li><strong>Quaternion Rotations</strong> - Smooth modulation between keys</li>
          <li><strong>Plücker Lines</strong> - Voice-leading paths between chords</li>
          <li><strong>Interactive Navigation</strong> - Click cells, drag to orbit, scroll to zoom</li>
        </ul>
      </Alert>

      <Grid container spacing={3}>
        {/* Visualization */}
        <Grid item xs={12} lg={8}>
          <Paper elevation={3} sx={{ p: 2, height: 700 }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="h6">3D Harmonic Space</Typography>
              <Box>
                <Button 
                  variant={showPaths ? "contained" : "outlined"} 
                  size="small" 
                  onClick={handleTogglePaths}
                  sx={{ mr: 1 }}
                >
                  {showPaths ? 'Hide' : 'Show'} Paths
                </Button>
                <Button 
                  variant="outlined" 
                  size="small" 
                  onClick={handleResetView}
                >
                  Reset View
                </Button>
              </Box>
            </Box>
            
            <Box sx={{ height: 600, bgcolor: '#0e1013', borderRadius: 1 }}>
              <HarmonicNavigator3D
                regions={regions}
                chordPaths={showPaths ? chordPaths : []}
                onSelectRegion={handleSelectRegion}
                width={800}
                height={600}
              />
            </Box>
          </Paper>
        </Grid>

        {/* Info Panel */}
        <Grid item xs={12} lg={4}>
          <Paper elevation={3} sx={{ p: 3, height: 700, overflow: 'auto' }}>
            <Typography variant="h6" gutterBottom>
              Region Information
            </Typography>
            
            {selectedRegionData ? (
              <Box>
                <Chip 
                  label={selectedRegionData.name} 
                  color="primary" 
                  sx={{ mb: 2 }}
                />
                
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  <strong>ID:</strong> {selectedRegionData.id}
                </Typography>
                
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  <strong>Family:</strong> {selectedRegionData.family}
                </Typography>
                
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  <strong>Tonic:</strong> {['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'][selectedRegionData.tonic]}
                </Typography>
                
                <Typography variant="body2" color="text.secondary" gutterBottom>
                  <strong>Pitch Classes:</strong> {selectedRegionData.pcs.join(', ')}
                </Typography>
                
                <Divider sx={{ my: 2 }} />
                
                <Typography variant="body2" color="text.secondary">
                  <strong>Notes:</strong>
                </Typography>
                <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5, mt: 1 }}>
                  {selectedRegionData.pcs.map((pc: number) => (
                    <Chip
                      key={pc}
                      label={['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'][pc]}
                      size="small"
                      variant="outlined"
                    />
                  ))}
                </Box>
              </Box>
            ) : (
              <Alert severity="info">
                Click on a tetrahedral cell in the 3D view to see region details.
              </Alert>
            )}
            
            <Divider sx={{ my: 3 }} />
            
            <Typography variant="h6" gutterBottom>
              Available Regions
            </Typography>
            
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1 }}>
              {regions.map(region => (
                <Chip
                  key={region.id}
                  label={region.name}
                  onClick={() => handleSelectRegion(region.id)}
                  color={selectedRegion === region.id ? 'primary' : 'default'}
                  variant={selectedRegion === region.id ? 'filled' : 'outlined'}
                />
              ))}
            </Box>
            
            <Divider sx={{ my: 3 }} />
            
            <Typography variant="h6" gutterBottom>
              Voice-Leading Paths
            </Typography>
            
            <Typography variant="body2" color="text.secondary" gutterBottom>
              {chordPaths.length} Plücker lines connecting chord progressions
            </Typography>
            
            {showPaths && (
              <Box sx={{ mt: 2 }}>
                {chordPaths.map((path, idx) => (
                  <Box key={idx} sx={{ mb: 1 }}>
                    <Typography variant="caption" color="text.secondary">
                      Path {idx + 1}: {path.fromChord.join('-')} → {path.toChord.join('-')}
                    </Typography>
                  </Box>
                ))}
              </Box>
            )}
          </Paper>
        </Grid>
      </Grid>

      <Paper elevation={3} sx={{ p: 3, mt: 3 }}>
        <Typography variant="h6" gutterBottom>
          Implementation Notes
        </Typography>
        
        <Typography variant="body2" color="text.secondary" paragraph>
          This component demonstrates advanced harmonic navigation concepts:
        </Typography>
        
        <Grid container spacing={2}>
          <Grid item xs={12} md={6}>
            <Typography variant="subtitle2" gutterBottom>
              <strong>BSP Partitioning</strong>
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Each mode/scale occupies a tetrahedral cell in 3D space. The BSP tree structure
              allows efficient spatial queries and hierarchical organization of tonal regions.
            </Typography>
          </Grid>
          
          <Grid item xs={12} md={6}>
            <Typography variant="subtitle2" gutterBottom>
              <strong>Quaternion Modulation</strong>
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Key changes are represented as quaternion rotations, providing smooth interpolation
              between tonal centers using SLERP (Spherical Linear Interpolation).
            </Typography>
          </Grid>
          
          <Grid item xs={12} md={6}>
            <Typography variant="subtitle2" gutterBottom>
              <strong>Plücker Coordinates</strong>
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Voice-leading paths are visualized as tubes using Plücker line representation,
              showing the geometric relationship between chord transitions.
            </Typography>
          </Grid>
          
          <Grid item xs={12} md={6}>
            <Typography variant="subtitle2" gutterBottom>
              <strong>Interactive Controls</strong>
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Drag to orbit the camera, scroll to zoom, and click on cells to select regions.
              The key wheel at the bottom can be used for modulation (future enhancement).
            </Typography>
          </Grid>
        </Grid>
      </Paper>
    </Container>
  );
};

export default HarmonicNavigator3DTest;

