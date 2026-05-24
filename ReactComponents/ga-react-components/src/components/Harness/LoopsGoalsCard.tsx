// Runtime visibility for Claude Code's /loop and /goal slash commands.
//
// Both are session-scoped with zero disk telemetry by default, so a second
// Claude window can't see "what is the first one looping on right now?".
// This card closes that gap by reading the append-only JSONL written by
// .claude/hooks/loops-goals-tracker.ps1 via /dev-data/runtime-loops-goals.
//
// Empty state: friendly hint that /loop or /goal needs to be invoked.
// Auto-refreshes every 15s. Stop button POSTs status=completed.
//
// Coordination note: harness-tab-redesign-agent is also touching
// src/components/Harness/. Conflict-resolution strategy lives in the PR body.

import React, { useCallback, useEffect, useState } from 'react';
import {
    Alert,
    Box,
    Button,
    Chip,
    CircularProgress,
    Paper,
    Stack,
    Tooltip,
    Typography,
} from '@mui/material';
import AllInclusiveIcon from '@mui/icons-material/AllInclusive';
import FlagIcon from '@mui/icons-material/Flag';
import StopCircleIcon from '@mui/icons-material/StopCircle';
import VisibilityIcon from '@mui/icons-material/Visibility';

// Mirrors src/dev-data/parsers.ts — duplicated locally so the component
// stays decoupled (parsers.ts is also imported by vite.config.ts; importing
// it into a runtime component module would pull node-only types into the
// browser bundle on some Vite configs).
interface ActiveRow {
    id: string;
    kind: 'loop' | 'goal';
    started_at: string;
    session_id: string;
    prompt_or_condition: string;
    turn_count: number;
    last_activity_at: string;
    status: 'active' | 'paused' | 'completed' | 'archived';
    age_min: number;
    last_activity_min_ago: number;
    branch?: string | null;
}

interface CompletedRow {
    id: string;
    kind: 'loop' | 'goal';
    started_at: string;
    last_activity_at: string;
    turn_count: number;
    prompt_or_condition: string;
}

interface Payload {
    fetched_at: string;
    active_loops: ActiveRow[];
    active_goals: ActiveRow[];
    completed_recent: CompletedRow[];
    total_records: number;
}

const REFRESH_MS = 15_000;
const TRUNCATE_AT = 60;

function relativeMin(n: number): string {
    if (n < 0) return '—';
    if (n < 1) return 'just now';
    if (n === 1) return '1 min ago';
    if (n < 60) return `${n} min ago`;
    const hours = Math.floor(n / 60);
    if (hours === 1) return '1 h ago';
    if (hours < 24) return `${hours} h ago`;
    const days = Math.floor(hours / 24);
    return days === 1 ? '1 d ago' : `${days} d ago`;
}

function truncate(s: string, max: number): string {
    if (!s) return '';
    if (s.length <= max) return s;
    return s.slice(0, max - 1) + '…';
}

