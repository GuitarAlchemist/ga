import React, { useState, useEffect } from 'react';
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

const GrothendieckDSLDemo: React.FC = () => {
  const [input, setInput] = useState('');
  const [parseResult, setParseResult] = useState<ParseResult | null>(null);
  const [selectedExample, setSelectedExample] = useState('');

  // Example Grothendieck operations - organized by category
  // Note: Some operations use simplified syntax that works with current parser
  const examples = {
    // Category Operations (Binary)
    'Tensor Product (⊗)': 'C ⊗ G',
    'Direct Sum (⊕)': 'Cmaj7 ⊕ Gmaj7',
    'Product (×)': 'Cmaj7 × Gmaj7',
    'Coproduct (+)': 'Cmaj7 + Gmaj7',
    'Exponential (^)': 'Cmaj7 ^ Gmaj7',

    // Category Operations (Function Form - for multiple objects)
    'Product (function)': 'product(Cmaj7, Gmaj7, Fmaj7)',
    'Coproduct (function)': 'coproduct(Cmaj7, Gmaj7, Fmaj7)',

    // Functor Operations
    'Define Functor': 'functor Transpose: Chords -> Chords',
    'Apply Functor': 'Transpose(Cmaj7)',
    'Compose Functors (∘)': 'Transpose ∘ Invert',

    // Natural Transformations
    'Apply Natural Transformation': 'η(Cmaj7)',

    // Limit Operations
    'Pullback': 'pullback(Cmaj7, Transpose, Gmaj7)',
    'Equalizer': 'eq(Transpose, Invert)',

    // Colimit Operations
    'Pushout': 'pushout(Cmaj7, Transpose, Gmaj7)',
    'Coequalizer': 'coeq(Transpose, Invert)',

    // Topos Operations
    'Power Object': 'power(Cmaj7)',
    'Subobject Classifier': 'Ω',
    'Truth Value': 'truth_value(Cmaj7)',

    // Sheaf Operations
    'Hom Functor': 'Hom(Cmaj7, Gmaj7)',
    'Restriction': 'Cmaj7 | Transpose',

    // Complex Combinations
    'Tensor + Functor': 'Transpose(C ⊗ G)',
    'Product + Pullback': 'pullback(Cmaj7 × Gmaj7, Transpose, Fmaj7)',
    'Functor Composition + Application': '(Transpose ∘ Invert)(Cmaj7)',
    'Direct Sum + Power': 'power(Cmaj7 ⊕ Gmaj7)',
    'Exponential + Hom': 'Hom(Cmaj7 ^ Gmaj7, Fmaj7)',
    'Coproduct + Pushout': 'pushout(Cmaj7 + Gmaj7, Transpose, Fmaj7)',
    'Sheaf + Restriction': 'Hom(Cmaj7, Gmaj7) | Transpose',
  };

  const handleExampleSelect = (exampleKey: string) => {
    setSelectedExample(exampleKey);
    setInput(examples[exampleKey as keyof typeof examples] || '');
  };

  const handleParse = async () => {
    if (!input.trim()) {
      setParseResult({
        success: false,
        error: 'Please enter a Grothendieck operation',
      });
      return;
    }

    try {
      // Call the API to parse the Grothendieck operation
      const response = await fetch('https://localhost:7001/api/dsl/parse-grothendieck', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ input }),
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const data = await response.json();

      if (data.success) {
        setParseResult({
          success: true,
          ast: data.ast,
        });
      } else {
        setParseResult({
          success: false,
          error: data.error || 'Parse failed',
        });
      }
    } catch (error) {
      setParseResult({
        success: false,
        error: `Error: ${error instanceof Error ? error.message : 'Unknown error'}`,
      });
    }
  };

  const renderAST = (ast: any, depth = 0): React.ReactNode => {
    if (!ast) return null;

    const indent = depth * 20;

    if (typeof ast === 'string' || typeof ast === 'number' || typeof ast === 'boolean') {
      return (
        <Box sx={{ ml: `${indent}px`, my: 0.5 }}>
          <Chip label={String(ast)} size="small" color="primary" variant="outlined" />
        </Box>
      );
    }

    if (Array.isArray(ast)) {
      return (
        <Box sx={{ ml: `${indent}px` }}>
          <Typography variant="caption" color="text.secondary">
            Array [{ast.length} items]
          </Typography>
          {ast.map((item, index) => (
            <Box key={index}>{renderAST(item, depth + 1)}</Box>
          ))}
        </Box>
      );
    }

    if (typeof ast === 'object') {
      return (
        <Box sx={{ ml: `${indent}px`, my: 1 }}>
          {Object.entries(ast).map(([key, value]) => (
            <Box key={key} sx={{ my: 0.5 }}>
              <Typography variant="body2" component="span" sx={{ fontWeight: 'bold', color: 'primary.main' }}>
                {key}:
              </Typography>
              {renderAST(value, depth + 1)}
            </Box>
          ))}
        </Box>
      );
    }

    return null;
  };

  return (
    <Box sx={{ width: '100%', height: '100%' }}>
      <Grid container spacing={3}>
        {/* Left Panel - Input & Examples */}
        <Grid item xs={12} md={6}>
          <Stack spacing={3}>
            {/* Header */}
            <Paper elevation={3} sx={{ p: 3 }}>
              <Stack direction="row" spacing={2} alignItems="center">
                <CodeIcon fontSize="large" color="primary" />
                <Box>
                  <Typography variant="h5" gutterBottom>
                    Grothendieck Operations DSL
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Category theory operations for musical objects
                  </Typography>
                </Box>
              </Stack>
            </Paper>

            {/* Examples Selector */}
            <Paper elevation={2} sx={{ p: 3 }}>
              <FormControl fullWidth>
                <InputLabel>Select Example</InputLabel>
                <Select
                  value={selectedExample}
                  label="Select Example"
                  onChange={(e) => handleExampleSelect(e.target.value)}
                >
                  <MenuItem value="">
                    <em>Choose an example...</em>
                  </MenuItem>
                  {Object.keys(examples).map((key) => (
                    <MenuItem key={key} value={key}>
                      {key}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Paper>

            {/* Input Field with Monaco Editor */}
            <Paper elevation={2} sx={{ p: 3 }}>
              <Typography variant="subtitle2" gutterBottom>
                Grothendieck Operation
              </Typography>
              <Box sx={{ border: 1, borderColor: 'divider', borderRadius: 1, overflow: 'hidden', mb: 2 }}>
                <Editor
                  height="200px"
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
                size="large"
                startIcon={<PlayArrowIcon />}
                onClick={handleParse}
              >
                Parse Operation
              </Button>
            </Paper>

            {/* Operation Categories */}
            <Paper elevation={2} sx={{ p: 3 }}>
              <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 2 }}>
                <InfoIcon color="info" />
                <Typography variant="h6">Supported Operations</Typography>
              </Stack>
              <Divider sx={{ mb: 2 }} />
              <Stack spacing={1.5}>
                <Box>
                  <Typography variant="subtitle2" color="primary">Category Operations</Typography>
                  <Typography variant="caption" color="text.secondary">
                    ⊗ (tensor), ⊕ (direct sum), × (product), + (coproduct), ^ (exponential)
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="subtitle2" color="primary">Functors</Typography>
                  <Typography variant="caption" color="text.secondary">
                    F(x), F ∘ G (composition)
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="subtitle2" color="primary">Natural Transformations</Typography>
                  <Typography variant="caption" color="text.secondary">
                    η: F =&gt; G
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="subtitle2" color="primary">Limits & Colimits</Typography>
                  <Typography variant="caption" color="text.secondary">
                    lim(...), colim(...), pullback(...), pushout(...), equalizer(...), coequalizer(...)
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="subtitle2" color="primary">Topos Operations</Typography>
                  <Typography variant="caption" color="text.secondary">
                    Ω(x), P(x), Hom(x, y), x =&gt; y
                  </Typography>
                </Box>
                <Box>
                  <Typography variant="subtitle2" color="primary">Sheaves</Typography>
                  <Typography variant="caption" color="text.secondary">
                    sheaf(x, U), x | U (restriction), x ∩ y (gluing)
                  </Typography>
                </Box>
              </Stack>
            </Paper>
          </Stack>
        </Grid>

        {/* Right Panel - Results */}
        <Grid item xs={12} md={6}>
          <Stack spacing={3}>
            {/* Parse Result */}
            {parseResult && (
              <Paper elevation={2} sx={{ p: 3 }}>
                {parseResult.success ? (
                  <>
                    <Alert severity="success" sx={{ mb: 2 }}>
                      ✅ Parse successful!
                    </Alert>
                    <Typography variant="h6" gutterBottom>
                      Abstract Syntax Tree (AST)
                    </Typography>
                    <Divider sx={{ my: 2 }} />
                    <Box
                      sx={{
                        maxHeight: '600px',
                        overflow: 'auto',
                        bgcolor: 'grey.50',
                        p: 2,
                        borderRadius: 1,
                        fontFamily: 'monospace',
                      }}
                    >
                      {renderAST(parseResult.ast)}
                    </Box>
                  </>
                ) : (
                  <Alert severity="error">
                    ❌ Parse failed: {parseResult.error}
                  </Alert>
                )}
              </Paper>
            )}

            {/* Info Card */}
            {!parseResult && (
              <Card elevation={2}>
                <CardContent>
                  <Typography variant="h6" gutterBottom>
                    Welcome to Grothendieck DSL
                  </Typography>
                  <Typography variant="body2" color="text.secondary" paragraph>
                    This demo showcases the Grothendieck Operations DSL parser, which applies category theory concepts to musical objects.
                  </Typography>
                  <Typography variant="body2" color="text.secondary" paragraph>
                    <strong>How to use:</strong>
                  </Typography>
                  <Typography variant="body2" color="text.secondary" component="div">
                    <ol>
                      <li>Select an example from the dropdown, or</li>
                      <li>Type your own Grothendieck operation</li>
                      <li>Click "Parse Operation" to see the AST</li>
                    </ol>
                  </Typography>
                  <Typography variant="body2" color="text.secondary" paragraph>
                    <strong>Musical Objects:</strong> Notes (C, D, E, etc.), Chords (Cmaj7, Gmin, etc.), Scales, Progressions, Voicings, Set Classes
                  </Typography>
                </CardContent>
              </Card>
            )}
          </Stack>
        </Grid>
      </Grid>
    </Box>
  );
};

export default GrothendieckDSLDemo;

