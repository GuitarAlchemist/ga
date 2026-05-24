// AuthChip — header pill showing the current Cloudflare Access identity.
//
// Renders one of three states based on useCfIdentity():
//   1. loading           — neutral chip "Checking sign-in…"
//   2. signed in         — success chip "Logged in as {email}" with a
//                          lock-icon glyph; click does nothing (purely
//                          informational, matches GitHub's profile pill)
//   3. not signed in     — warning chip "Sign in" that links to the
//                          Cloudflare Access login URL (which bounces
//                          back to the current page after PIN / OAuth)
//
// Mounted in DevelopmentSection.tsx near the tab strip so the operator
// can spot at a glance whether action buttons will work before clicking
// one. The intent is: "I should never click a Skill Action and be
// surprised by a redirect" — the chip telegraphs the state.
//
// See docs/runbooks/cf-access-dashboard.md for the operator setup that
// makes this chip light up in production.

import React from 'react';
import { Chip, Tooltip } from '@mui/material';
import LockOutlinedIcon from '@mui/icons-material/LockOutlined';
import LockOpenIcon from '@mui/icons-material/LockOpen';
import HourglassEmptyIcon from '@mui/icons-material/HourglassEmpty';
import { useCfIdentity } from '../../hooks/useCfIdentity';

export interface AuthChipProps {
  /** Optional override — useful for storybook / tests. Defaults to the hook. */
  identityOverride?: ReturnType<typeof useCfIdentity>;
  /** If true, render as a smaller compact chip suitable for tight tab strips. */
  compact?: boolean;
}

export const AuthChip: React.FC<AuthChipProps> = ({ identityOverride, compact = true }) => {
  const hook = useCfIdentity();
  const { authed, loading, identity, signInUrl } = identityOverride ?? hook;

  if (loading) {
    return (
      <Tooltip title="Checking Cloudflare Access identity…">
        <Chip
          size={compact ? 'small' : 'medium'}
          icon={<HourglassEmptyIcon fontSize="small" />}
          label="Checking sign-in…"
          variant="outlined"
          color="default"
          data-auth-state="loading"
          sx={{ fontSize: compact ? '0.7rem' : '0.8rem' }}
        />
      </Tooltip>
    );
  }

  if (authed && identity) {
    const isStub = identity.type === 'local-dev-stub';
    const displayName = identity.email ?? identity.name ?? 'authenticated';
    return (
      <Tooltip
        title={
          isStub
            ? 'Local dev — Cloudflare Access stub identity (production would show your operator email)'
            : `Authenticated via Cloudflare Access as ${displayName}. Action buttons are enabled.`
        }
      >
        <Chip
          size={compact ? 'small' : 'medium'}
          icon={<LockOpenIcon fontSize="small" />}
          label={isStub ? `dev (${displayName})` : `Logged in as ${displayName}`}
          color="success"
          variant="outlined"
          data-auth-state="signed-in"
          data-stub={isStub ? 'true' : 'false'}
          sx={{ fontSize: compact ? '0.7rem' : '0.8rem' }}
        />
      </Tooltip>
    );
  }

  // Not signed in — clickable chip that routes through CF Access.
  return (
    <Tooltip title="Sign in via Cloudflare Access to enable harness skill actions and algedonic ack">
      <Chip
        size={compact ? 'small' : 'medium'}
        icon={<LockOutlinedIcon fontSize="small" />}
        label="Sign in"
        color="warning"
        clickable
        component="a"
        href={signInUrl}
        data-auth-state="signed-out"
        sx={{
          fontSize: compact ? '0.7rem' : '0.8rem',
          textDecoration: 'none',
        }}
      />
    </Tooltip>
  );
};

export default AuthChip;