function ActiveTable(props: { rows: ActiveRow[]; onStop: (id: string) => Promise<void>; busyId: string | null; kind: 'loop' | 'goal' }) {
    const { rows, onStop, busyId, kind } = props;
    if (rows.length === 0) {
        return (
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', py: 0.5 }}>
                None active.
            </Typography>
        );
    }
    return (
        <Box sx={{ overflow: 'auto' }}>
            <Box component="table" sx={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
                <Box
                    component="thead"
                    sx={{ '& th': { textAlign: 'left', py: 0.75, color: 'text.secondary', fontWeight: 600, fontSize: '0.72rem', borderBottom: '1px solid', borderColor: 'divider' } }}
                >
                    <tr>
                        <th>{kind === 'loop' ? 'Loop prompt' : 'Goal condition'}</th>
                        <th style={{ width: 70, textAlign: 'right' }}>Turns</th>
                        <th style={{ width: 110 }}>Started</th>
                        <th style={{ width: 110 }}>Last activity</th>
                        <th style={{ width: 80, textAlign: 'right' }}>Action</th>
                    </tr>
                </Box>
                <Box component="tbody" sx={{ '& td': { py: 1, borderBottom: '1px solid', borderColor: 'divider', verticalAlign: 'top' } }}>
                    {rows.map((row) => (
                        <tr key={row.id}>
                            <td>
                                <Stack direction="row" spacing={0.5} alignItems="flex-start">
                                    <Tooltip title={row.prompt_or_condition || '(empty)'} placement="top-start" arrow>
                                        <Typography
                                            variant="body2"
                                            component="span"
                                            sx={{
                                                fontFamily: 'monospace',
                                                fontSize: '0.78rem',
                                                wordBreak: 'break-word',
                                            }}
                                        >
                                            {truncate(row.prompt_or_condition || '(no prompt captured)', TRUNCATE_AT)}
                                        </Typography>
                                    </Tooltip>
                                    {row.status === 'paused' && (
                                        <Chip label="paused" size="small" color="warning" sx={{ height: 18, fontSize: '0.65rem' }} />
                                    )}
                                </Stack>
                                <Typography variant="caption" color="text.disabled" sx={{ display: 'block', fontSize: '0.65rem', mt: 0.25 }}>
                                    session {row.session_id.slice(0, 8)} · branch {row.branch ?? '—'}
                                </Typography>
                            </td>
                            <td style={{ textAlign: 'right' }}>
                                <Typography variant="body2" sx={{ fontFamily: 'monospace' }}>{row.turn_count}</Typography>
                            </td>
                            <td>
                                <Tooltip title={row.started_at} arrow>
                                    <Typography variant="caption" color="text.secondary">
                                        {relativeMin(row.age_min)}
                                    </Typography>
                                </Tooltip>
                            </td>
                            <td>
                                <Tooltip title={row.last_activity_at} arrow>
                                    <Typography variant="caption" color="text.secondary">
                                        {relativeMin(row.last_activity_min_ago)}
                                    </Typography>
                                </Tooltip>
                            </td>
                            <td style={{ textAlign: 'right' }}>
                                <Button
                                    size="small"
                                    variant="outlined"
                                    color="warning"
                                    onClick={() => onStop(row.id)}
                                    disabled={busyId === row.id}
                                    startIcon={busyId === row.id ? <CircularProgress size={12} /> : <StopCircleIcon sx={{ fontSize: 16 }} />}
                                    sx={{ fontSize: '0.7rem', minWidth: 0, py: 0.25, px: 0.75 }}
                                >
                                    Stop
                                </Button>
                            </td>
                        </tr>
                    ))}
                </Box>
            </Box>
        </Box>
    );
}

const LoopsGoalsCard: React.FC = () => {
    const [data, setData] = useState<Payload | null>(null);
    const [error, setError] = useState<string | null>(null);
    const [busyId, setBusyId] = useState<string | null>(null);

    const load = useCallback(async () => {
        try {
            const r = await fetch('/dev-data/runtime-loops-goals');
            if (!r.ok) throw new Error(`HTTP ${r.status}`);
            const json = (await r.json()) as Payload;
            setData(json);
            setError(null);
        } catch (e) {
            setError(String((e as Error).message ?? e));
        }
    }, []);

    useEffect(() => {
        let cancelled = false;
        void load();
        const id = setInterval(() => {
            if (!cancelled) void load();
        }, REFRESH_MS);
        return () => {
            cancelled = true;
            clearInterval(id);
        };
    }, [load]);

    const onStop = useCallback(async (id: string) => {
        setBusyId(id);
        try {
            const r = await fetch(`/dev-data/runtime-loops-goals/stop/${encodeURIComponent(id)}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({}),
            });
            if (!r.ok) {
                const body = await r.text();
                throw new Error(`HTTP ${r.status}: ${body}`);
            }
            await load();
        } catch (e) {
            setError(String((e as Error).message ?? e));
        } finally {
            setBusyId(null);
        }
    }, [load]);

    const loopCount = data?.active_loops.length ?? 0;
    const goalCount = data?.active_goals.length ?? 0;
    const totalActive = loopCount + goalCount;
    const completedCount = data?.completed_recent.length ?? 0;

    return (
        <Paper sx={{ p: 2 }}>
            <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
                <VisibilityIcon fontSize="small" sx={{ color: 'primary.main' }} />
                <Typography variant="h6">Active loops &amp; goals</Typography>
                {data && (
                    <>
                        <Chip
                            icon={<AllInclusiveIcon sx={{ fontSize: 14 }} />}
                            label={`${loopCount} loop${loopCount === 1 ? '' : 's'}`}
                            size="small"
                            color={loopCount > 0 ? 'primary' : 'default'}
                            sx={{ fontSize: '0.7rem' }}
                        />
                        <Chip
                            icon={<FlagIcon sx={{ fontSize: 14 }} />}
                            label={`${goalCount} goal${goalCount === 1 ? '' : 's'}`}
                            size="small"
                            color={goalCount > 0 ? 'primary' : 'default'}
                            sx={{ fontSize: '0.7rem' }}
                        />
                        <Box sx={{ flex: 1 }} />
                        <Typography variant="caption" color="text.secondary">
                            refreshed {new Date(data.fetched_at).toLocaleTimeString()}
                        </Typography>
                    </>
                )}
            </Stack>

            <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
                Cross-session visibility for Claude Code{' '}
                <Box component="code" sx={{ px: 0.5, bgcolor: 'action.hover', borderRadius: 0.5, fontSize: '0.78rem' }}>/loop</Box>{' '}
                and{' '}
                <Box component="code" sx={{ px: 0.5, bgcolor: 'action.hover', borderRadius: 0.5, fontSize: '0.78rem' }}>/goal</Box>{' '}
                invocations. Each Stop hook turn bumps the per-record turn count.
                Telemetry is read from{' '}
                <Box component="code" sx={{ px: 0.5, bgcolor: 'action.hover', borderRadius: 0.5, fontSize: '0.78rem' }}>state/.runtime-loops-goals.jsonl</Box>{' '}
                (gitignored, per-developer).
            </Typography>

            {error && (
                <Alert severity="error" sx={{ mb: 1 }}>
                    Failed to load /dev-data/runtime-loops-goals: {error}
                </Alert>
            )}

            {!data && !error && <CircularProgress size={20} />}

            {data && totalActive === 0 && completedCount === 0 && (
                <Alert severity="info" sx={{ mt: 1 }}>
                    No active loops or goals — invoke{' '}
                    <Box component="code" sx={{ px: 0.5, bgcolor: 'action.hover', borderRadius: 0.5 }}>/loop</Box>{' '}
                    or{' '}
                    <Box component="code" sx={{ px: 0.5, bgcolor: 'action.hover', borderRadius: 0.5 }}>/goal</Box>{' '}
                    in any Claude session to populate this view.
                </Alert>
            )}

            {data && (totalActive > 0 || completedCount > 0) && (
                <Stack spacing={2} sx={{ mt: 1 }}>
                    <Box>
                        <Stack direction="row" spacing={0.5} alignItems="center" sx={{ mb: 0.5 }}>
                            <AllInclusiveIcon sx={{ fontSize: 16, color: 'text.secondary' }} />
                            <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>Active loops</Typography>
                        </Stack>
                        <ActiveTable rows={data.active_loops} onStop={onStop} busyId={busyId} kind="loop" />
                    </Box>

                    <Box>
                        <Stack direction="row" spacing={0.5} alignItems="center" sx={{ mb: 0.5 }}>
                            <FlagIcon sx={{ fontSize: 16, color: 'text.secondary' }} />
                            <Typography variant="subtitle2" sx={{ fontWeight: 600 }}>Active goals</Typography>
                        </Stack>
                        <ActiveTable rows={data.active_goals} onStop={onStop} busyId={busyId} kind="goal" />
                    </Box>

                    {completedCount > 0 && (
                        <Box>
                            <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 0.5 }}>
                                Completed recently ({completedCount})
                            </Typography>
                            <Stack spacing={0.5}>
                                {data.completed_recent.slice(0, 5).map((row) => (
                                    <Box key={row.id} sx={{ display: 'flex', alignItems: 'baseline', gap: 1 }}>
                                        <Chip
                                            label={row.kind}
                                            size="small"
                                            variant="outlined"
                                            sx={{ height: 18, fontSize: '0.65rem' }}
                                        />
                                        <Tooltip title={row.prompt_or_condition || '(empty)'} arrow>
                                            <Typography variant="caption" sx={{ fontFamily: 'monospace', fontSize: '0.72rem' }}>
                                                {truncate(row.prompt_or_condition, TRUNCATE_AT)}
                                            </Typography>
                                        </Tooltip>
                                        <Box sx={{ flex: 1 }} />
                                        <Typography variant="caption" color="text.disabled">
                                            {row.turn_count} turn{row.turn_count === 1 ? '' : 's'} · {row.last_activity_at}
                                        </Typography>
                                    </Box>
                                ))}
                            </Stack>
                        </Box>
                    )}
                </Stack>
            )}
        </Paper>
    );
};

export default LoopsGoalsCard;
