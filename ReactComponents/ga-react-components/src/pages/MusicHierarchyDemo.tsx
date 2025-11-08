import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Container,
  Divider,
  Grid,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import { fetchHierarchyItems, fetchHierarchyLevels } from '../api/musicHierarchyApi';
import { MusicHierarchyItem, MusicHierarchyLevel, MusicHierarchyLevelInfo } from '../types/musicHierarchy';

const levelOrder: MusicHierarchyLevel[] = [
  'SetClass',
  'ForteNumber',
  'PrimeForm',
  'Chord',
  'ChordVoicing',
  'Scale',
];

const levelLabels: Record<MusicHierarchyLevel, string> = {
  SetClass: 'Set Classes',
  ForteNumber: 'Forte Numbers',
  PrimeForm: 'Prime Forms',
  Chord: 'Chords',
  ChordVoicing: 'Voicings',
  Scale: 'Scales',
};

const initialItemsState = levelOrder.reduce((acc, level) => {
  acc[level] = [] as MusicHierarchyItem[];
  return acc;
}, {} as Record<MusicHierarchyLevel, MusicHierarchyItem[]>);

const initialSelectionState = levelOrder.reduce((acc, level) => {
  acc[level] = null;
  return acc;
}, {} as Record<MusicHierarchyLevel, MusicHierarchyItem | null>);

const initialLoadingState = levelOrder.reduce((acc, level) => {
  acc[level] = false;
  return acc;
}, {} as Record<MusicHierarchyLevel, boolean>);

interface TableColumn {
  key: string;
  label: string;
  render?: (item: MusicHierarchyItem) => React.ReactNode;
}

const tableColumns: Record<MusicHierarchyLevel, TableColumn[]> = {
  SetClass: [
    { key: 'name', label: 'Set Class' },
    { key: 'category', label: 'Category' },
    { key: 'metadata.Cardinality', label: 'Cardinality', render: item => item.metadata?.['Cardinality'] ?? '—' },
    { key: 'metadata.IntervalVector', label: 'Interval Vector', render: item => item.metadata?.['IntervalVector'] ?? '—' },
  ],
  ForteNumber: [
    { key: 'name', label: 'Forte Number' },
    { key: 'metadata.Cardinality', label: 'Cardinality', render: item => item.metadata?.['Cardinality'] ?? '—' },
    { key: 'metadata.ParentSetClassId', label: 'Set Class', render: item => item.metadata?.['ParentSetClassId'] ?? '—' },
  ],
  PrimeForm: [
    { key: 'name', label: 'Prime Form' },
    { key: 'category', label: 'Category' },
    { key: 'metadata.Cardinality', label: 'Cardinality', render: item => item.metadata?.['Cardinality'] ?? '—' },
  ],
  Chord: [
    { key: 'name', label: 'Chord' },
    { key: 'category', label: 'Family' },
    { key: 'metadata.Extension', label: 'Extension', render: item => item.metadata?.['Extension'] ?? '—' },
    { key: 'metadata.NoteCount', label: '# Notes', render: item => item.metadata?.['NoteCount'] ?? '—' },
  ],
  ChordVoicing: [
    { key: 'name', label: 'Position' },
    { key: 'metadata.Frets', label: 'Frets', render: item => item.metadata?.['Frets'] ?? '—' },
    { key: 'metadata.Strings', label: 'Strings', render: item => item.metadata?.['Strings'] ?? '—' },
    { key: 'metadata.Root', label: 'Root', render: item => item.metadata?.['Root'] ?? '—' },
  ],
  Scale: [
    { key: 'name', label: 'Scale / Mode' },
    { key: 'category', label: 'Family' },
    { key: 'metadata.Root', label: 'Root', render: item => item.metadata?.['Root'] ?? '—' },
    { key: 'metadata.NoteCount', label: '# Notes', render: item => item.metadata?.['NoteCount'] ?? '—' },
  ],
};

const getNextLevel = (current: MusicHierarchyLevel): MusicHierarchyLevel | undefined => {
  const index = levelOrder.indexOf(current);
  if (index === -1) return undefined;
  return levelOrder[index + 1];
};

