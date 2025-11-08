import React, { useState, useEffect } from 'react';
import { Box, Typography, Stack, Chip, CircularProgress, Divider, Accordion, AccordionSummary, AccordionDetails } from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { BSPApiService, VoicingWithAnalysis, ChordInContext } from './BSPApiService';
import { RealisticFretboard } from '../RealisticFretboard';
import type { FretboardPosition } from '../RealisticFretboard';

export interface ElementInfo {
  type: 'element' | 'region' | 'partition' | 'sample' | 'group' | 'portal' | 'door';
  name: string;
  tonalityType?: string;
  strategy?: string;
  depth?: number;
  distance?: number;
  object?: any;
}

interface ElementInfoPanelProps {
  element: ElementInfo | null;
  isSelected: boolean;
}

export const ElementInfoPanel: React.FC<ElementInfoPanelProps> = ({ element, isSelected }) => {
  const [voicings, setVoicings] = useState<VoicingWithAnalysis[]>([]);
  const [relatedChords, setRelatedChords] = useState<ChordInContext[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedVoicing, setSelectedVoicing] = useState<VoicingWithAnalysis | null>(null);

  useEffect(() => {
    if (!element) {
      setVoicings([]);
      setRelatedChords([]);
      setSelectedVoicing(null);
      return;
    }

    // Fetch detailed information based on element type
    const fetchDetails = async () => {
      setLoading(true);
      try {
        if (element.type === 'element') {
          // Fetch voicings for chord
          const voicingsData = await BSPApiService.getVoicingsForChord(element.name, {
            maxDifficulty: 'Intermediate',
            limit: 5,
          });
          setVoicings(voicingsData);
          if (voicingsData.length > 0) {
            setSelectedVoicing(voicingsData[0]);
          }
        } else if (element.type === 'region') {
          // Fetch chords for key/scale
          const chordsData = await BSPApiService.getChordsForKey(element.name, {
            extension: 'Seventh',
            onlyNaturallyOccurring: true,
            limit: 7,
          });
          setRelatedChords(chordsData);
        } else if (element.type === 'partition') {
          // Could fetch related information based on partition strategy
          // For now, just clear the data
          setVoicings([]);
          setRelatedChords([]);
        }
      } catch (error) {
        console.error('Failed to fetch element details:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchDetails();
  }, [element]);

  if (!element) return null;

  // Convert voicing to fretboard positions
  const voicingToPositions = (voicing: VoicingWithAnalysis): FretboardPosition[] => {
    // Safety check: ensure voicing has positions
    if (!voicing || !voicing.positions || !Array.isArray(voicing.positions)) {
      return [];
    }

    return voicing.positions
      .filter(pos => pos && typeof pos.string === 'number' && typeof pos.fret === 'number')
      .map((pos) => ({
        string: pos.string,
        fret: pos.fret,
        label: pos.note || '',
        color: '#2196F3',
        emphasized: pos.finger === 1, // Emphasize root notes (finger 1)
      }));
  };

  return (
    <Box
      sx={{
        position: 'absolute',
        top: '50%',
        right: 16,
        transform: 'translateY(-50%)',
        width: 400,
        maxHeight: '80%',
        overflow: 'auto',
        backgroundColor: 'rgba(10, 10, 20, 0.95)',
        border: isSelected ? '3px solid #00ff00' : '2px solid #4466ff',
        p: 2,
        boxShadow: isSelected ? '0 0 20px #00ff00' : '0 0 15px #4466ff',
        borderRadius: 1,
      }}
    >
      <Stack spacing={2}>
        {/* Header */}
        <Box>
          <Typography
            variant="h6"
            sx={{
              color: isSelected ? '#00ff00' : '#4466ff',
              fontFamily: 'monospace',
              fontWeight: 'bold',
              textTransform: 'uppercase',
              borderBottom: isSelected ? '2px solid #00ff00' : '2px solid #4466ff',
              pb: 1,
              mb: 1,
            }}
          >
            {isSelected ? '🎯 SELECTED' : '👁️ VIEWING'}
          </Typography>
          <Typography
            variant="h5"
            sx={{
              color: '#ffffff',
              fontFamily: 'monospace',
              fontWeight: 'bold',
            }}
          >
            {element.name}
          </Typography>
          <Chip
            label={element.type.toUpperCase()}
            size="small"
            sx={{
              mt: 1,
              backgroundColor: element.type === 'element' ? '#ff6644' :
                               element.type === 'partition' ? '#4466ff' : '#00ff00',
              color: '#fff',
              fontFamily: 'monospace',
              fontWeight: 'bold',
            }}
          />
        </Box>

        {/* Element Properties */}
        <Box>
          <Typography variant="subtitle2" sx={{ color: '#aaa', fontFamily: 'monospace', mb: 1 }}>
            PROPERTIES
          </Typography>
          <Stack spacing={0.5}>
            {element.tonalityType && (
              <Typography sx={{ color: '#fff', fontFamily: 'monospace', fontSize: '0.9rem' }}>
                <strong style={{ color: '#4466ff' }}>Tonality:</strong> {element.tonalityType}
              </Typography>
            )}
            {element.strategy && (
              <Typography sx={{ color: '#fff', fontFamily: 'monospace', fontSize: '0.9rem' }}>
                <strong style={{ color: '#4466ff' }}>Strategy:</strong> {element.strategy}
              </Typography>
            )}
            {element.depth !== undefined && (
              <Typography sx={{ color: '#fff', fontFamily: 'monospace', fontSize: '0.9rem' }}>
                <strong style={{ color: '#4466ff' }}>Depth:</strong> {element.depth}
              </Typography>
            )}
            {element.distance !== undefined && (
              <Typography sx={{ color: '#fff', fontFamily: 'monospace', fontSize: '0.9rem' }}>
                <strong style={{ color: '#4466ff' }}>Distance:</strong> {element.distance.toFixed(2)} units
              </Typography>
            )}
          </Stack>
        </Box>

        <Divider sx={{ borderColor: '#333' }} />

        {/* Loading Indicator */}
        {loading && (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 2 }}>
            <CircularProgress size={24} sx={{ color: '#4466ff' }} />
          </Box>
        )}

        {/* Chord Voicings (for elements) */}
        {element.type === 'element' && voicings.length > 0 && !loading && (
          <Box>
            <Typography variant="subtitle2" sx={{ color: '#aaa', fontFamily: 'monospace', mb: 1 }}>
              CHORD VOICINGS ({voicings.length})
            </Typography>
            
            {/* Fretboard Diagram */}
            {selectedVoicing && selectedVoicing.positions && selectedVoicing.positions.length > 0 && (
              <Box sx={{ mb: 2, backgroundColor: '#000', p: 1, borderRadius: 1 }}>
                <RealisticFretboard
                  positions={voicingToPositions(selectedVoicing)}
                  config={{
                    fretCount: 15,
                    stringCount: 6,
                    tuning: ['E', 'B', 'G', 'D', 'A', 'E'],
                    showFretNumbers: true,
                    showStringLabels: false,
                    width: 350,
                    height: 120,
                    spacingMode: 'realistic',
                    flipped: true,
                  }}
                />
                <Typography sx={{ color: '#888', fontFamily: 'monospace', fontSize: '0.75rem', mt: 1, textAlign: 'center' }}>
                  {selectedVoicing.notes?.join(' - ') || 'Notes not available'}
                </Typography>
              </Box>
            )}

            {/* Voicing List */}
            <Stack spacing={1}>
              {voicings.map((voicing, index) => {
                // Safety check: ensure voicing has required properties
                if (!voicing || !voicing.fretRange) {
                  return null;
                }

                return (
                  <Box
                    key={index}
                    onClick={() => setSelectedVoicing(voicing)}
                    sx={{
                      p: 1,
                      backgroundColor: selectedVoicing === voicing ? 'rgba(33, 150, 243, 0.2)' : 'rgba(255, 255, 255, 0.05)',
                      border: selectedVoicing === voicing ? '1px solid #2196F3' : '1px solid #333',
                      borderRadius: 1,
                      cursor: 'pointer',
                      '&:hover': {
                        backgroundColor: 'rgba(33, 150, 243, 0.1)',
                      },
                    }}
                  >
                    <Typography sx={{ color: '#fff', fontFamily: 'monospace', fontSize: '0.85rem' }}>
                      <strong>Voicing {index + 1}:</strong> Frets {voicing.fretRange.min}-{voicing.fretRange.max}
                    </Typography>
                    <Typography sx={{ color: '#888', fontFamily: 'monospace', fontSize: '0.75rem' }}>
                      {voicing.difficulty || 'Unknown'} • {voicing.cagedShape || 'N/A'} shape
                    </Typography>
                  </Box>
                );
              })}
            </Stack>
          </Box>
        )}

        {/* Related Chords (for regions) */}
        {element.type === 'region' && relatedChords.length > 0 && !loading && (
          <Box>
            <Typography variant="subtitle2" sx={{ color: '#aaa', fontFamily: 'monospace', mb: 1 }}>
              DIATONIC CHORDS ({relatedChords.length})
            </Typography>
            <Stack spacing={0.5}>
              {relatedChords.map((chord, index) => (
                <Box
                  key={index}
                  sx={{
                    p: 1,
                    backgroundColor: 'rgba(255, 255, 255, 0.05)',
                    border: '1px solid #333',
                    borderRadius: 1,
                  }}
                >
                  <Typography sx={{ color: '#fff', fontFamily: 'monospace', fontSize: '0.85rem' }}>
                    <strong>{chord.name}</strong> {chord.function && `(${chord.function})`}
                  </Typography>
                  <Typography sx={{ color: '#888', fontFamily: 'monospace', fontSize: '0.75rem' }}>
                    {chord.intervals?.join(', ') || 'Intervals not available'}
                  </Typography>
                </Box>
              ))}
            </Stack>
          </Box>
        )}

        {/* Partition Strategy Info */}
        {element.type === 'partition' && element.strategy && !loading && (
          <Box>
            <Typography variant="subtitle2" sx={{ color: '#aaa', fontFamily: 'monospace', mb: 1 }}>
              PARTITION STRATEGY
            </Typography>
            <Typography sx={{ color: '#fff', fontFamily: 'monospace', fontSize: '0.9rem', mb: 1 }}>
              {element.strategy}
            </Typography>
            <Typography sx={{ color: '#888', fontFamily: 'monospace', fontSize: '0.85rem', fontStyle: 'italic' }}>
              {element.strategy === 'CircleOfFifths' && 'Organizes chords by their position in the circle of fifths, grouping harmonically related chords together.'}
              {element.strategy === 'ChromaticDistance' && 'Partitions based on chromatic distance between pitch classes, creating regions of similar tonal color.'}
              {element.strategy === 'HarmonicSeries' && 'Uses the harmonic series to partition chords by their overtone relationships and consonance.'}
              {element.strategy === 'ModalBrightness' && 'Organizes chords by modal brightness, from dark (Phrygian) to bright (Lydian).'}
              {element.strategy === 'TonalStability' && 'Partitions based on tonal stability and tendency toward resolution.'}
            </Typography>
          </Box>
        )}

        {/* Instructions */}
        <Box sx={{ borderTop: '1px solid #333', pt: 1 }}>
          <Typography sx={{ color: '#666', fontFamily: 'monospace', fontSize: '0.75rem' }}>
            {isSelected ? 'Click elsewhere to deselect' : 'Click to select and lock this view'}
          </Typography>
        </Box>
      </Stack>
    </Box>
  );
};

export default ElementInfoPanel;

