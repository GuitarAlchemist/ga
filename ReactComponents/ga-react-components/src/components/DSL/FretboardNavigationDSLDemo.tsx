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

const FretboardNavigationDSLDemo: React.FC = () => {
  const [input, setInput] = useState('');
  const [parseResult, setParseResult] = useState<ParseResult | null>(null);
  const [selectedExample, setSelectedExample] = useState('');

  // Example fretboard navigation commands
  const examples = {
    // Position Commands
    'String:Fret Notation': '6:5',
    'Position on String': 'position 5 on string 3',
    'String and Fret': 'string 3 fret 5',

    // CAGED Shapes
    'C Shape at Fret 3': 'CAGED shape C at fret 3',
    'E Shape at Fret 7': 'CAGED shape E at fret 7',
    'A Shape at Fret 5': 'CAGED shape A at fret 5',
    'G Shape at Fret 10': 'CAGED shape G at fret 10',
    'D Shape at Fret 12': 'CAGED shape D at fret 12',

    // Movement Commands
    'Move Up 2 Frets': 'move up 2',
    'Move Down 3 Frets': 'move down 3',
    'Move Left': 'move left 1',
    'Move Right': 'move right 1',

    // Slide Commands
    'Slide from 6:5 to 6:7': 'slide from 6:5 to 6:7',
    'Slide from 5:3 to 5:5': 'slide from 5:3 to 5:5',
    'Slide Across Strings': 'slide from 6:5 to 5:7',

    // Complex Patterns
    'Pentatonic Box 1': 'CAGED shape E at fret 12',
    'Blues Scale Position': 'CAGED shape A at fret 5',
    'Major Scale CAGED': 'CAGED shape C at fret 8',
  };

  const handleExampleSelect = (exampleKey: string) => {
    setSelectedExample(exampleKey);
    setInput(examples[exampleKey as keyof typeof examples] || '');
  };

  const handleParse = async () => {
    if (!input.trim()) {
      setParseResult({
        success: false,
        error: 'Please enter a fretboard navigation command',
      });
      return;
    }

    try {
      // Call the API to parse the fretboard navigation command
      const response = await fetch('https://localhost:7001/api/dsl/parse-fretboard-navigation', {
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
            Fretboard Navigation DSL Demo
          </Typography>
        </Stack>

        <Alert severity="info" icon={<InfoIcon />} sx={{ mb: 3 }}>
          <Typography variant="body2">
            Navigate the guitar fretboard using positions (6:5), CAGED shapes, movements (move up 2),
            and slides (slide from 6:5 to 6:7).
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
                  Parse Command
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
                          Successfully parsed navigation command!
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
                              Command Details:
                            </Typography>
                            <Chip
                              label={parseResult.ast.Type || 'Unknown'}
                              color="primary"
                              variant="outlined"
                              sx={{ mb: 1 }}
                            />
                            {parseResult.ast.String && (
                              <Typography variant="body2">
                                <strong>String:</strong> {parseResult.ast.String}
                              </Typography>
                            )}
                            {parseResult.ast.Fret !== undefined && (
                              <Typography variant="body2">
                                <strong>Fret:</strong> {parseResult.ast.Fret}
                              </Typography>
                            )}
                            {parseResult.ast.Shape && (
                              <Typography variant="body2">
                                <strong>Shape:</strong> {parseResult.ast.Shape}
                              </Typography>
                            )}
                            {parseResult.ast.Direction && (
                              <Typography variant="body2">
                                <strong>Direction:</strong> {parseResult.ast.Direction}
                              </Typography>
                            )}
                            {parseResult.ast.Distance && (
                              <Typography variant="body2">
                                <strong>Distance:</strong> {parseResult.ast.Distance}
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
                    Enter a navigation command and click "Parse Command" to see the result.
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
          <Grid item xs={12} md={4}>
            <Card variant="outlined">
              <CardContent>
                <Typography variant="subtitle2" gutterBottom>
                  Position Notation
                </Typography>
                <Typography variant="body2" component="div">
                  • String:Fret: 6:5, 1:12<br />
                  • Position: position 5 on string 3<br />
                  • String/Fret: string 3 fret 5
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} md={4}>
            <Card variant="outlined">
              <CardContent>
                <Typography variant="subtitle2" gutterBottom>
                  CAGED Shapes
                </Typography>
                <Typography variant="body2" component="div">
                  • C Shape: CAGED shape C at fret 3<br />
                  • A Shape: CAGED shape A at fret 5<br />
                  • G Shape: CAGED shape G at fret 10<br />
                  • E Shape: CAGED shape E at fret 7<br />
                  • D Shape: CAGED shape D at fret 12
                </Typography>
              </CardContent>
            </Card>
          </Grid>
          <Grid item xs={12} md={4}>
            <Card variant="outlined">
              <CardContent>
                <Typography variant="subtitle2" gutterBottom>
                  Movement & Slides
                </Typography>
                <Typography variant="body2" component="div">
                  • Move: move up 2, move down 3<br />
                  • Slide: slide from 6:5 to 6:7<br />
                  • Directions: up, down, left, right
                </Typography>
              </CardContent>
            </Card>
          </Grid>
        </Grid>
      </Paper>
    </Box>
  );
};

export default FretboardNavigationDSLDemo;

