// SkillActionButton — POSTs to /dev-data/harness/skill/<name> to queue
// a skill invocation. The dev-data middleware appends a line to
// state/harness/skill-invocations.jsonl (gitignored). The button itself
// doesn't execute the skill — agents read the queue on their own beat.
//
// UX: shows a snackbar/alert on success or failure. Disabled while in
// flight to prevent double-click duplicate queue entries.

import React, { useState } from 'react';
import { Alert, Button, ButtonProps, Snackbar, Tooltip } from '@mui/material';
import PlayArrowIcon from '@mui/icons-material/PlayArrow';

interface Props {
  skill: string;
  /** Optional label override; defaults to `/${skill}`. */
  label?: string;
  /** Optional context string (item title etc.) sent in POST body for traceability. */
  context?: string;
  /** Optional harness item number for cross-reference in the queue file. */
  itemNumber?: number;
  /** Style overrides. */
  variant?: ButtonProps['variant'];
  size?: ButtonProps['size'];
  /** A short tooltip explaining what queueing means. */
  tooltip?: string;
}

export const SkillActionButton: React.FC<Props> = ({
  skill,
  label,
  context,
  itemNumber,
  variant = 'outlined',
  size = 'small',
  tooltip,
}) => {
  const [busy, setBusy] = useState(false);
  const [snack, setSnack] = useState<{ severity: 'success' | 'error' | 'info'; message: string } | null>(null);

  const handleClick = async (e: React.MouseEvent) => {
    e.stopPropagation();
    setBusy(true);
    try {
      const resp = await fetch(`/dev-data/harness/skill/${encodeURIComponent(skill)}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          source: 'harness-tab',
          context: context ?? null,
          item_number: itemNumber ?? null,
        }),
      });
      const body = await resp.json().catch(() => ({}));
      if (!resp.ok) {
        setSnack({
          severity: 'error',
          message: `Queue failed (HTTP ${resp.status}): ${body?.error ?? 'unknown'}`,
        });
      } else {
        setSnack({
          severity: 'success',
          message: body?.message ?? `Queued /${skill} — agents will pick this up.`,
        });
      }
    } catch (err) {
      setSnack({
        severity: 'error',
        message: `Network error queueing /${skill}: ${String((err as Error).message ?? err)}`,
      });
    } finally {
      setBusy(false);
    }
  };

  const button = (
    <Button
      size={size}
      variant={variant}
      startIcon={<PlayArrowIcon sx={{ fontSize: 16 }} />}
      onClick={handleClick}
      disabled={busy}
      sx={{ textTransform: 'none', fontSize: '0.75rem', py: 0.25 }}
    >
      {label ?? `/${skill}`}
    </Button>
  );

  return (
    <>
      {tooltip ? <Tooltip title={tooltip} arrow>{button}</Tooltip> : button}
      <Snackbar
        open={snack != null}
        autoHideDuration={5000}
        onClose={() => setSnack(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        {snack ? (
          <Alert
            severity={snack.severity}
            variant="filled"
            onClose={() => setSnack(null)}
            sx={{ width: '100%', maxWidth: 520 }}
          >
            {snack.message}
          </Alert>
        ) : undefined}
      </Snackbar>
    </>
  );
};

export default SkillActionButton;
