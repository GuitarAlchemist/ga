import React, { useEffect, useState } from 'react';
import { Box, Typography, Grid, Card, CardContent, CircularProgress, Alert, TextField, MenuItem, Select, FormControl, InputLabel } from '@mui/material';
import { InstrumentIcon } from '../components/InstrumentIcon';
import { Instrument } from '../types/instrument';

/**
 * Test page for Instrument Icons
 */
const InstrumentIconsTest: React.FC = () => {
  const [instruments, setInstruments] = useState<Instrument[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState('');
  const [iconSize, setIconSize] = useState(32);
  const [iconColor, setIconColor] = useState('#1976d2');

  useEffect(() => {
    fetchInstruments();
  }, []);

  const fetchInstruments = async () => {
    try {
      setLoading(true);
      const response = await fetch('https://localhost:7001/Instruments');
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      
      const data = await response.json();
      setInstruments(data);
      setError(null);
    } catch (err) {
      console.error('Failed to fetch instruments:', err);
      setError(err instanceof Error ? err.message : 'Failed to fetch instruments');
    } finally {
      setLoading(false);
    }
  };

  const filteredInstruments = instruments.filter(inst =>
    inst.name.toLowerCase().includes(filter.toLowerCase())
  );

  if (loading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '400px' }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="error">
          <Typography variant="h6">Error Loading Instruments</Typography>
          <Typography>{error}</Typography>
          <Typography variant="caption" sx={{ mt: 1, display: 'block' }}>
            Make sure the API server is running at https://localhost:7001
          </Typography>
        </Alert>
      </Box>
    );
  }

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h4" gutterBottom>
        🎸 Instrument Icons Gallery
      </Typography>
      
      <Typography variant="body1" color="text.secondary" paragraph>
        Displaying {filteredInstruments.length} of {instruments.length} instruments with SVG icons
      </Typography>

      {/* Controls */}
      <Box sx={{ mb: 3, display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'center' }}>
        <TextField
          label="Filter Instruments"
          variant="outlined"
          size="small"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          sx={{ minWidth: 250 }}
        />
        
        <FormControl size="small" sx={{ minWidth: 120 }}>
          <InputLabel>Icon Size</InputLabel>
          <Select
            value={iconSize}
            label="Icon Size"
            onChange={(e) => setIconSize(Number(e.target.value))}
          >
            <MenuItem value={16}>16px</MenuItem>
            <MenuItem value={24}>24px</MenuItem>
            <MenuItem value={32}>32px</MenuItem>
            <MenuItem value={48}>48px</MenuItem>
            <MenuItem value={64}>64px</MenuItem>
          </Select>
        </FormControl>

        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography variant="body2">Color:</Typography>
          <input
            type="color"
            value={iconColor}
            onChange={(e) => setIconColor(e.target.value)}
            style={{ width: 40, height: 40, border: 'none', cursor: 'pointer' }}
          />
        </Box>
      </Box>

      {/* Instrument Grid */}
      <Grid container spacing={2}>
        {filteredInstruments.map((instrument) => (
          <Grid item xs={12} sm={6} md={4} lg={3} key={instrument.name}>
            <Card 
              sx={{ 
                height: '100%',
                transition: 'transform 0.2s, box-shadow 0.2s',
                '&:hover': {
                  transform: 'translateY(-4px)',
                  boxShadow: 4,
                }
              }}
            >
              <CardContent>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
                  <InstrumentIcon 
                    icon={instrument.icon} 
                    size={iconSize}
                    color={iconColor}
                  />
                  <Typography variant="h6" component="div">
                    {instrument.name}
                  </Typography>
                </Box>
                
                <Typography variant="body2" color="text.secondary">
                  {instrument.tunings.length} tuning{instrument.tunings.length !== 1 ? 's' : ''}
                </Typography>
                
                {instrument.tunings.length > 0 && (
                  <Box sx={{ mt: 1 }}>
                    <Typography variant="caption" color="text.secondary">
                      Standard: {instrument.tunings.find(t => t.name.toLowerCase().includes('standard'))?.tuning || instrument.tunings[0].tuning}
                    </Typography>
                  </Box>
                )}
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      {filteredInstruments.length === 0 && (
        <Box sx={{ textAlign: 'center', py: 8 }}>
          <Typography variant="h6" color="text.secondary">
            No instruments found matching "{filter}"
          </Typography>
        </Box>
      )}

      {/* Icon Examples */}
      <Box sx={{ mt: 6, p: 3, bgcolor: 'background.paper', borderRadius: 2 }}>
        <Typography variant="h5" gutterBottom>
          Icon Size Examples
        </Typography>
        
        <Box sx={{ display: 'flex', gap: 4, alignItems: 'flex-end', flexWrap: 'wrap' }}>
          {[16, 24, 32, 48, 64].map(size => (
            <Box key={size} sx={{ textAlign: 'center' }}>
              <InstrumentIcon 
                icon={instruments[0]?.icon} 
                size={size}
                color={iconColor}
              />
              <Typography variant="caption" display="block" sx={{ mt: 1 }}>
                {size}px
              </Typography>
            </Box>
          ))}
        </Box>
      </Box>

      {/* Color Examples */}
      <Box sx={{ mt: 3, p: 3, bgcolor: 'background.paper', borderRadius: 2 }}>
        <Typography variant="h5" gutterBottom>
          Icon Color Examples
        </Typography>
        
        <Box sx={{ display: 'flex', gap: 4, alignItems: 'center', flexWrap: 'wrap' }}>
          {['#1976d2', '#2e7d32', '#ed6c02', '#d32f2f', '#9c27b0', '#000000'].map(color => (
            <Box key={color} sx={{ textAlign: 'center' }}>
              <InstrumentIcon 
                icon={instruments[0]?.icon} 
                size={48}
                color={color}
              />
              <Typography variant="caption" display="block" sx={{ mt: 1 }}>
                {color}
              </Typography>
            </Box>
          ))}
        </Box>
      </Box>

      {/* Usage Example */}
      <Box sx={{ mt: 3, p: 3, bgcolor: 'grey.100', borderRadius: 2 }}>
        <Typography variant="h6" gutterBottom>
          Usage Example
        </Typography>
        <pre style={{ overflow: 'auto', padding: '16px', background: '#fff', borderRadius: '4px' }}>
{`import { InstrumentIcon } from './components/InstrumentIcon';

// Basic usage
<InstrumentIcon icon={instrument.icon} />

// With custom size and color
<InstrumentIcon 
  icon={instrument.icon} 
  size={48}
  color="#1976d2"
/>

// In a dropdown
<Select>
  {instruments.map(inst => (
    <MenuItem key={inst.name} value={inst.name}>
      <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
        <InstrumentIcon icon={inst.icon} size={20} />
        {inst.name}
      </Box>
    </MenuItem>
  ))}
</Select>`}
        </pre>
      </Box>
    </Box>
  );
};

export default InstrumentIconsTest;

