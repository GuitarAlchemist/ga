import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Alert,
  Autocomplete,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
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
import { fetchAllHierarchyItems, fetchHierarchyLevels } from '../api/musicHierarchyApi';
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

// Build a parent-id for a child item by reading the right metadata key
// and reconstructing the parent's full ID format. Discovered by probing
// the GraphQL API — the metadata stores a raw integer (e.g.
// ParentSetClassId: 0) while the parent item's `id` is `setclass:0`.
//
// PrimeForm / Chord / ChordVoicing don't expose explicit parent IDs in
// their current metadata, so they return undefined and the table shows
// "—" for child counts at those levels (the detail panel will still
// drill in via `loadLevelItems(parentId)` if needed).
function findParentId(item: MusicHierarchyItem): string | undefined {
  const meta = item.metadata;
  if (!meta) return undefined;
  if (item.level === 'ForteNumber') {
    const v = meta['ParentSetClassId'] ?? meta['SetClassId'];
    return v != null && v !== '' ? `setclass:${v}` : undefined;
  }
  return undefined;
}

// Column definitions align with what the GraphQL backend actually emits.
// SetClass/Forte/Prime/Scale are wired against the real domain repository;
// Chord and ChordVoicing currently return placeholder rows (see backend
// `ChordRepository` TODOs), so their tables intentionally stay sparse.
//
// Metadata key lookups use BOTH the new wire form (e.g. `icv`,
// `cardinality`) and the historical PascalCase aliases (`IntervalVector`,
// `Cardinality`) — the API layer in musicHierarchyApi.ts adds the aliases.
const meta = (key: string) => (item: MusicHierarchyItem) => item.metadata?.[key] ?? '—';

const baseTableColumns: Record<MusicHierarchyLevel, TableColumn[]> = {
  SetClass: [
    { key: 'name', label: 'Set Class' },
    { key: 'category', label: 'Category' },
    { key: 'metadata.Cardinality',    label: 'Cardinality',     render: meta('Cardinality') },
    { key: 'metadata.IntervalVector', label: 'Interval Vector', render: meta('IntervalVector') },
    { key: 'metadata.IsModal',        label: 'Modal',           render: meta('IsModal') },
  ],
  ForteNumber: [
    { key: 'name',                 label: 'Forte Number' },
    { key: 'metadata.Cardinality', label: 'Cardinality', render: meta('Cardinality') },
    { key: 'metadata.index',       label: 'Index in Cardinality', render: meta('index') },
  ],
  PrimeForm: [
    { key: 'name',           label: 'Prime Form' },
    { key: 'category',       label: 'Category' },
    { key: 'metadata.count', label: 'Cardinality', render: meta('count') },
    { key: 'metadata.forte', label: 'Forte',       render: meta('forte') },
  ],
  Chord: [
    { key: 'name',     label: 'Chord' },
    { key: 'category', label: 'Family' },
  ],
  ChordVoicing: [
    { key: 'name',     label: 'Position' },
    { key: 'category', label: 'Family' },
  ],
  Scale: [
    { key: 'name',                  label: 'Scale / Mode' },
    { key: 'category',              label: 'Family' },
    { key: 'metadata.notes',        label: 'Notes',  render: meta('notes') },
    { key: 'metadata.forte',        label: 'Forte',  render: meta('forte') },
    { key: 'metadata.common',       label: 'Common', render: meta('common') },
  ],
};

