/**
 * Sunburst3DDemo — drives the 3D sunburst with a complete musical-theory
 * hierarchy (Forte set classes, triads/sevenths/extended chords, voicing
 * families, modal/pentatonic/blues scales). Builds the tree
 * programmatically so set-class counts are exhaustive instead of the
 * truncated samples the previous version shipped with.
 */

import React, { useMemo, useState } from 'react';
import {
  Box,
  Typography,
  Paper,
  Stack,
  Slider,
  FormControlLabel,
  Switch,
  Breadcrumbs,
  Link,
  Chip,
  Button,
} from '@mui/material';
import HomeIcon from '@mui/icons-material/Home';
import { Sunburst3D, SunburstNode } from './Sunburst3D';

// ──────────────────────────────────────────────────────────────────────
// Catalog generators
// ──────────────────────────────────────────────────────────────────────

const NOTE_NAMES = ['C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'Ab', 'A', 'Bb', 'B'];

// Z-pair labelling for Forte tetrachords (the only cardinality with a small
// enough Z-set to spell out inline; pentachords/hexachords use plain "n-i").
const TETRACHORD_Z_INDICES = new Set([15, 29]);

function buildSetClass(cardinality: number, count: number, zIndices?: Set<number>): SunburstNode {
  const children: SunburstNode[] = Array.from({ length: count }, (_, i) => {
    const idx = i + 1;
    const z = zIndices?.has(idx) ? 'Z' : '';
    return { name: `${cardinality}-${z}${idx}`, value: 1 };
  });
  return { name: pluraliseCardinality(cardinality), value: count, children };
}

function pluraliseCardinality(n: number): string {
  return ({ 3: 'Trichords', 4: 'Tetrachords', 5: 'Pentachords', 6: 'Hexachords' } as Record<number, string>)[n] ?? `${n}-chords`;
}

function buildChordsByRoot(quality: string, abbreviation: string): SunburstNode {
  return {
    name: quality,
    value: 12,
    children: NOTE_NAMES.map((note) => ({
      name: `${note}${abbreviation}`,
      value: 1,
    })),
  };
}

function buildAugmented(): SunburstNode {
  // Augmented triads collapse to 4 distinct equivalence classes under
  // T-transposition (C+ = E+ = Ab+, etc.).
  return {
    name: 'Augmented',
    value: 4,
    children: ['C+', 'C#+', 'D+', 'Eb+'].map((n) => ({ name: n, value: 1 })),
  };
}

function buildModes(): SunburstNode {
  const modes = ['Ionian', 'Dorian', 'Phrygian', 'Lydian', 'Mixolydian', 'Aeolian', 'Locrian'];
  return {
    name: 'Major Modes',
    color: 0xff66ff,
    children: modes.map((m) => ({
      name: m,
      value: 12,
      children: NOTE_NAMES.map((root) => ({ name: `${root} ${m}`, value: 1 })),
    })),
  };
}

function buildMelodicMinorModes(): SunburstNode {
  const modes = [
    'Melodic Minor', 'Dorian b2', 'Lydian Augmented', 'Lydian Dominant',
    'Mixolydian b6', 'Locrian #2', 'Altered',
  ];
  return {
    name: 'Melodic Minor Modes',
    color: 0xff44aa,
    children: modes.map((m) => ({ name: m, value: 12 })),
  };
}

function buildHarmonicMinorModes(): SunburstNode {
  const modes = [
    'Harmonic Minor', 'Locrian #6', 'Ionian #5', 'Dorian #4',
    'Phrygian Dominant', 'Lydian #2', 'Super Locrian bb7',
  ];
  return {
    name: 'Harmonic Minor Modes',
    color: 0xaa44ff,
    children: modes.map((m) => ({ name: m, value: 12 })),
  };
}

// ──────────────────────────────────────────────────────────────────────
// Top-level tree
// ──────────────────────────────────────────────────────────────────────

const musicalHierarchyData: SunburstNode = {
  name: 'Music Theory',
  children: [
    {
      name: 'Pitch Class Sets',
      color: 0x00ffff,
      children: [
        buildSetClass(3, 12),
        buildSetClass(4, 29, TETRACHORD_Z_INDICES),
        buildSetClass(5, 38),
        buildSetClass(6, 50),
      ],
    },
    {
      name: 'Chords',
      color: 0xffff00,
      children: [
        {
          name: 'Triads',
          children: [
            buildChordsByRoot('Major', ''),
            buildChordsByRoot('Minor', 'm'),
            buildChordsByRoot('Diminished', '°'),
            buildAugmented(),
            buildChordsByRoot('Suspended 2', 'sus2'),
            buildChordsByRoot('Suspended 4', 'sus4'),
          ],
        },
        {
          name: 'Seventh Chords',
          children: [
            buildChordsByRoot('Major 7th', 'maj7'),
            buildChordsByRoot('Minor 7th', 'm7'),
            buildChordsByRoot('Dominant 7th', '7'),
            buildChordsByRoot('Half-Diminished', 'm7b5'),
            buildChordsByRoot('Diminished 7th', '°7'),
            buildChordsByRoot('Minor-Major 7th', 'mM7'),
          ],
        },
        {
          name: 'Extended Chords',
          children: [
            buildChordsByRoot('9th', '9'),
            buildChordsByRoot('Major 9th', 'maj9'),
            buildChordsByRoot('Minor 9th', 'm9'),
            buildChordsByRoot('11th', '11'),
            buildChordsByRoot('13th', '13'),
            buildChordsByRoot('Add9', 'add9'),
          ],
        },
        {
          name: 'Altered Dominants',
          children: [
            buildChordsByRoot('7♭9', '7b9'),
            buildChordsByRoot('7♯9', '7#9'),
            buildChordsByRoot('7♭5', '7b5'),
            buildChordsByRoot('7♯5', '7#5'),
            buildChordsByRoot('7alt', '7alt'),
          ],
        },
      ],
    },
    {
      name: 'Voicings',
      color: 0xff8800,
      children: [
        {
          name: 'Jazz Voicings',
          color: 0x00ff00,
          children: [
            { name: 'Drop 2', children: ['Maj7', 'Min7', 'Dom7', 'Min7b5', 'Dim7'].map((q) => ({ name: q, value: 48 })) },
            { name: 'Drop 3', children: ['Maj7', 'Min7', 'Dom7', 'Min7b5'].map((q) => ({ name: q, value: 48 })) },
            { name: 'Drop 2+4', children: ['Maj7', 'Min7', 'Dom7'].map((q) => ({ name: q, value: 48 })) },
            { name: 'Rootless', children: [{ name: 'Type A', value: 24 }, { name: 'Type B', value: 24 }] },
            { name: 'Shell Voicings', children: [{ name: 'Root-3-7', value: 24 }, { name: 'Root-7-3', value: 24 }] },
            { name: 'Quartal', children: [{ name: '4ths', value: 36 }, { name: 'Sus4', value: 24 }, { name: 'Add11', value: 24 }] },
          ],
        },
        {
          name: 'Classical Voicings',
          color: 0x0088ff,
          children: [
            { name: 'Close Position', value: 100 },
            { name: 'Open Position', value: 100 },
            { name: 'Four-Part', value: 80 },
            { name: 'SATB', value: 80 },
          ],
        },
        {
          name: 'Rock Voicings',
          color: 0xff0088,
          children: [
            { name: 'Power Chords', value: 60 },
            { name: 'Barre Chords', value: 120 },
            { name: 'Open Chords', value: 80 },
            { name: 'Triads', value: 100 },
          ],
        },
        {
          name: 'CAGED System',
          color: 0xffff00,
          children: ['C Shape', 'A Shape', 'G Shape', 'E Shape', 'D Shape'].map((s) => ({ name: s, value: 50 })),
        },
      ],
    },
    {
      name: 'Scales',
      color: 0xff00ff,
      children: [
        buildModes(),
        buildMelodicMinorModes(),
        buildHarmonicMinorModes(),
        {
          name: 'Pentatonic',
          children: [
            { name: 'Major Pentatonic', value: 12 },
            { name: 'Minor Pentatonic', value: 12 },
            { name: 'Suspended Pentatonic', value: 12 },
            { name: 'Egyptian', value: 12 },
            { name: 'Blues Minor Pentatonic', value: 12 },
          ],
        },
        {
          name: 'Symmetric Scales',
          children: [
            { name: 'Whole Tone', value: 2 },
            { name: 'Diminished (W-H)', value: 3 },
            { name: 'Diminished (H-W)', value: 3 },
            { name: 'Chromatic', value: 1 },
            { name: 'Augmented', value: 4 },
          ],
        },
        { name: 'Blues', value: 12 },
      ],
    },
  ],
};

// ──────────────────────────────────────────────────────────────────────
// Path navigation helper — walks `path` from the root, returning the
// matching subtree (or null if any segment doesn't resolve).
// ──────────────────────────────────────────────────────────────────────

function subtreeAtPath(root: SunburstNode, path: string[]): SunburstNode | null {
  let node: SunburstNode | null = root;
  for (const segment of path) {
    if (!node?.children) return null;
    node = node.children.find((c) => c.name === segment) ?? null;
    if (!node) return null;
  }
  return node;
}

// ──────────────────────────────────────────────────────────────────────
// Demo Component
// ──────────────────────────────────────────────────────────────────────

export const Sunburst3DDemo: React.FC = () => {
  const [maxDepth, setMaxDepth] = useState<number>(4);
  const [slopeAngle, setSlopeAngle] = useState<number>(30);
  const [autoRotate, setAutoRotate] = useState<boolean>(true);
  const [selectedNode, setSelectedNode] = useState<SunburstNode | null>(null);
  const [selectedPath, setSelectedPath] = useState<string[]>([]);
  // `viewPath` controls which subtree the renderer shows; clicking a node with
  // children pushes a deeper view, clicking the breadcrumb pops back.
  const [viewPath, setViewPath] = useState<string[]>([]);

  const viewData = useMemo(() => subtreeAtPath(musicalHierarchyData, viewPath) ?? musicalHierarchyData, [viewPath]);

  const handleNodeClick = (node: SunburstNode, path: string[]) => {
    setSelectedNode(node);
    setSelectedPath(path);
    if (node.children && node.children.length > 0) {
      setViewPath([...viewPath, ...path]);
    }
  };

  const popTo = (depth: number) => {
    setViewPath(viewPath.slice(0, depth));
    setSelectedNode(null);
    setSelectedPath([]);
  };

  return (
    <Box sx={{ width: '100%', height: 'calc(100vh - 48px)', backgroundColor: '#000', overflow: 'hidden' }}>
      <Stack direction="row" sx={{ height: '100%', width: '100%' }}>
        <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column', position: 'relative' }}>
          {/* Breadcrumb — sits on top of the canvas, click to navigate up */}
          <Box sx={{
            position: 'absolute',
            top: 12,
            left: 12,
            zIndex: 2,
            backgroundColor: 'rgba(0, 0, 0, 0.6)',
            border: '1px solid #0f0',
            borderRadius: 1,
            px: 1.5,
            py: 0.75,
            maxWidth: 'calc(100% - 24px)',
          }}>
            <Breadcrumbs separator="›" sx={{ '& .MuiBreadcrumbs-separator': { color: '#0f0' } }}>
              <Link
                component="button"
                onClick={() => popTo(0)}
                sx={{ color: viewPath.length === 0 ? '#0ff' : '#0f0', display: 'flex', alignItems: 'center', gap: 0.5, fontFamily: 'monospace', fontSize: '13px', textDecoration: 'none' }}
              >
                <HomeIcon sx={{ fontSize: 16 }} /> Music Theory
              </Link>
              {viewPath.map((segment, i) => (
                <Link
                  key={`${i}-${segment}`}
                  component="button"
                  onClick={() => popTo(i + 1)}
                  sx={{ color: i === viewPath.length - 1 ? '#0ff' : '#0f0', fontFamily: 'monospace', fontSize: '13px', textDecoration: 'none' }}
                >
                  {segment}
                </Link>
              ))}
            </Breadcrumbs>
          </Box>

          <Sunburst3D
            data={viewData}
            width={window.innerWidth - 320}
            height={window.innerHeight - 48}
            maxDepth={maxDepth}
            slopeAngle={slopeAngle}
            onNodeClick={handleNodeClick}
          />
        </Box>

        <Paper
          sx={{
            width: 320,
            padding: 3,
            backgroundColor: 'rgba(0, 0, 0, 0.9)',
            border: '1px solid #0f0',
            overflowY: 'auto',
          }}
        >
          <Typography variant="h5" sx={{ color: '#0f0', fontFamily: 'monospace', mb: 3 }}>
            3D Sunburst Controls
          </Typography>

          <Box sx={{ mb: 3 }}>
            <Typography sx={{ color: '#0f0', fontSize: '14px', mb: 1 }}>
              Max Depth (LOD): {maxDepth}
            </Typography>
            <Slider
              value={maxDepth}
              onChange={(_, value) => setMaxDepth(value as number)}
              min={1}
              max={6}
              step={1}
              marks
              sx={{
                color: '#0f0',
                '& .MuiSlider-thumb': { backgroundColor: '#0f0' },
                '& .MuiSlider-track': { backgroundColor: '#0f0' },
                '& .MuiSlider-rail': { backgroundColor: '#333' },
              }}
            />
            <Typography sx={{ color: '#888', fontSize: '11px', mt: 1 }}>
              Controls how many levels are rendered (Level of Detail)
            </Typography>
          </Box>

          <Box sx={{ mb: 3 }}>
            <Typography sx={{ color: '#0f0', fontSize: '14px', mb: 1 }}>
              Slope Angle: {slopeAngle}°
            </Typography>
            <Slider
              value={slopeAngle}
              onChange={(_, value) => setSlopeAngle(value as number)}
              min={0}
              max={60}
              step={5}
              marks
              sx={{
                color: '#0f0',
                '& .MuiSlider-thumb': { backgroundColor: '#0f0' },
                '& .MuiSlider-track': { backgroundColor: '#0f0' },
                '& .MuiSlider-rail': { backgroundColor: '#333' },
              }}
            />
            <Typography sx={{ color: '#888', fontSize: '11px', mt: 1 }}>
              Controls the elevation slope (0° = flat, 60° = steep)
            </Typography>
          </Box>

          <FormControlLabel
            control={
              <Switch
                checked={autoRotate}
                onChange={(e) => setAutoRotate(e.target.checked)}
                sx={{
                  '& .MuiSwitch-switchBase.Mui-checked': { color: '#0f0' },
                  '& .MuiSwitch-switchBase.Mui-checked + .MuiSwitch-track': { backgroundColor: '#0f0' },
                }}
              />
            }
            label={<Typography sx={{ color: '#0f0', fontSize: '14px' }}>Auto Rotate</Typography>}
            sx={{ mb: 3 }}
          />

          {viewPath.length > 0 && (
            <Box sx={{ mb: 3 }}>
              <Button
                variant="outlined"
                size="small"
                onClick={() => popTo(0)}
                sx={{ color: '#0ff', borderColor: '#0ff', fontFamily: 'monospace' }}
              >
                ← Reset view
              </Button>
            </Box>
          )}

          {selectedNode && (
            <Box sx={{ mt: 3, pt: 3, borderTop: '1px solid #0f0' }}>
              <Typography sx={{ color: '#0ff', fontSize: '16px', fontWeight: 'bold', mb: 1 }}>
                Selected: {selectedNode.name}
              </Typography>
              <Typography sx={{ color: '#888', fontSize: '12px', mb: 1 }}>
                Path: {selectedPath.join(' → ')}
              </Typography>
              <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                {selectedNode.value !== undefined && (
                  <Chip size="small" label={`value: ${selectedNode.value}`} sx={{ bgcolor: 'rgba(0,255,0,0.15)', color: '#0f0', fontFamily: 'monospace' }} />
                )}
                {selectedNode.children && (
                  <Chip size="small" label={`${selectedNode.children.length} children`} sx={{ bgcolor: 'rgba(0,255,0,0.15)', color: '#0f0', fontFamily: 'monospace' }} />
                )}
              </Stack>
            </Box>
          )}

          <Box sx={{ mt: 3, pt: 3, borderTop: '1px solid #0f0' }}>
            <Typography sx={{ color: '#888', fontSize: '12px', mb: 1 }}>
              <strong style={{ color: '#0f0' }}>Instructions:</strong>
            </Typography>
            <Typography sx={{ color: '#888', fontSize: '11px', mb: 0.5 }}>• Hover segments to highlight</Typography>
            <Typography sx={{ color: '#888', fontSize: '11px', mb: 0.5 }}>• Click a branch to drill in</Typography>
            <Typography sx={{ color: '#888', fontSize: '11px', mb: 0.5 }}>• Click breadcrumbs to step back</Typography>
            <Typography sx={{ color: '#888', fontSize: '11px' }}>• Slope/Depth shape elevation + LOD</Typography>
          </Box>
        </Paper>
      </Stack>
    </Box>
  );
};

export default Sunburst3DDemo;
