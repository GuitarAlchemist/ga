// HarnessTimeline — horizontal Gantt-style strip showing when each item
// shipped. X-axis is calendar time (oldest merge → most recent). Each item
// is a small pill at its merge date, colored by status. Click a pill to
// scroll the item's card into view (id = harness-item-<n>).
//
// Items without a merged_at date stack at the right edge in a "not yet
// shipped" column. The whole strip sits between the donut header and the
// card grid.

import React from 'react';
import { Box, Stack, Tooltip, Typography } from '@mui/material';
import type { HarnessItem } from './types';
import { statusMeta } from './types';

interface Props {
  items: HarnessItem[];
  /** Strip height in px (the SVG body — text axis is rendered below). */
  height?: number;
}

const PILL_W = 22;
const PILL_H = 18;
const PAD_X = 28;
const AXIS_H = 18;

function shortDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso.slice(0, 10);
  return `${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export const HarnessTimeline: React.FC<Props> = ({ items, height = 80 }) => {
  const dated = items.filter((it) => it.merged_at).map((it) => ({ ...it, ts: new Date(it.merged_at!).getTime() }));
  const undated = items.filter((it) => !it.merged_at);

  if (dated.length === 0) {
    return (
      <Typography variant="caption" color="text.secondary">
        No merge timestamps yet — items appear here once they ship.
      </Typography>
    );
  }

  const minTs = Math.min(...dated.map((d) => d.ts));
  const maxTs = Math.max(...dated.map((d) => d.ts));
  // Add 8% padding on each side so the first/last pill aren't cut off.
  const span = Math.max(maxTs - minTs, 1);
  const pad = span * 0.08;
  const lo = minTs - pad;
  const hi = maxTs + pad;

  // Compute positions, then resolve overlaps by stacking vertically.
  const stripH = height - AXIS_H;
  const handleClick = (n: number) => {
    const el = document.getElementById(`harness-item-${n}`);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  const renderRow = (rowItems: typeof dated, totalWidth: number) => {
    type Placed = { item: typeof dated[number]; xPct: number; row: number };
    const placed: Placed[] = [];
    const rows: number[][] = []; // each row: list of x positions in px
    const sortedByTs = [...rowItems].sort((a, b) => a.ts - b.ts);

    for (const it of sortedByTs) {
      const xPct = ((it.ts - lo) / (hi - lo)) * 100;
      const xPx = (xPct / 100) * totalWidth;
      let row = 0;
      while (true) {
        if (!rows[row]) rows[row] = [];
        const collision = rows[row].some((other) => Math.abs(other - xPx) < PILL_W + 4);
        if (!collision) {
          rows[row].push(xPx);
          break;
        }
        row += 1;
      }
      placed.push({ item: it, xPct, row });
    }
    return placed;
  };

  // We render at 100% width so we use percentages and let CSS handle absolute layout.
  // Approximate totalWidth for overlap detection; the strip stretches but pill collision
  // is computed in CSS pixels assuming ~720px (typical card column).
  const approxWidth = 720;
  const placed = renderRow(dated, approxWidth);
  const maxRow = placed.reduce((m, p) => Math.max(m, p.row), 0);

  return (
    <Box>
      <Box
        sx={{
          position: 'relative',
          height,
          width: '100%',
          bgcolor: 'action.hover',
          borderRadius: 1,
          px: `${PAD_X}px`,
          overflow: 'visible',
        }}
      >
        {/* Axis line */}
        <Box
          sx={{
            position: 'absolute',
            left: PAD_X,
            right: PAD_X,
            top: stripH - 1,
            height: 1,
            bgcolor: 'divider',
          }}
        />
        {/* Endpoints */}
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ position: 'absolute', left: 4, top: stripH + 2, fontVariantNumeric: 'tabular-nums' }}
        >
          {shortDate(new Date(minTs).toISOString())}
        </Typography>
        <Typography
          variant="caption"
          color="text.secondary"
          sx={{ position: 'absolute', right: 4, top: stripH + 2, fontVariantNumeric: 'tabular-nums' }}
        >
          {shortDate(new Date(maxTs).toISOString())}
        </Typography>
        {/* Pills */}
        {placed.map(({ item, xPct, row }) => {
          const meta = statusMeta(item.status);
          // Distribute rows across stripH so they don't fall off the axis.
          const rowH = Math.max(PILL_H + 2, (stripH - 6) / Math.max(1, maxRow + 1));
          const topPx = 4 + row * rowH;
          return (
            <Tooltip
              key={item.number}
              arrow
              title={
                <Box>
                  <Typography variant="caption" sx={{ fontWeight: 600, display: 'block' }}>#{item.number}: {item.title}</Typography>
                  <Typography variant="caption" sx={{ display: 'block', opacity: 0.85 }}>
                    {item.merged_at ? new Date(item.merged_at).toLocaleString() : 'unshipped'} · {meta.label}
                  </Typography>
                </Box>
              }
            >
              <Box
                role="button"
                aria-label={`Jump to item ${item.number}: ${item.title}`}
                onClick={() => handleClick(item.number)}
                sx={{
                  position: 'absolute',
                  left: `calc(${xPct}% - ${PILL_W / 2}px + ${PAD_X / 2}px)`,
                  top: topPx,
                  width: PILL_W,
                  height: PILL_H,
                  borderRadius: '4px',
                  bgcolor: meta.color,
                  color: 'common.white',
                  fontSize: 10,
                  fontWeight: 700,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  cursor: 'pointer',
                  boxShadow: 1,
                  transition: 'transform 120ms ease, box-shadow 120ms ease',
                  '&:hover': { transform: 'translateY(-2px)', boxShadow: 3 },
                }}
              >
                {item.number}
              </Box>
            </Tooltip>
          );
        })}
        {/* Undated stack on the right margin */}
        {undated.length > 0 && (
          <Stack
            direction="row"
            spacing={0.5}
            sx={{ position: 'absolute', right: 4, top: 4 }}
          >
            {undated.map((it) => {
              const meta = statusMeta(it.status);
              return (
                <Tooltip
                  key={it.number}
                  arrow
                  title={`#${it.number}: ${it.title} (${meta.label})`}
                >
                  <Box
                    role="button"
                    onClick={() => handleClick(it.number)}
                    sx={{
                      width: PILL_W,
                      height: PILL_H,
                      borderRadius: '4px',
                      bgcolor: meta.color,
                      color: 'common.white',
                      fontSize: 10,
                      fontWeight: 700,
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      cursor: 'pointer',
                      opacity: 0.7,
                    }}
                  >
                    {it.number}
                  </Box>
                </Tooltip>
              );
            })}
          </Stack>
        )}
      </Box>
    </Box>
  );
};

export default HarnessTimeline;