// Friendly label for the next-level column header on a parent's table.
const NEXT_LEVEL_LABEL: Record<MusicHierarchyLevel, string | undefined> = {
  SetClass: 'Forte numbers',
  ForteNumber: 'Prime forms',
  PrimeForm: 'Chords',
  Chord: 'Voicings',
  ChordVoicing: undefined,
  Scale: undefined,
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
      const data = await fetchAllHierarchyItems({ level, parentId });
      setItemsByLevel(prev => ({ ...prev, [level]: data }));
      return data;
    } catch (err: unknown) {
      setError((err instanceof Error ? err.message : null) ?? 'Failed to load hierarchy data.');
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
        // Clear downstream selections so drilling re-anchors at the new
        // parent. Item caches stay populated (eager-loaded at bootstrap)
        // so child counts remain available; tableRows filters by parent
        // selection client-side via findParentId.
        downstreamLevels.forEach(l => {
          updated[l] = null;
        });
        return updated;
      });

      const nextLevel = getNextLevel(level);
      if (!nextLevel || !item) return;

      // ChordVoicing isn't eager-loaded (too many rows); fetch on demand
      // when its parent (Chord) gets selected. Other levels already have
      // full data from bootstrap.
      if (nextLevel === 'ChordVoicing' && (itemsByLevel.ChordVoicing?.length ?? 0) === 0) {
        await loadLevelItems(nextLevel, item.id);
      }

      if (autoAdvance) {
        const haveChildren = (itemsByLevel[nextLevel]?.length ?? 0) > 0;
        if (haveChildren || nextLevel === 'ChordVoicing') {
          setActiveLevel(nextLevel);
        }
      }
    },
    [itemsByLevel, loadLevelItems]
  );

  useEffect(() => {
    const bootstrap = async () => {
      try {
        setInitializing(true);
        setError(null);
        const meta = await fetchHierarchyLevels();
        setLevelsInfo(meta);
        // Block on the first level (so the table has something to show)
        // and fire-and-forget the rest in the background. As they land,
        // child indexes rebuild via `useMemo` and the # Forte numbers
        // column populates progressively. Chord can be very large; not
        // blocking on it keeps the page interactive.
        await loadLevelItems('SetClass');
        // Background fills.
        const backgroundLevels: MusicHierarchyLevel[] = ['ForteNumber', 'PrimeForm', 'Chord', 'Scale'];
        backgroundLevels.forEach(level => {
          void loadLevelItems(level);
        });
      } catch (err: unknown) {
        setError((err instanceof Error ? err.message : null) ?? 'Music hierarchy API is unavailable.');
      } finally {
        setInitializing(false);
      }
    };

    bootstrap();
  }, [loadLevelItems]);

  // Build a child index per level: for each level L > 0, group its items
  // by parent-id. So `childIndex.ForteNumber.get(setClassId)` gives all
  // ForteNumbers that belong to that SetClass. Used to render the
  // "Children" + "Example" columns and the detail-panel children list.
  const childIndex = useMemo(() => {
    const result: Partial<Record<MusicHierarchyLevel, Map<string, MusicHierarchyItem[]>>> = {};
    for (const level of levelOrder) {
      const map = new Map<string, MusicHierarchyItem[]>();
      const items = itemsByLevel[level] ?? [];
      for (const item of items) {
        const pid = findParentId(item);
        if (!pid) continue;
        const arr = map.get(pid) ?? [];
        arr.push(item);
        map.set(pid, arr);
      }
      result[level] = map;
    }
    return result;
  }, [itemsByLevel]);

  // Pick a "representative" child — favour the alphabetically/numerically
  // earliest sibling, which is usually the canonical / simplest member of
  // the family (e.g. 4-1 for tetrachord set class 4-Z15-ish).
  const pickRepresentative = useCallback((children: MusicHierarchyItem[]): MusicHierarchyItem | undefined => {
    if (!children.length) return undefined;
    return [...children].sort((a, b) => a.name.localeCompare(b.name))[0];
  }, []);

  // tableColumns extended with Children count + Example columns when the
  // active level has a downstream level that we have child data for.
  const tableColumns = useMemo<Record<MusicHierarchyLevel, TableColumn[]>>(() => {
    const result = { ...baseTableColumns };
    for (const level of levelOrder) {
      const next = getNextLevel(level);
      if (!next) continue;
      const childMap = childIndex[next];
      if (!childMap || childMap.size === 0) continue;
      const label = NEXT_LEVEL_LABEL[level] ?? next;
      result[level] = [
        ...baseTableColumns[level],
        {
          key: '__childCount',
          label: `# ${label}`,
          render: (item) => {
            const kids = childMap.get(item.id);
            return kids?.length ?? '—';
          },
        },
        {
          key: '__childExample',
          label: 'e.g.',
          render: (item) => {
            const kids = childMap.get(item.id);
            const rep = kids ? pickRepresentative(kids) : undefined;
            return rep?.name ?? '—';
          },
        },
      ];
    }
    return result;
  }, [childIndex, pickRepresentative]);

  const tableRows = useMemo(() => {
    let rows = itemsByLevel[activeLevel] ?? [];

    // Drill-down filter: when an upstream level has a selection, restrict
    // the active table to children of that selection (client-side filter
    // via the parent-id metadata key). Lets the user keep navigation
    // context without losing eager-loaded data.
    const idx = levelOrder.indexOf(activeLevel);
    if (idx > 0) {
      const parentLevel = levelOrder[idx - 1];
      const parent = selectedItems[parentLevel];
      if (parent) {
        const filtered = rows.filter(r => findParentId(r) === parent.id);
        // Only narrow if the filter actually matches something — avoids
        // showing an empty table when the parent-id key isn't exposed.
        if (filtered.length > 0) rows = filtered;
      }
    }

    if (!tableFilter.trim()) return rows;
    const needle = tableFilter.toLowerCase();
    return rows.filter(item =>
      item.name.toLowerCase().includes(needle) ||
      item.category.toLowerCase().includes(needle) ||
      (item.description ?? '').toLowerCase().includes(needle) ||
      item.tags.some(tag => tag.toLowerCase().includes(needle))
    );
  }, [itemsByLevel, selectedItems, activeLevel, tableFilter]);

  const selectedForActive = selectedItems[activeLevel];

  return (
    // Full-bleed shell: page itself doesn't scroll; the level-card column
    // and the table panel scroll independently. Header is fixed at top.
    <Box
      sx={{
        width: '100%',
        height: 'calc(100vh - 48px)',
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        bgcolor: 'background.default',
      }}
    >
      {/* Header (non-scrolling) */}
      <Box sx={{ px: 3, py: 2, borderBottom: 1, borderColor: 'divider', flexShrink: 0 }}>
        <Typography variant="h4" fontWeight={700}>
          Music Hierarchy Navigator
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Explore the entire atonal/tonal hierarchy from Set Classes to guitar voicings and compatible scales.
        </Typography>
        {error && <Alert severity="error" sx={{ mt: 1 }}>{error}</Alert>}
        {initializing && (
          <Box display="flex" alignItems="center" gap={2} sx={{ mt: 1 }}>
            <CircularProgress size={16} />
            <Typography variant="caption" color="text.secondary">
              Initializing hierarchy data...
            </Typography>
          </Box>
        )}
      </Box>

      {/* Body — two columns sharing remaining vertical space. */}
      <Box sx={{ flex: 1, display: 'flex', minHeight: 0, gap: 2, p: 2 }}>
        {/* Left column — level cards scroll vertically inside the column. */}
        <Box
          sx={{
            width: { xs: '100%', md: 360 },
            flexShrink: 0,
            display: { xs: 'none', md: 'flex' },
            flexDirection: 'column',
            overflowY: 'auto',
            overflowX: 'hidden',
            pr: 1,
          }}
        >
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
        </Box>

        {/* Right column — toolbar fixed at top of column, table scrolls
            inside its own panel, selected-item detail (when present)
            takes a bottom slice with its own scroll. */}
        <Box
          sx={{
            flex: 1,
            minWidth: 0,
            display: 'flex',
            flexDirection: 'column',
            gap: 2,
          }}
        >
          <Paper variant="outlined" sx={{ p: 2, flexShrink: 0 }}>
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

          <Paper
            variant="outlined"
            sx={{
              flex: 1,
              minHeight: 0,
              overflow: 'auto',  // scrollbar lives on the table panel
              // Sticky header so the table can scroll while the column
              // labels stay visible.
              '& thead th': {
                position: 'sticky',
                top: 0,
                bgcolor: 'background.paper',
                zIndex: 1,
                borderBottom: 1,
                borderColor: 'divider',
              },
            }}
          >
            <Table size="small" stickyHeader>
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
                      onClick={() => handleSelectionChange(activeLevel, row, true).catch(() => undefined)}
                      sx={{ cursor: 'pointer' }}
                    >
                      {tableColumns[activeLevel].map(column => (
                        <TableCell key={`${row.id}-${column.key}`}>
                          {column.render ? column.render(row) : (row as Record<string, unknown>)[column.key.split('.').at(-1) ?? column.key] as string ?? '—'}
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
            <Paper
              variant="outlined"
              sx={{
                flexShrink: 0,
                maxHeight: '40%',
                overflow: 'auto',
              }}
            >
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

                {(() => {
                  const next = getNextLevel(activeLevel);
                  if (!next) return null;
                  const nextLabel = NEXT_LEVEL_LABEL[activeLevel] ?? next;
                  const kids = childIndex[next]?.get(selectedForActive.id) ?? [];
                  if (kids.length === 0) return null;
                  // Show up to 8 representative children, sorted alphabetically.
                  const sample = [...kids].sort((a, b) => a.name.localeCompare(b.name)).slice(0, 8);
                  return (
                    <>
                      <Divider sx={{ my: 2 }} />
                      <Box display="flex" alignItems="baseline" justifyContent="space-between" mb={1}>
                        <Typography variant="subtitle2">
                          {kids.length} {nextLabel} · {kids.length > sample.length ? `top ${sample.length}` : 'all'}
                        </Typography>
                        <Typography
                          variant="caption"
                          color="primary"
                          sx={{ cursor: 'pointer' }}
                          onClick={() => setActiveLevel(next)}
                        >
                          View all →
                        </Typography>
                      </Box>
                      <Box display="flex" flexWrap="wrap" gap={0.75}>
                        {sample.map(child => (
                          <Chip
                            key={child.id}
                            size="small"
                            label={child.name}
                            onClick={() => handleSelectionChange(next, child, false).catch(() => undefined)}
                            sx={{ cursor: 'pointer' }}
                          />
                        ))}
                      </Box>
                    </>
                  );
                })()}
              </CardContent>
            </Paper>
          )}
        </Box>
      </Box>
    </Box>
  );
};

export default MusicHierarchyDemo;
