// HarnessItemCard — one card per harness item. Replaces the dense
// rollout table from the previous design. Each card carries:
//
//   * Header: number, status icon, title, expand chevron
//   * Body: category + effort + impact chips, full notes (no truncation)
//   * Footer: PR link, owner, merge date, optional skill action button
//
// Collapsible: default expanded for the first few cards, but the user can
// collapse to scan or expand all. The card's outer Box has id
// "harness-item-<n>" so the timeline pills can scrollIntoView to it.

import React, { useState } from 'react';
import {
  Box,
  Chip,
  Collapse,
  IconButton,
  Link as MuiLink,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import OpenInNewIcon from '@mui/icons-material/OpenInNew';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked';
import RocketLaunchIcon from '@mui/icons-material/RocketLaunch';
import type { HarnessItem, HarnessStatus } from './types';
import { statusMeta } from './types';
import { SkillActionButton } from './SkillActionButton';

interface Props {
  item: HarnessItem;
  defaultExpanded?: boolean;
}

function statusIcon(s: HarnessStatus): React.ReactNode {
  const color = statusMeta(s).color;
  const sx = { fontSize: 20, color };
  switch (s) {
    case 'shipped': return <CheckCircleIcon sx={sx} />;
    case 'ready-for-install': return <RocketLaunchIcon sx={sx} />;
    case 'in_flight': return <HourglassEmptyIcon sx={sx} />;
    case 'todo':
    default: return <RadioButtonUncheckedIcon sx={sx} />;
  }
}

function formatMergedDate(iso?: string): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

export const HarnessItemCard: React.FC<Props> = ({ item, defaultExpanded = false }) => {
  const [expanded, setExpanded] = useState(defaultExpanded);
  const meta = statusMeta(item.status);
  const merged = formatMergedDate(item.merged_at);

  const headerClick = () => setExpanded((e) => !e);

  return (
    <Paper
      id={`harness-item-${item.number}`}
      variant="outlined"
      sx={{
        scrollMarginTop: 80,
        borderLeft: 4,
        borderLeftColor: meta.color,
        transition: 'box-shadow 160ms ease, transform 160ms ease',
        '&:hover': { boxShadow: 3 },
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
      }}
    >
      {/* Header — always visible, click to toggle */}
      <Box
        role="button"
        aria-expanded={expanded}
        aria-controls={`harness-item-${item.number}-body`}
        tabIndex={0}
        onClick={headerClick}
        onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); headerClick(); } }}
        sx={{
          p: 1.5,
          cursor: 'pointer',
          display: 'flex',
          alignItems: 'center',
          gap: 1,
        }}
      >
        {statusIcon(item.status)}
        <Box
          component="span"
          sx={{
            fontFamily: 'monospace',
            fontWeight: 700,
            fontSize: '0.75rem',
            color: 'text.secondary',
            minWidth: 22,
          }}
        >
          #{item.number}
        </Box>
        <Typography
          variant="subtitle2"
          sx={{
            fontWeight: 600,
            flex: 1,
            minWidth: 0,
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            whiteSpace: expanded ? 'normal' : 'nowrap',
          }}
        >
          {item.title}
        </Typography>
        <IconButton size="small" aria-label={expanded ? 'collapse' : 'expand'}>
          {expanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
        </IconButton>
      </Box>

      {/* Always-visible chip row, just below the header */}
      <Stack direction="row" spacing={0.5} sx={{ px: 1.5, pb: 1 }} flexWrap="wrap" useFlexGap>
        <Chip label={meta.label} size="small" color={meta.muiColor} sx={{ fontSize: '0.7rem', height: 20 }} />
        <Chip label={`effort ${item.effort}`} size="small" variant="outlined" sx={{ fontSize: '0.7rem', height: 20 }} />
        <Chip
          label={`impact ${item.impact}`}
          size="small"
          color={item.impact === 'H' ? 'primary' : 'default'}
          variant={item.impact === 'H' ? 'filled' : 'outlined'}
          sx={{ fontSize: '0.7rem', height: 20 }}
        />
        <Chip label={item.category} size="small" variant="outlined" sx={{ fontSize: '0.7rem', height: 20, color: 'text.secondary' }} />
      </Stack>

      <Collapse in={expanded} timeout="auto" unmountOnExit={false}>
        <Box id={`harness-item-${item.number}-body`} sx={{ px: 1.5, pb: 1 }}>
          {item.screenshot_url && (
            <Box
              component="img"
              src={item.screenshot_url}
              alt={`Screenshot for ${item.title}`}
              loading="lazy"
              sx={{
                width: '100%',
                maxHeight: 200,
                objectFit: 'cover',
                borderRadius: 1,
                mb: 1,
                bgcolor: 'action.hover',
              }}
            />
          )}
          {item.notes && (
            <Typography
              variant="body2"
              color="text.secondary"
              sx={{ whiteSpace: 'pre-wrap', mb: 1, lineHeight: 1.55 }}
            >
              {item.notes}
            </Typography>
          )}
        </Box>
      </Collapse>

      {/* Footer — always visible */}
      <Box sx={{ borderTop: 1, borderColor: 'divider', px: 1.5, py: 1, mt: 'auto' }}>
        <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
          {item.pr_number ? (
            <MuiLink
              href={item.evidence_url ?? `https://github.com/GuitarAlchemist/ga/pull/${item.pr_number}`}
              target="_blank"
              rel="noopener noreferrer"
              onClick={(e) => e.stopPropagation()}
              sx={{ fontFamily: 'monospace', fontSize: '0.78rem', display: 'inline-flex', alignItems: 'center', gap: 0.25 }}
            >
              PR #{item.pr_number} <OpenInNewIcon sx={{ fontSize: 12 }} />
            </MuiLink>
          ) : (
            <Typography variant="caption" color="text.disabled">no PR</Typography>
          )}
          {merged && (
            <Typography variant="caption" color="text.secondary" sx={{ fontVariantNumeric: 'tabular-nums' }}>
              · merged {merged}
            </Typography>
          )}
          {item.merge_sha && (
            <Typography
              variant="caption"
              color="text.disabled"
              sx={{ fontFamily: 'monospace', fontSize: '0.7rem' }}
              title={item.merge_sha}
            >
              {item.merge_sha.slice(0, 7)}
            </Typography>
          )}
          <Box sx={{ flex: 1 }} />
          {item.skill && (
            <SkillActionButton
              skill={item.skill}
              context={`harness #${item.number}: ${item.title}`}
              itemNumber={item.number}
              tooltip={`Queue /${item.skill} for an agent to pick up`}
            />
          )}
        </Stack>
        {item.owner && (
          <Typography variant="caption" color="text.disabled" sx={{ display: 'block', mt: 0.5 }}>
            {item.owner}
          </Typography>
        )}
      </Box>
    </Paper>
  );
};

export default HarnessItemCard;