const MusicHierarchyDemo: React.FC = () => {
  const [levelsInfo, setLevelsInfo] = useState<MusicHierarchyLevelInfo[]>([]);
  const [itemsByLevel, setItemsByLevel] = useState(initialItemsState);
  const [selectedItems, setSelectedItems] = useState(initialSelectionState);
  const [loadingByLevel, setLoadingByLevel] = useState(initialLoadingState);
  const [activeLevel, setActiveLevel] = useState<MusicHierarchyLevel>('SetClass');
  const [error, setError] = useState<string | null>(null);
  const [tableFilter, setTableFilter] = useState('');
  const [initializing, setInitializing] = useState(true);

  const loadLevelItems = useCallback(async (level: MusicHierarchyLevel, parentId?: string) => {
    setLoadingByLevel(prev => ({ ...prev, [level]: true }));
    try {
      const data = await fetchHierarchyItems({ level, parentId, take: level === 'SetClass' ? 120 : 80 });
      setItemsByLevel(prev => ({ ...prev, [level]: data }));
      return data;
    } catch (err: any) {
      setError(err?.message ?? 'Failed to load hierarchy data.');
      return [];
    } finally {
      setLoadingByLevel(prev => ({ ...prev, [level]: false }));
    }
  }, []);

  const handleSelectionChange = useCallback(
    async (level: MusicHierarchyLevel, item: MusicHierarchyItem | null, autoAdvance = false) => {
      setError(null);
      setActiveLevel(level);

      const levelIndex = levelOrder.indexOf(level);
      const downstreamLevels = levelOrder.slice(levelIndex + 1);

      setSelectedItems(prev => {
        const updated = { ...prev, [level]: item };
        downstreamLevels.forEach(l => {
          updated[l] = null;
        });
        return updated;
      });

      setItemsByLevel(prev => {
        const updated = { ...prev };
        downstreamLevels.forEach(l => {
          updated[l] = [];
        });
        return updated;
      });

      const nextLevel = getNextLevel(level);
      if (!nextLevel || !item) return;

      const nextItems = await loadLevelItems(nextLevel, item.id);
      if (autoAdvance && nextItems.length) {
        await handleSelectionChange(nextLevel, nextItems[0], true);
      }
    },
    [loadLevelItems]
  );

  useEffect(() => {
    const bootstrap = async () => {
      try {
        setInitializing(true);
        const [meta] = await Promise.all([fetchHierarchyLevels()]);
        setLevelsInfo(meta);
        const rootItems = await loadLevelItems('SetClass');
        if (rootItems.length) {
          await handleSelectionChange('SetClass', rootItems[0], true);
        }
      } catch (err: any) {
        setError(err?.message ?? 'Failed to initialize hierarchy data.');
      } finally {
        setInitializing(false);
      }
    };

    bootstrap();
  }, [handleSelectionChange, loadLevelItems]);

  const tableRows = useMemo(() => {
    const rows = itemsByLevel[activeLevel] ?? [];
    if (!tableFilter.trim()) return rows;
    const needle = tableFilter.toLowerCase();
    return rows.filter(item =>
      item.name.toLowerCase().includes(needle) ||
      item.category.toLowerCase().includes(needle) ||
      (item.description ?? '').toLowerCase().includes(needle) ||
      item.tags.some(tag => tag.toLowerCase().includes(needle))
    );
  }, [itemsByLevel, activeLevel, tableFilter]);

  const selectedForActive = selectedItems[activeLevel];

  return (
    <Container maxWidth="xl" sx={{ py: 4 }}>
      <Box display="flex" flexDirection="column" gap={3}>
        <Box>
          <Typography variant="h3" fontWeight={700} gutterBottom>
            Music Hierarchy Navigator
          </Typography>
          <Typography variant="subtitle1" color="text.secondary">
            Explore the entire atonal/tonal hierarchy from Set Classes to guitar voicings and compatible scales.
          </Typography>
        </Box>

        {error && <Alert severity="error">{error}</Alert>}

        {initializing && (
          <Box display="flex" alignItems="center" gap={2}>
            <CircularProgress size={20} />
            <Typography variant="body2" color="text.secondary">
              Initializing hierarchy data...
            </Typography>
          </Box>
        )}

        <Grid container spacing={3}>
          <Grid item xs={12} md={4}>
            <Box display="flex" flexDirection="column" gap={2}>
              {levelOrder.map(level => {
                const info = levelsInfo.find(l => l.level === level);
                const options = itemsByLevel[level] ?? [];
                const selected = selectedItems[level];
                return (
                  <Card key={level} variant="outlined">
                    <CardContent>
                      <Box display="flex" justifyContent="space-between" alignItems="center" mb={1}>
                        <Typography variant="subtitle2" color="text.secondary">
                          {levelLabels[level]}
                        </Typography>
                        {loadingByLevel[level] && <CircularProgress size={16} />}
                      </Box>
                      <Autocomplete
                        size="small"
                        options={options}
                        value={selected}
                        loading={loadingByLevel[level]}
                        onChange={(_, value) => {
                          handleSelectionChange(level, value ?? null, true).catch(() => undefined);
                        }}
                        getOptionLabel={option => option?.name ?? ''}
                        isOptionEqualToValue={(option, value) => option.id === value.id}
                        renderInput={params => (
                          <TextField
                            {...params}
                            label={info?.displayName ?? levelLabels[level]}
                            placeholder={options.length ? 'Select...' : 'No data yet'}
                          />
                        )}
                        renderOption={(props, option) => (
                          <li {...props} key={option.id}>
                            <Box display="flex" flexDirection="column" width="100%">
                              <Typography variant="body2">{option.name}</Typography>
                              <Typography variant="caption" color="text.secondary">
                                {option.description ?? option.category}
                              </Typography>
                            </Box>
                          </li>
                        )}
                        disableClearable={!selected}
                      />
                      {info && (
                        <Box mt={1.5} display="flex" flexWrap="wrap" gap={1}>
                          {info.highlights.slice(0, 3).map(highlight => (
                            <Chip size="small" key={highlight} label={highlight} />
                          ))}
                        </Box>
                      )}
                    </CardContent>
                  </Card>
                );
              })}
            </Box>
          </Grid>

          <Grid item xs={12} md={8}>
            <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
              <Box display="flex" flexWrap="wrap" gap={2} alignItems="center" justifyContent="space-between">
                <ToggleButtonGroup
                  size="small"
                  value={activeLevel}
                  exclusive
                  onChange={(_, value) => value && setActiveLevel(value)}
                >
                  {levelOrder.map(level => (
                    <ToggleButton key={level} value={level}>
                      {levelLabels[level]}
                    </ToggleButton>
                  ))}
                </ToggleButtonGroup>
                <TextField
                  size="small"
                  label="Filter table"
                  value={tableFilter}
                  onChange={event => setTableFilter(event.target.value)}
                />
              </Box>
            </Paper>

            <Paper variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    {tableColumns[activeLevel].map(column => (
                      <TableCell key={column.key}>{column.label}</TableCell>
                    ))}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {tableRows.map(row => (
                    <TableRow
                      key={row.id}
                      hover
                      selected={selectedForActive?.id === row.id}
                      onClick={() => handleSelectionChange(activeLevel, row).catch(() => undefined)}
                      sx={{ cursor: 'pointer' }}
                    >
                      {tableColumns[activeLevel].map(column => (
                        <TableCell key={`${row.id}-${column.key}`}>
                          {column.render ? column.render(row) : (row as any)[column.key.split('.').at(-1) ?? column.key] ?? '—'}
                        </TableCell>
                      ))}
                    </TableRow>
                  ))}
                  {!tableRows.length && (
                    <TableRow>
                      <TableCell colSpan={tableColumns[activeLevel].length} align="center">
                        <Typography variant="body2" color="text.secondary">
                          No data available for this level.
                        </Typography>
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </Paper>

            {selectedForActive && (
              <Paper variant="outlined" sx={{ mt: 2 }}>
                <CardContent>
                  <Box display="flex" justifyContent="space-between" alignItems="center">
                    <Typography variant="h6">{selectedForActive.name}</Typography>
                    <Chip label={selectedForActive.category} size="small" />
                  </Box>
                  {selectedForActive.description && (
                    <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                      {selectedForActive.description}
                    </Typography>
                  )}
                  <Divider sx={{ my: 2 }} />
                  <Box display="flex" flexWrap="wrap" gap={1} mb={2}>
                    {selectedForActive.tags?.map(tag => (
                      <Chip key={tag} size="small" label={tag} />
                    ))}
                  </Box>
                  <Grid container spacing={2}>
                    {Object.entries(selectedForActive.metadata ?? {}).map(([key, value]) => (
                      <Grid item xs={6} sm={4} key={key}>
                        <Typography variant="caption" color="text.secondary">
                          {key}
                        </Typography>
                        <Typography variant="body2">{value}</Typography>
                      </Grid>
                    ))}
                  </Grid>
                  <Box mt={2} display="flex" justifyContent="flex-end" gap={1}>
                    <Button
                      size="small"
                      variant="outlined"
                      onClick={() => handleSelectionChange(activeLevel, selectedForActive, true).catch(() => undefined)}
                    >
                      Drill Down
                    </Button>
                  </Box>
                </CardContent>
              </Paper>
            )}
          </Grid>
        </Grid>
      </Box>
    </Container>
  );
};

export default MusicHierarchyDemo;
