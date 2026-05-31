/**
 * Inverse Kinematics test page — fully client-side.
 *
 * Picks a chord shape from a built-in library and shows a 3D hand
 * skeleton solving FABRIK toward the fretboard targets in real time.
 * Sliders expose IK iterations + damping so the user can see the solver
 * settle from a stiff "snap" into a smooth tween.
 */

import React, { useEffect, useState } from 'react';
import {
  Box,
  Typography,
  Slider,
  Switch,
  FormControlLabel,
  Stack,
  Chip,
} from '@mui/material';
import { InverseKinematics, CHORD_LIBRARY } from '../components/InverseKinematics';
import ResponsiveDemoShell, { useIsMobile } from '../components/Common/ResponsiveDemoShell';
import { DemoErrorBoundary } from '../components/Common/DemoErrorBoundary';

const InverseKinematicsTest: React.FC = () => {
  const isMobile = useIsMobile();

  const [chordName, setChordName] = useState<string>(CHORD_LIBRARY[0].name);
  const [ikIterations, setIkIterations] = useState<number>(6);
  const [ikDamping, setIkDamping] = useState<number>(0.18);
  const [showTargets, setShowTargets] = useState<boolean>(true);
  const [autoRotate, setAutoRotate] = useState<boolean>(false);

  const chord = CHORD_LIBRARY.find((c) => c.name === chordName) ?? CHORD_LIBRARY[0];

  // MCP control surface: register chord/control setters on window.__gaIK
  // so chrome-devtools / playwright MCP can drive the demo via
  // evaluate_script. The InverseKinematics component contributes the
  // scene-introspection half (getSceneState / validate).
  useEffect(() => {
    const w = window as unknown as { __gaIK?: Record<string, unknown> };
    const ikApi = w.__gaIK ?? {};
    ikApi.listChords = () => CHORD_LIBRARY.map((c) => c.name);
    ikApi.getCurrentChord = () => chordName;
    ikApi.setChord = (name: string) => {
      const found = CHORD_LIBRARY.find((c) => c.name === name);
      if (!found) return false;
      setChordName(name);
      return true;
    };
    ikApi.setControls = (opts: Partial<{
      ikIterations: number;
      ikDamping: number;
      showTargets: boolean;
      autoRotate: boolean;
    }>) => {
      if (typeof opts.ikIterations === 'number') setIkIterations(opts.ikIterations);
      if (typeof opts.ikDamping === 'number') setIkDamping(opts.ikDamping);
      if (typeof opts.showTargets === 'boolean') setShowTargets(opts.showTargets);
      if (typeof opts.autoRotate === 'boolean') setAutoRotate(opts.autoRotate);
      return true;
    };
    w.__gaIK = ikApi;
    return () => {
      delete ikApi.listChords;
      delete ikApi.getCurrentChord;
      delete ikApi.setChord;
      delete ikApi.setControls;
    };
  }, [chordName]);

  const sliderSx = {
    color: '#ffd54f',
    '& .MuiSlider-thumb': { backgroundColor: '#ffd54f' },
    '& .MuiSlider-track': { backgroundColor: '#ffd54f' },
    '& .MuiSlider-rail':  { backgroundColor: '#ffd54f', opacity: 0.3 },
  };
  const labelSx = { color: '#f4ddc0', fontFamily: 'monospace', mb: 1 };
  const headSx  = { color: '#ffd54f', fontFamily: 'monospace', mb: 1, mt: 2 };

  const controls = (
    <>
      <Typography variant="h5" sx={{ color: '#ffd54f', fontFamily: 'monospace', mb: 1 }}>
        🤖 INVERSE KINEMATICS
      </Typography>
      <Typography variant="caption" sx={{ color: '#a99dcc', fontFamily: 'monospace', display: 'block', mb: 2 }}>
        FABRIK hand solver on a 3D fretboard
      </Typography>

      <Typography variant="subtitle2" sx={headSx}>CHORD</Typography>
      <Stack direction="row" gap={1} sx={{ flexWrap: 'wrap', mb: 2 }}>
        {CHORD_LIBRARY.map((c) => (
          <Chip
            key={c.name}
            label={c.name}
            size="small"
            onClick={() => setChordName(c.name)}
            sx={{
              cursor: 'pointer',
              fontFamily: 'monospace',
              fontSize: 12,
              bgcolor: chordName === c.name ? '#ffd54f' : 'rgba(255,213,79,0.12)',
              color: chordName === c.name ? '#1a1a1a' : '#f4ddc0',
              border: '1px solid #ffd54f55',
              '&:hover': { bgcolor: chordName === c.name ? '#ffd54f' : 'rgba(255,213,79,0.25)' },
            }}
          />
        ))}
      </Stack>

      <Typography variant="subtitle2" sx={headSx}>SOLVER</Typography>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>FABRIK iterations: {ikIterations}</Typography>
        <Slider value={ikIterations} onChange={(_, v) => setIkIterations(v as number)} min={1} max={16} step={1} sx={sliderSx} />
      </Box>
      <Box sx={{ mb: 2 }}>
        <Typography variant="body2" sx={labelSx}>Tween damping: {ikDamping.toFixed(2)}</Typography>
        <Slider value={ikDamping} onChange={(_, v) => setIkDamping(v as number)} min={0.02} max={1} step={0.02} sx={sliderSx} />
      </Box>

      <FormControlLabel
        control={<Switch checked={showTargets} onChange={(_, v) => setShowTargets(v)} />}
        label={<span style={{ color: '#f4ddc0', fontFamily: 'monospace', fontSize: 13 }}>Show fret targets</span>}
      />
      <FormControlLabel
        control={<Switch checked={autoRotate} onChange={(_, v) => setAutoRotate(v)} />}
        label={<span style={{ color: '#f4ddc0', fontFamily: 'monospace', fontSize: 13 }}>Auto-rotate</span>}
      />

      <Typography variant="caption" sx={{ color: '#a99dcc', fontFamily: 'monospace', display: 'block', mt: 3 }}>
        {isMobile ? 'Pinch to zoom · drag to look' : 'Drag to look · scroll to zoom'}
      </Typography>
      <Typography variant="caption" sx={{ color: '#a99dcc', fontFamily: 'monospace', display: 'block', mt: 1 }}>
        Yellow spheres are the IK targets behind each fret. Hand bones tween toward them via FABRIK each frame.
      </Typography>
    </>
  );

  const viewport = (
    <InverseKinematics
      chord={chord}
      ikIterations={ikIterations}
      ikDamping={ikDamping}
      showTargets={showTargets}
      autoRotate={autoRotate}
    />
  );

  return (
    <DemoErrorBoundary demoName="Inverse Kinematics">
      <ResponsiveDemoShell
        viewport={viewport}
        controls={controls}
        backgroundColor="#0d1014"
        panelBackgroundColor="rgba(15, 18, 22, 0.96)"
        panelBorderColor="#3a3625"
        cogColor="#ffd54f"
      />
    </DemoErrorBoundary>
  );
};

export default InverseKinematicsTest;
