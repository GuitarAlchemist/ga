import React, { useState } from 'react';
import { Provider as JotaiProvider } from 'jotai';
import {
  ThemeProvider,
  createTheme,
  CssBaseline,
  Box,
  AppBar,
  Toolbar,
  Typography,
  ToggleButtonGroup,
  ToggleButton,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  SelectChangeEvent,
  Paper,
} from '@mui/material';
import NavigationDrawer from './components/NavigationDrawer';
import GuitarFretboard from './components/GuitarFretboard';
import { DisplayMode } from './types/fretboard.types';
import { mockDataByMode } from './data/mockData';

// Create a dark theme
const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: {
      main: '#2196F3',
    },
    secondary: {
      main: '#FF9800',
    },
    background: {
      default: '#121212',
      paper: '#1e1e1e',
    },
  },
});

const App: React.FC = () => {
  const [displayMode, setDisplayMode] = useState<DisplayMode>('chord');
  const [selectedItem, setSelectedItem] = useState<string>('C Major');

  // Get available items for the current display mode
  const availableItems = Object.keys(mockDataByMode[displayMode]);

  // Update selected item when display mode changes
  const handleDisplayModeChange = (
    _event: React.MouseEvent<HTMLElement>,
    newMode: DisplayMode | null,
  ) => {
    if (newMode !== null) {
      setDisplayMode(newMode);
      // Set the first available item for the new mode
      const items = Object.keys(mockDataByMode[newMode]);
      if (items.length > 0) {
        setSelectedItem(items[0]);
      }
    }
  };

  const handleItemChange = (event: SelectChangeEvent) => {
    setSelectedItem(event.target.value);
  };

  // Get positions for the current selection
  const currentPositions = (mockDataByMode[displayMode] as Record<string, any>)[selectedItem] || [];

  const handlePositionClick = (position: any) => {
    console.log('Position clicked:', position);
    // In a real app, this could trigger additional actions
  };

  return (
    <JotaiProvider>
      <ThemeProvider theme={darkTheme}>
        <CssBaseline />
        <Box sx={{ display: 'flex', height: '100vh' }}>
          {/* Navigation Drawer */}
          <NavigationDrawer />

          {/* Main Content */}
          <Box
            component="main"
            sx={{
              flexGrow: 1,
              display: 'flex',
              flexDirection: 'column',
              overflow: 'auto',
            }}
          >
            {/* App Bar */}
            <AppBar
              position="static"
              sx={{
                backgroundColor: '#1e1e1e',
                boxShadow: 1,
              }}
            >
              <Toolbar>
                <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
                  Guitar Fretboard Visualizer
                </Typography>
              </Toolbar>
            </AppBar>

            {/* Content Area */}
            <Box sx={{ p: 3, flexGrow: 1 }}>
              {/* Controls */}
              <Paper sx={{ p: 2, mb: 3 }}>
                <Box sx={{ display: 'flex', gap: 3, alignItems: 'center', flexWrap: 'wrap' }}>
                  {/* Display Mode Toggle */}
                  <FormControl>
                    <Typography variant="caption" sx={{ mb: 1 }}>
                      Display Mode
                    </Typography>
                    <ToggleButtonGroup
                      value={displayMode}
                      exclusive
                      onChange={handleDisplayModeChange}
                      aria-label="display mode"
                      size="small"
                    >
                      <ToggleButton value="chord" aria-label="chord">
                        Chord
                      </ToggleButton>
                      <ToggleButton value="scale" aria-label="scale">
                        Scale
                      </ToggleButton>
                      <ToggleButton value="mode" aria-label="mode">
                        Mode
                      </ToggleButton>
                      <ToggleButton value="arpeggio" aria-label="arpeggio">
                        Arpeggio
                      </ToggleButton>
                    </ToggleButtonGroup>
                  </FormControl>

                  {/* Item Selection */}
                  <FormControl sx={{ minWidth: 200 }}>
                    <InputLabel id="item-select-label">
                      Select {displayMode.charAt(0).toUpperCase() + displayMode.slice(1)}
                    </InputLabel>
                    <Select
                      labelId="item-select-label"
                      id="item-select"
                      value={selectedItem}
                      label={`Select ${displayMode.charAt(0).toUpperCase() + displayMode.slice(1)}`}
                      onChange={handleItemChange}
                      size="small"
                    >
                      {availableItems.map((item) => (
                        <MenuItem key={item} value={item}>
                          {item}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Box>
              </Paper>

              {/* Fretboard Display */}
              <Paper sx={{ p: 2 }}>
                <GuitarFretboard
                  positions={currentPositions}
                  displayMode={displayMode}
                  title={selectedItem}
                  onPositionClick={handlePositionClick}
                  config={{
                    showFretNumbers: true,
                    showStringLabels: true,
                  }}
                />
              </Paper>

              {/* Information Panel */}
              <Paper sx={{ p: 2, mt: 3 }}>
                <Typography variant="h6" gutterBottom>
                  About This Application
                </Typography>
                <Typography variant="body2" paragraph>
                  This is a guitar fretboard visualization application built with React, TypeScript,
                  Material-UI, and Jotai for state management.
                </Typography>
                <Typography variant="body2" paragraph>
                  <strong>Features:</strong>
                </Typography>
                <ul>
                  <li>
                    <Typography variant="body2">
                      Collapsible navy-themed navigation drawer
                    </Typography>
                  </li>
                  <li>
                    <Typography variant="body2">
                      Interactive guitar fretboard with SVG rendering
                    </Typography>
                  </li>
                  <li>
                    <Typography variant="body2">
                      Support for displaying chords, scales, modes, and arpeggios
                    </Typography>
                  </li>
                  <li>
                    <Typography variant="body2">
                      Props-based architecture - all music theory calculations should be done by the backend
                    </Typography>
                  </li>
                  <li>
                    <Typography variant="body2">
                      Mock data for demonstration purposes
                    </Typography>
                  </li>
                </ul>
                <Typography variant="body2" sx={{ mt: 2 }}>
                  <strong>Note:</strong> The fretboard component is designed to be backend-agnostic.
                  It accepts position data as props and handles only the visual rendering.
                  All music theory logic (chord voicings, scale patterns, etc.) should be
                  calculated by your backend API.
                </Typography>
              </Paper>
            </Box>
          </Box>
        </Box>
      </ThemeProvider>
    </JotaiProvider>
  );
};

export default App;

