import React, { useMemo, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  FormControl,
  Grid,
  InputLabel,
  LinearProgress,
  MenuItem,
  Paper,
  Select,
  Slider,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { ThreeFretboard, ThreeFretboardPosition } from '../components/ThreeFretboard';
import { HandVisualization3D } from '../components/HandVisualization3D';

type RawPosition = { string: number; fret: number };

interface ChordPreset {
  name: string;
  description?: string;
  positions: RawPosition[];
}

interface Vector3 {
  x: number;
  y: number;
  z: number;
}

interface Quaternion {
  x: number;
  y: number;
  z: number;
  w: number;
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
  palmOrientation: Quaternion;
  fretboardGeometry: FretboardGeometry;
}

interface IKSolution {
  playabilityScore: number;
  fingerStretch?: {
    maxStretch: number;
    maxFretSpan: number;
    description: string;
  };
  fingeringEfficiency?: {
    efficiencyScore: number;
    fingerSpan: number;
    pinkyUsagePercentage: number;
    hasBarreChord: boolean;
    usesThumb: boolean;
    reason: string;
    recommendations: string[];
  };
  wristPosture?: {
    angle: number;
    postureType: string;
    isErgonomic: boolean;
  };
  muting?: {
    technique: string;
    reason: string;
  };
  slideLegato?: {
    technique: string;
    reason: string;
  };
  visualization?: HandPoseVisualization;
}

const STRING_NAMES = ['High E', 'B', 'G', 'D', 'A', 'Low E'];
const FINGER_COLORS = ['#ff6b6b', '#4dabf7', '#51cf66', '#ffd43b', '#845ef7', '#ff922b'];

const CHORD_PRESETS: ChordPreset[] = [
  {
    name: 'C Major',
    description: 'Open position C triad',
    positions: [
      { string: 1, fret: 0 },
      { string: 2, fret: 1 },
      { string: 3, fret: 0 },
      { string: 4, fret: 2 },
      { string: 5, fret: 3 },
    ],
  },
  {
    name: 'G Major',
    description: 'Six-string G with extended bass',
    positions: [
      { string: 1, fret: 3 },
      { string: 2, fret: 0 },
      { string: 3, fret: 0 },
      { string: 4, fret: 0 },
      { string: 5, fret: 2 },
      { string: 6, fret: 3 },
    ],
  },
  {
    name: 'D Major',
    description: 'Compact D shape',
    positions: [
      { string: 2, fret: 3 },
      { string: 3, fret: 2 },
      { string: 4, fret: 0 },
      { string: 5, fret: 2 },
    ],
  },
  {
    name: 'E Minor',
    description: 'Classic open E minor',
    positions: [
      { string: 1, fret: 0 },
      { string: 2, fret: 0 },
      { string: 3, fret: 0 },
      { string: 4, fret: 2 },
      { string: 5, fret: 2 },
      { string: 6, fret: 0 },
    ],
  },
  {
    name: 'A Minor',
    description: 'Campfire A minor voicing',
    positions: [
      { string: 1, fret: 0 },
      { string: 2, fret: 1 },
      { string: 3, fret: 2 },
      { string: 4, fret: 2 },
      { string: 5, fret: 0 },
    ],
  },
];

const convertStringIndex = (value: number, stringCount = 6): number => {
  if (value >= 0 && value < stringCount) {
    return value;
  }

  if (value >= 1 && value <= stringCount) {
    const zeroBased = value - 1;
    return stringCount - 1 - zeroBased;
  }

  return Math.max(0, Math.min(stringCount - 1, value));
};

const toThreeFretboardPositions = (positions: RawPosition[]): ThreeFretboardPosition[] => {
  if (!positions.length) {
    return [];
  }

  const minNonZeroFret = positions
    .filter((pos) => pos.fret > 0)
    .reduce((acc, pos) => Math.min(acc, pos.fret), Number.POSITIVE_INFINITY);

  return positions.map((pos, index) => ({
    string: convertStringIndex(pos.string),
    fret: pos.fret,
    label: `F${index + 1}`,
    color: FINGER_COLORS[index % FINGER_COLORS.length],
    emphasized: pos.fret > 0 && pos.fret === minNonZeroFret,
  }));
};

const formatStringLabel = (value: number): string => {
  // Strings are 1-based (1-6), where 1 is high E and 6 is low E
  if (value >= 1 && value <= 6) {
    // STRING_NAMES is 0-based array: ['High E', 'B', 'G', 'D', 'A', 'Low E']
    // String 1 (High E) -> STRING_NAMES[0]
    // String 6 (Low E) -> STRING_NAMES[5]
    return `String ${value} (${STRING_NAMES[value - 1]})`;
  }

  return `String ${value}`;
};

const getPlayabilityChipColor = (score: number) => {
  if (score >= 8000) return 'success';
  if (score >= 5000) return 'warning';
  return 'error';
};

export const InverseKinematicsTest: React.FC = () => {
  const [selectedChordName, setSelectedChordName] = useState<string>(CHORD_PRESETS[0].name);
  const [customPositions, setCustomPositions] = useState('');
  const [parsedCustomPositions, setParsedCustomPositions] = useState<RawPosition[] | null>(null);
  const [inputError, setInputError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [solution, setSolution] = useState<IKSolution | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [populationSize, setPopulationSize] = useState(120);
  const [generations, setGenerations] = useState(160);

  const selectedChord = useMemo(
    () => CHORD_PRESETS.find((preset) => preset.name === selectedChordName) ?? CHORD_PRESETS[0],
    [selectedChordName],
  );

  const activePositions = parsedCustomPositions ?? selectedChord.positions;
  const fretboardPositions = useMemo(
    () => toThreeFretboardPositions(activePositions),
    [activePositions],
  );

  const handleCustomPositionsChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const value = event.target.value;
    setCustomPositions(value);

    if (!value.trim()) {
      setParsedCustomPositions(null);
      setInputError(null);
      return;
    }

    try {
      const parsed = JSON.parse(value);
      if (!Array.isArray(parsed)) {
        throw new Error('Expected an array of positions.');
      }

      const normalized: RawPosition[] = parsed.map((item, index) => {
        if (typeof item !== 'object' || item === null) {
          throw new Error(`Position at index ${index} must be an object.`);
        }

        const stringValue = Number(item.string);
        const fretValue = Number(item.fret);

        if (!Number.isFinite(stringValue) || !Number.isFinite(fretValue)) {
          throw new Error(`Position at index ${index} is missing numeric "string" and "fret" values.`);
        }

        return { string: stringValue, fret: fretValue };
      });

      setParsedCustomPositions(normalized);
      setInputError(null);
    } catch (parseError) {
      const message = parseError instanceof Error ? parseError.message : 'Invalid JSON payload.';
      setParsedCustomPositions(null);
      setInputError(message);
    }
  };

  const solveIK = async () => {
    setLoading(true);
    setError(null);

    try {
      const positions = parsedCustomPositions ?? selectedChord.positions;

      const response = await fetch('http://localhost:5232/api/biomechanics/analyze-chord', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          chordName: selectedChord.name,
          solverConfig: {
            populationSize,
            generations,
          },
          fingerAssignments: positions.map((pos, index) => ({
            string: pos.string,
            fret: pos.fret,
            finger: index + 1,
          })),
        }),
      });

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`HTTP ${response.status}: ${errorText}`);
      }

      const apiResponse = await response.json();
      if (apiResponse.success && apiResponse.data) {
        setSolution(apiResponse.data);
      } else {
        throw new Error(apiResponse.message || 'Failed to analyze chord.');
      }
    } catch (err) {
      console.error('IK solve error:', err);
      setSolution(null);
      setError(err instanceof Error ? err.message : 'Failed to solve IK.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        bgcolor: '#121212',
        color: '#e0e0e0',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <Box sx={{ px: 3, py: 1.5, borderBottom: '1px solid rgba(255, 255, 255, 0.12)' }}>
        <Typography variant="h5" fontWeight={600}>
          Inverse Kinematics Playground
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
          Biomechanical solver for chord shapes
        </Typography>
      </Box>

      <Grid container spacing={2} sx={{ flex: 1, p: 2 }}>
        <Grid item xs={12} md={4} lg={3}>
          <Stack spacing={1.5}>
            <Card sx={{ bgcolor: '#1e1e1e', border: '1px solid rgba(255, 255, 255, 0.12)' }}>
              <CardContent sx={{ '&:last-child': { pb: 1.5 } }}>
                <Typography variant="subtitle1" gutterBottom sx={{ mb: 1 }}>
                  Chord Source
                </Typography>

                <FormControl fullWidth size="small" sx={{ mb: 1 }}>
                  <InputLabel id="chord-preset-label">Preset</InputLabel>
                  <Select
                    labelId="chord-preset-label"
                    label="Preset"
                    value={selectedChordName}
                    onChange={(event) => {
                      setSelectedChordName(event.target.value);
                      setSolution(null);
                    }}
                  >
                    {CHORD_PRESETS.map((preset) => (
                      <MenuItem key={preset.name} value={preset.name}>
                        {preset.name}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                <Divider sx={{ my: 1, borderColor: 'rgba(255, 255, 255, 0.12)' }} />

                <Typography variant="caption" gutterBottom sx={{ color: 'text.primary', display: 'block', mb: 0.5 }}>
                  Custom JSON
                </Typography>
                <TextField
                  multiline
                  minRows={3}
                  placeholder='[{"string":1,"fret":3}]'
                  value={customPositions}
                  onChange={handleCustomPositionsChange}
                  spellCheck={false}
                  size="small"
                  sx={{
                    '& .MuiInputBase-root': {
                      fontFamily: 'JetBrains Mono, monospace',
                      fontSize: '0.75rem'
                    },
                  }}
                />

                {inputError && (
                  <Alert severity="warning" sx={{ mt: 1, py: 0 }}>
                    {inputError}
                  </Alert>
                )}

                <Divider sx={{ my: 1, borderColor: 'rgba(255, 255, 255, 0.12)' }} />

                <Typography variant="caption" gutterBottom sx={{ color: 'text.primary', display: 'block', mb: 0.5 }}>
                  Solver Parameters
                </Typography>

                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 500, fontSize: '0.7rem' }}>
                  Population: {populationSize}
                </Typography>
                <Slider
                  min={40}
                  max={300}
                  step={10}
                  value={populationSize}
                  onChange={(_, value) => setPopulationSize(value as number)}
                  sx={{ mb: 1 }}
                  size="small"
                />

                <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 500, fontSize: '0.7rem' }}>
                  Generations: {generations}
                </Typography>
                <Slider
                  min={60}
                  max={400}
                  step={10}
                  value={generations}
                  onChange={(_, value) => setGenerations(value as number)}
                  size="small"
                />

                <Button
                  fullWidth
                  variant="contained"
                  size="small"
                  sx={{ mt: 1 }}
                  onClick={solveIK}
                  disabled={loading || !!inputError}
                >
                  {loading ? 'Solving…' : 'Solve IK'}
                </Button>

                {loading && <LinearProgress sx={{ mt: 1 }} />}
                {error && (
                  <Alert severity="error" sx={{ mt: 1, py: 0 }}>
                    {error}
                  </Alert>
                )}
              </CardContent>
            </Card>

            <Card sx={{ bgcolor: '#1e1e1e', border: '1px solid rgba(255, 255, 255, 0.12)' }}>
              <CardContent sx={{ '&:last-child': { pb: 1.5 } }}>
                <Typography variant="subtitle1" gutterBottom sx={{ mb: 1 }}>
                  Active Fingering
                </Typography>
                <Table
                  size="small"
                  sx={{
                    '& td, & th': {
                      borderColor: 'rgba(255, 255, 255, 0.23)',
                      color: 'text.primary',
                      py: 0.5,
                      fontSize: '0.8125rem'
                    },
                    '& th': {
                      fontWeight: 600
                    }
                  }}
                >
                  <TableHead>
                    <TableRow>
                      <TableCell>String</TableCell>
                      <TableCell align="right">Fret</TableCell>
                      <TableCell align="right">Finger</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {activePositions.map((pos, index) => {
                      // Calculate finger number: only fretted notes get finger assignments
                      const frettedPositions = activePositions.filter(p => p.fret > 0);
                      const frettedIndex = frettedPositions.findIndex(p => p === pos);
                      const fingerNumber = pos.fret === 0 ? '-' : (frettedIndex + 1).toString();

                      return (
                        <TableRow key={`${pos.string}-${pos.fret}-${index}`}>
                          <TableCell>{formatStringLabel(pos.string)}</TableCell>
                          <TableCell align="right">{pos.fret}</TableCell>
                          <TableCell align="right">{fingerNumber}</TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </CardContent>
            </Card>
          </Stack>
        </Grid>

        <Grid item xs={12} md={8} lg={9}>
          <Stack spacing={1.5} sx={{ height: '100%' }}>
            <Card
              component={Paper}
              elevation={0}
              sx={{
                flex: 1,
                display: 'flex',
                flexDirection: 'column',
                bgcolor: '#1e1e1e',
                border: '1px solid rgba(255, 255, 255, 0.12)',
                overflow: 'hidden',
              }}
            >
              <CardContent sx={{ flex: 1, display: 'flex', flexDirection: 'column', '&:last-child': { pb: 1.5 } }}>
                <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
                  <Typography variant="subtitle1">
                    Fretboard Visualizer
                  </Typography>
                  <Chip
                    label={`${activePositions.length} ${activePositions.length === 1 ? 'note' : 'notes'}`}
                    size="small"
                    color="primary"
                  />
                </Stack>

                <Box sx={{ flex: 1, minHeight: 450, overflowX: 'auto' }}>
                  <ThreeFretboard
                    positions={fretboardPositions}
                    config={{
                      width: 1600,
                      height: 500,
                      enableOrbitControls: true,
                      showStringLabels: true,
                      showFretNumbers: true,
                    }}
                  />
                </Box>
              </CardContent>
            </Card>

            <Card
              sx={{
                bgcolor: '#1e1e1e',
                border: '1px solid rgba(255, 255, 255, 0.12)',
              }}
            >
              <CardContent sx={{ '&:last-child': { pb: 1.5 } }}>
                <Stack direction={{ xs: 'column', md: 'row' }} spacing={1} justifyContent="space-between" alignItems={{ md: 'center' }}>
                  <Typography variant="subtitle1">Solver Insights</Typography>
                  {solution ? (
                    <Chip
                      label={`Playability ${solution.playabilityScore.toFixed(1)}`}
                      color={getPlayabilityChipColor(solution.playabilityScore)}
                      size="small"
                    />
                  ) : (
                    <Chip label="Awaiting solve" size="small" color="default" />
                  )}
                </Stack>

                <Divider sx={{ my: 1, borderColor: 'rgba(255, 255, 255, 0.12)' }} />

                {solution ? (
                  <Grid container spacing={1}>
                    {solution.fingerStretch && (
                      <Grid item xs={12} md={4}>
                        <Card sx={{ bgcolor: '#2c2c2c', border: '1px solid rgba(255, 255, 255, 0.12)' }}>
                          <CardContent sx={{ '&:last-child': { pb: 1 } }}>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: 'block', mb: 0.5 }}>
                              Finger Stretch
                            </Typography>
                            <Typography variant="h6" sx={{ mb: 0.5 }}>
                              {solution.fingerStretch.maxStretch.toFixed(1)} mm
                            </Typography>
                            <Chip
                              label={`${solution.fingerStretch.maxFretSpan} fret span`}
                              size="small"
                            />
                          </CardContent>
                        </Card>
                      </Grid>
                    )}

                    {solution.fingeringEfficiency && (
                      <Grid item xs={12} md={4}>
                        <Card sx={{ bgcolor: '#2c2c2c', border: '1px solid rgba(255, 255, 255, 0.12)' }}>
                          <CardContent sx={{ '&:last-child': { pb: 1 } }}>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: 'block', mb: 0.5 }}>
                              Efficiency
                            </Typography>
                            <Typography variant="h6" sx={{ mb: 0.5 }}>
                              {solution.fingeringEfficiency.efficiencyScore.toFixed(1)}%
                            </Typography>
                            <Stack direction="row" spacing={0.5} sx={{ flexWrap: 'wrap', gap: 0.5 }}>
                              <Chip label={`${solution.fingeringEfficiency.fingerSpan} frets`} size="small" />
                              {solution.fingeringEfficiency.hasBarreChord && <Chip label="Barre" size="small" />}
                              {solution.fingeringEfficiency.usesThumb && <Chip label="Thumb" size="small" />}
                            </Stack>
                          </CardContent>
                        </Card>
                      </Grid>
                    )}

                    {solution.wristPosture && (
                      <Grid item xs={12} md={4}>
                        <Card sx={{ bgcolor: '#2c2c2c', border: '1px solid rgba(255, 255, 255, 0.12)' }}>
                          <CardContent sx={{ '&:last-child': { pb: 1 } }}>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: 'block', mb: 0.5 }}>
                              Wrist Posture
                            </Typography>
                            <Typography variant="h6" sx={{ mb: 0.5 }}>
                              {solution.wristPosture.angle.toFixed(1)}°
                            </Typography>
                            <Chip
                              label={solution.wristPosture.postureType}
                              color={solution.wristPosture.isErgonomic ? 'success' : 'warning'}
                              size="small"
                            />
                          </CardContent>
                        </Card>
                      </Grid>
                    )}

                    {solution.muting && (
                      <Grid item xs={12} md={4}>
                        <Card sx={{ bgcolor: '#2c2c2c', border: '1px solid rgba(255, 255, 255, 0.12)' }}>
                          <CardContent sx={{ '&:last-child': { pb: 1 } }}>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: 'block', mb: 0.5 }}>
                              Muting
                            </Typography>
                            <Typography variant="body1" sx={{ fontWeight: 500 }}>
                              {solution.muting.technique}
                            </Typography>
                          </CardContent>
                        </Card>
                      </Grid>
                    )}

                    {solution.slideLegato && (
                      <Grid item xs={12} md={4}>
                        <Card sx={{ bgcolor: '#2c2c2c', border: '1px solid rgba(255, 255, 255, 0.12)' }}>
                          <CardContent sx={{ '&:last-child': { pb: 1 } }}>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: 'block', mb: 0.5 }}>
                              Slide / Legato
                            </Typography>
                            <Typography variant="body1" sx={{ fontWeight: 500 }}>
                              {solution.slideLegato.technique}
                            </Typography>
                          </CardContent>
                        </Card>
                      </Grid>
                    )}
                  </Grid>
                ) : (
                  <Typography variant="body2" color="text.secondary">
                    Choose a chord or provide custom JSON, then click “Solve IK” to see biomechanical feedback.
                  </Typography>
                )}
              </CardContent>
            </Card>

            {solution?.visualization && solution.visualization.fretboardGeometry && (
              <Card
                sx={{
                  bgcolor: '#1e1e1e',
                  border: '1px solid rgba(255, 255, 255, 0.12)',
                }}
              >
                <CardContent sx={{ '&:last-child': { pb: 1.5 } }}>
                  <Typography variant="subtitle1" gutterBottom sx={{ mb: 1 }}>
                    3D Hand Model Visualization
                  </Typography>

                  <Divider sx={{ my: 1, borderColor: 'rgba(255, 255, 255, 0.12)' }} />

                  {/* 3D Canvas */}
                  <Box sx={{ mb: 2, display: 'flex', justifyContent: 'center' }}>
                    <HandVisualization3D
                      visualization={solution.visualization}
                      width={800}
                      height={500}
                    />
                  </Box>

                  <Divider sx={{ my: 1, borderColor: 'rgba(255, 255, 255, 0.12)' }} />

                  <Grid container spacing={1}>
                    {/* Fretboard Geometry */}
                    <Grid item xs={12} md={6}>
                      <Card sx={{ bgcolor: '#2c2c2c', border: '1px solid rgba(255, 255, 255, 0.12)' }}>
                        <CardContent sx={{ '&:last-child': { pb: 1 } }}>
                          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: 'block', mb: 0.5 }}>
                            Fretboard Geometry
                          </Typography>
                          <Stack spacing={0.5}>
                            <Typography variant="body2" color="text.secondary">
                              Thickness: {solution.visualization.fretboardGeometry?.thicknessMm?.toFixed(1) ?? 'N/A'} mm
                            </Typography>
                            <Typography variant="body2" color="text.secondary">
                              Neck @ Nut: {solution.visualization.fretboardGeometry?.neckThicknessAtNut?.toFixed(1) ?? 'N/A'} mm
                            </Typography>
                            <Typography variant="body2" color="text.secondary">
                              Neck @ 12th: {solution.visualization.fretboardGeometry?.neckThicknessAt12thFret?.toFixed(1) ?? 'N/A'} mm
                            </Typography>
                            <Typography variant="body2" color="text.secondary">
                              String Height: {solution.visualization.fretboardGeometry?.stringHeightAtNut?.toFixed(1) ?? 'N/A'} - {solution.visualization.fretboardGeometry?.stringHeightAt12th?.toFixed(1) ?? 'N/A'} mm
                            </Typography>
                          </Stack>
                        </CardContent>
                      </Card>
                    </Grid>

                    {/* Wrist & Palm Position */}
                    <Grid item xs={12} md={6}>
                      <Card sx={{ bgcolor: '#2c2c2c', border: '1px solid rgba(255, 255, 255, 0.12)' }}>
                        <CardContent sx={{ '&:last-child': { pb: 1 } }}>
                          <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: 'block', mb: 0.5 }}>
                            Wrist & Palm Position
                          </Typography>
                          <Stack spacing={0.5}>
                            <Typography variant="body2" color="text.secondary">
                              Wrist: ({solution.visualization.wristPosition?.x?.toFixed(1) ?? 'N/A'}, {solution.visualization.wristPosition?.y?.toFixed(1) ?? 'N/A'}, {solution.visualization.wristPosition?.z?.toFixed(1) ?? 'N/A'}) mm
                            </Typography>
                            <Typography variant="body2" color="text.secondary">
                              Palm Orientation: ({solution.visualization.palmOrientation?.x?.toFixed(2) ?? 'N/A'}, {solution.visualization.palmOrientation?.y?.toFixed(2) ?? 'N/A'}, {solution.visualization.palmOrientation?.z?.toFixed(2) ?? 'N/A'}, {solution.visualization.palmOrientation?.w?.toFixed(2) ?? 'N/A'})
                            </Typography>
                          </Stack>
                        </CardContent>
                      </Card>
                    </Grid>

                    {/* Finger Details */}
                    {solution.visualization.fingertips && Object.entries(solution.visualization.fingertips).map(([fingerName, fingerData]) => (
                      <Grid item xs={12} md={6} key={fingerName}>
                        <Card sx={{ bgcolor: '#2c2c2c', border: '1px solid rgba(255, 255, 255, 0.12)' }}>
                          <CardContent sx={{ '&:last-child': { pb: 1 } }}>
                            <Typography variant="caption" color="text.secondary" sx={{ fontWeight: 600, display: 'block', mb: 0.5 }}>
                              {fingerName}
                            </Typography>
                            <Stack spacing={0.5}>
                              <Typography variant="body2" color="text.secondary">
                                Position: ({fingerData.position?.x?.toFixed(1) ?? 'N/A'}, {fingerData.position?.y?.toFixed(1) ?? 'N/A'}, {fingerData.position?.z?.toFixed(1) ?? 'N/A'}) mm
                              </Typography>
                              <Typography variant="body2" color="text.secondary">
                                Joints: {fingerData.jointPositions?.length ?? 0} positions
                              </Typography>
                              <Typography variant="body2" color="text.secondary">
                                Arc Points: {fingerData.arcTrajectory?.length ?? 0} trajectory points
                              </Typography>
                              <Typography variant="body2" color="text.secondary">
                                Flexion: [{fingerData.jointFlexionAngles?.map(a => a.toFixed(0)).join('°, ') ?? 'N/A'}°]
                              </Typography>
                              <Typography variant="body2" color="text.secondary">
                                Abduction: [{fingerData.jointAbductionAngles?.map(a => a.toFixed(0)).join('°, ') ?? 'N/A'}°]
                              </Typography>
                            </Stack>
                          </CardContent>
                        </Card>
                      </Grid>
                    ))}
                  </Grid>

                  <Alert severity="info" sx={{ mt: 1.5, py: 0.5 }}>
                    <Typography variant="caption">
                      The hand is positioned underneath the fretboard with fingers approaching from below in natural arc trajectories.
                      All joint positions and angles are computed using realistic guitar playing constraints.
                    </Typography>
                  </Alert>
                </CardContent>
              </Card>
            )}
          </Stack>
        </Grid>
      </Grid>
    </Box>
  );
};

export default InverseKinematicsTest;
