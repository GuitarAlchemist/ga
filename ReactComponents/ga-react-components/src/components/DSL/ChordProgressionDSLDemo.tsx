import React, { useState } from 'react';
import {
  Box,
  Paper,
  Typography,
  TextField,
  Button,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Alert,
  Chip,
  Stack,
  Divider,
  Card,
  CardContent,
  Grid,
} from '@mui/material';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';
import CodeIcon from '@mui/icons-material/Code';
import InfoIcon from '@mui/icons-material/Info';
import Editor from '@monaco-editor/react';

interface ParseResult {
  success: boolean;
  ast?: any;
  error?: string;
}

const ChordProgressionDSLDemo: React.FC = () => {
  const [input, setInput] = useState('');
  const [parseResult, setParseResult] = useState<ParseResult | null>(null);
  const [selectedExample, setSelectedExample] = useState('');

  // Example chord progressions
  const examples = {
    // Basic Progressions
    'I-IV-V-I (C major)': 'C F G C',
    'ii-V-I (Jazz)': 'Dm7 G7 Cmaj7',
    'I-vi-IV-V (Pop)': 'C Am F G',
    '12-Bar Blues': 'C C C C F F C C G F C G',

    // Roman Numeral Progressions
    'I-IV-V-I (Roman)': 'I IV V I in C',
    'ii-V-I (Roman)': 'ii V I in C',
    'Circle of Fifths': 'I IV vii° iii vi ii V I in C',

    // With Metadata
    'With Time Signature': 'C F G C | time: 4/4',
    'With Tempo': 'Dm7 G7 Cmaj7 | tempo: 120',
    'With Key': 'I IV V I | key: G',
    'Full Metadata': 'C F G C | key: C, time: 4/4, tempo: 120',

    // Complex Progressions
    'Modal Interchange': 'C Fm G7 C',
    'Secondary Dominants': 'C E7 Am D7 G7 C',
    'Borrowed Chords': 'C Fm Ab G7 C',
    'Extended Chords': 'Cmaj9 Fmaj7#11 G13 Cmaj9',

    // Jazz Standards
    'Autumn Leaves (Am)': 'Am7 D7 Gmaj7 Cmaj7 F#m7b5 B7 Em7 Em7',
    'Giant Steps': 'Bmaj7 D7 Gmaj7 Bb7 Ebmaj7 Am7 D7 Gmaj7',
    'All The Things You Are': 'Fm7 Bbm7 Eb7 Abmaj7 Dbmaj7 Dm7 G7 Cmaj7',
  };

  const handleExampleSelect = (exampleKey: string) => {
    setSelectedExample(exampleKey);
    setInput(examples[exampleKey as keyof typeof examples] || '');
  };

  const handleParse = async () => {
    if (!input.trim()) {
      setParseResult({
        success: false,
        error: 'Please enter a chord progression',
      });
      return;
    }

    try {
      // Call the API to parse the chord progression
      const response = await fetch('https://localhost:7001/api/dsl/parse-chord-progression', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ input: input.trim() }),
      });

      const data = await response.json();
      setParseResult(data);
    } catch (error) {
      setParseResult({
        success: false,
        error: `Failed to parse: ${error instanceof Error ? error.message : 'Unknown error'}`,
      });
    }
  };

  return (
    <Box sx={{ p: 3 }}>
      <Paper elevation={3} sx={{ p: 3, mb: 3 }}>
        <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2 }}>
          <CodeIcon color="primary" fontSize="large" />
          <Typography variant="h4" component="h1">
            Chord Progression DSL Demo
          </Typography>
        </Stack>

        <Alert severity="info" icon={<InfoIcon />} sx={{ mb: 3 }}>
          <Typography variant="body2">
            Enter chord progressions using absolute chords (C, Dm7, Gmaj7) or Roman numerals (I, ii, V).
            Add metadata with | key: C, time: 4/4, tempo: 120
          </Typography>
        </Alert>

        <Grid container spacing={3}>
          {/* Input Section */}
          <Grid item xs={12} md={6}>
            <Card>
              <CardContent>
                <Typography variant="h6" gutterBottom>
                  Input
                </Typography>

                <FormControl fullWidth sx={{ mb: 2 }}>
                  <InputLabel>Examples</InputLabel>
                  <Select
                    value={selectedExample}
                    label="Examples"
                    onChange={(e) => handleExampleSelect(e.target.value)}
                  >
                    <MenuItem value="">
                      <em>Select an example...</em>
                    </MenuItem>
                    {Object.keys(examples).map((key) => (
                      <MenuItem key={key} value={key}>
                        {key}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                <Box sx={{ border: 1, borderColor: 'divider', borderRadius: 1, overflow: 'hidden', mb: 2 }}>
                  <Editor
                    height="150px"
                    defaultLanguage="plaintext"
                    value={input}
                    onChange={(value) => setInput(value || '')}
                    theme="vs-dark"
                    options={{
                      minimap: { enabled: false },
                      fontSize: 14,
                      lineNumbers: 'off',
                      glyphMargin: false,
                      folding: false,
                      lineDecorationsWidth: 0,
                      lineNumbersMinChars: 0,
                      renderLineHighlight: 'none',
                      scrollBeyondLastLine: false,
                      wordWrap: 'on',
                      wrappingIndent: 'indent',
                      automaticLayout: true,
                      tabSize: 2,
                      insertSpaces: true,
                      fontFamily: 'Consolas, "Courier New", monospace',
                    }}
                  />
                </Box>

                <Button
                  fullWidth
                  variant="contained"
                  color="primary"
                  startIcon={<PlayArrowIcon />}
                  onClick={handleParse}
                  size="large"
                >
                  Parse Progression
                </Button>
              </CardContent>
            </Card>
          </Grid>

          {/* Output Section */}
          <Grid item xs={12} md={6}>
            <Card>
              <CardContent>
                <Typography variant="h6" gutterBottom>
                  Parse Result
                </Typography>

                {parseResult ? (
                  <>
                    {parseResult.success ? (
                      <>
                        <Alert severity="success" sx={{ mb: 2 }}>
                          Successfully parsed chord progression!
                        </Alert>

                        <Divider sx={{ my: 2 }} />

                        <Typography variant="subtitle2" gutterBottom>
                          Abstract Syntax Tree (AST):
                        </Typography>
                        <Paper
                          elevation={0}
                          sx={{
                            p: 2,
                            bgcolor: 'grey.100',
                            maxHeight: 400,
                            overflow: 'auto',
                          }}
                        >
                          <pre style={{ margin: 0, fontSize: '0.875rem' }}>
                            {JSON.stringify(parseResult.ast, null, 2)}
                          </pre>
                        </Paper>

                        {parseResult.ast && (
                          <Box sx={{ mt: 2 }}>
                            <Typography variant="subtitle2" gutterBottom>
                              Progression Details:
                            </Typography>
                            <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
                              {parseResult.ast.Chords?.map((chord: any, idx: number) => (
                                <Chip
                                  key={idx}
                                  label={chord.Chord || chord.Type}
                                  color="primary"
                                  variant="outlined"
                                  size="small"
                                />
                              ))}
                            </Stack>
                            {parseResult.ast.Key && (
                              <Typography variant="body2" sx={{ mt: 1 }}>
                                <strong>Key:</strong> {parseResult.ast.Key}
                              </Typography>
                            )}
                            {parseResult.ast.TimeSignature && (
                              <Typography variant="body2">
                                <strong>Time:</strong> {parseResult.ast.TimeSignature}
                              </Typography>
                            )}
                            {parseResult.ast.Tempo && (
                              <Typography variant="body2">
                                <strong>Tempo:</strong> {parseResult.ast.Tempo} BPM
                              </Typography>
                            )}
                          </Box>
                        )}
                      </>
                    ) : (
                      <Alert severity="error">
                        <Typography variant="body2">
                          <strong>Error:</strong> {parseResult.error}
                        </Typography>
                      </Alert>
                    )}
                  </>
                ) : (
                  <Alert severity="info">
                    Enter a chord progression and click "Parse Progression" to see the result.
                  </Alert>
                )}
              </CardContent>
            </Card>
          </Grid>
        </Grid>

        <Divider sx={{ my: 3 }} />

        <Typography variant="h6" gutterBottom>
          Syntax Guide
        </Typography>
        <Grid container spacing={2}>
          <Grid item xs={12} md={6}>
            <Card variant="outlined">
              <CardContent>
                <Typography variant="subtitle2" gutterBottom>
                  Absolute Chords
                </Typography>
                <Typography variant="body2" component="div">
                  • Basic: C, D, E, F, G, A, B<br />
                  • Minor: Cm, Dm, Em<br />
                  • Seventh: C7, Cmaj7, Cm7, Cdim7<br />
                  • Extended: Cmaj9, C13, Cm11<br />
                  • Altered: C7#9, C7b5, Cmaj7#11
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} md={6}>
            <Card variant="outlined">
              <CardContent>
                <Typography variant="subtitle2" gutterBottom>
                  Roman Numerals
                </Typography>
                <Typography variant="body2" component="div">
                  • Major: I, IV, V<br />
                  • Minor: ii, iii, vi<br />
                  • Diminished: vii°<br />
                  • With key: I IV V I in C<br />
                  • Metadata: | key: C, time: 4/4, tempo: 120
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      </Paper>
    </Box>
  );
};

export default ChordProgressionDSLDemo;

