// HarnessDonut — donut ring showing N of M items shipped, with segments
// colored by status (shipped / ready / in_flight / todo). Replaces the
// chip-count strip on the original harness header.
//
// Implemented as a pure SVG donut so we avoid pulling another recharts
// PieChart wrapper just for 4 segments. ~140px square; center text shows
// "shipped/total". Hover a segment to see its status name.

import React from 'react';
import { Box, Stack, Typography } from '@mui/material';
import type { HarnessItem } from './types';
import { statusMeta } from './types';

interface Props {
  items: HarnessItem[];
  size?: number;
}

interface Segment {
  status: HarnessItem['status'];
  count: number;
  color: string;
  label: string;
}

export const HarnessDonut: React.FC<Props> = ({ items, size = 140 }) => {
  const total = items.length;
  const counts: Record<string, number> = { shipped: 0, 'ready-for-install': 0, in_flight: 0, todo: 0 };
  for (const it of items) counts[it.status] = (counts[it.status] ?? 0) + 1;
  const shipped = counts.shipped;

  const segments: Segment[] = (['shipped', 'ready-for-install', 'in_flight', 'todo'] as const)
    .map((s) => ({
      status: s,
      count: counts[s] ?? 0,
      color: statusMeta(s).color,
      label: statusMeta(s).label,
    }))
    .filter((s) => s.count > 0);

  const stroke = Math.max(14, size * 0.14);
  const radius = (size - stroke) / 2;
  const cx = size / 2;
  const cy = size / 2;
  const circumference = 2 * Math.PI * radius;

  // Build SVG arcs as stroked circles using stroke-dasharray offsets.
  let offset = 0;
  const arcs = segments.map((seg, i) => {
    const len = (seg.count / Math.max(1, total)) * circumference;
    const node = (
      <circle
        key={i}
        cx={cx}
        cy={cy}
        r={radius}
        fill="none"
        stroke={seg.color}
        strokeWidth={stroke}
        strokeDasharray={`${len} ${circumference - len}`}
        strokeDashoffset={-offset}
        transform={`rotate(-90 ${cx} ${cy})`}
        style={{ transition: 'stroke-dasharray 600ms ease' }}
      >
        <title>{`${seg.label}: ${seg.count}`}</title>
      </circle>
    );
    offset += len;
    return node;
  });

  return (
    <Stack direction="row" spacing={2} alignItems="center">
      <Box sx={{ position: 'relative', width: size, height: size, flexShrink: 0 }}>
        <svg width={size} height={size} role="img" aria-label={`${shipped} of ${total} harness items shipped`}>
          {/* track */}
          <circle cx={cx} cy={cy} r={radius} fill="none" stroke="rgba(255,255,255,0.06)" strokeWidth={stroke} />
          {arcs}
        </svg>
        <Box
          sx={{
            position: 'absolute',
            inset: 0,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            pointerEvents: 'none',
          }}
        >
          <Typography variant="h4" sx={{ fontWeight: 700, lineHeight: 1, fontVariantNumeric: 'tabular-nums' }}>
            {shipped}
          </Typography>
          <Typography variant="caption" color="text.secondary" sx={{ fontVariantNumeric: 'tabular-nums' }}>
            of {total}
          </Typography>
        </Box>
      </Box>
      <Stack spacing={0.5} sx={{ minWidth: 0 }}>
        {segments.map((seg) => (
          <Stack key={seg.status} direction="row" spacing={1} alignItems="center">
            <Box sx={{ width: 10, height: 10, borderRadius: '2px', bgcolor: seg.color, flexShrink: 0 }} />
            <Typography variant="caption" sx={{ fontVariantNumeric: 'tabular-nums' }}>
              <Box component="span" sx={{ fontWeight: 600, mr: 0.5 }}>{seg.count}</Box>
              <Box component="span" sx={{ color: 'text.secondary' }}>{seg.label}</Box>
            </Typography>
          </Stack>
        ))}
      </Stack>
    </Stack>
  );
};

export default HarnessDonut;
