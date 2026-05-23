// AUTO-GENERATED from /DESIGN.md (sha 93797784). DO NOT EDIT.
// Re-run: `npm run gen:theme` from ReactComponents/ga-react-components.
// Source of truth: DESIGN.md at the repo root.
// Plan: docs/plans/2026-05-23-arch-design-md-adoption-plan.md (Phase 2).
//
// Generator: scripts/gen-theme-from-design.mjs
// Generated: 2026-05-23T20:12:55.533Z

import { createTheme } from '@mui/material/styles';

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: '#1d4ed8' },
    secondary: { main: '#06b6d4' },
    success: { main: '#168a4a' },
    warning: { main: '#b56a00' },
    error: { main: '#c2410c' },
    background: {
      default: '#f6f7f9',
      paper: '#ffffff',
    },
    text: {
      primary: '#172033',
      secondary: '#6c7278',
      disabled: '#9ca3af',
    },
    divider: '#e0e0e0',
  },
  typography: {
    fontFamily: "Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
    h1: { fontSize: "28px", fontWeight: 700, lineHeight: 1.2 },
    h2: { fontSize: "1.5rem", fontWeight: 700, lineHeight: 1.25 },
    h3: { fontSize: "1.25rem", fontWeight: 600, lineHeight: 1.3 },
    h6: { fontSize: "1rem", fontWeight: 600, lineHeight: 1.4 },
    body1: { fontSize: "1rem", fontWeight: 400, lineHeight: 1.5 },
    body2: { fontSize: "0.875rem", fontWeight: 400, lineHeight: 1.5 },
    caption: { fontSize: "0.75rem", fontWeight: 400, lineHeight: 1.4 },
  },
  shape: {
    // MUI accepts a single borderRadius — use the spec's 'md' as the canonical
    // card radius. Components needing other radii can read from designTokens.
    borderRadius: 6,
  },
  spacing: 8,  // base unit; sx={{ p: 2 }} => 16px === DESIGN.md spacing.md
});

// Exposed for components that need design tokens MUI's theme doesn't model
// (border, surface-alt, primary-soft, rounded.pill / xl, elevation, etc.).
// Read these as designTokens.border, designTokens.surface-alt, etc.
export const designTokens = {
  "name": "Guitar Alchemist",
  "colors": {
    "primary": "#1d4ed8",
    "primary-soft": "#1f6feb",
    "secondary": "#06b6d4",
    "success": "#168a4a",
    "warning": "#b56a00",
    "error": "#c2410c",
    "neutral": "#f6f7f9",
    "surface": "#ffffff",
    "surface-alt": "#f1f5f9",
    "text-primary": "#172033",
    "text-secondary": "#6c7278",
    "text-disabled": "#9ca3af",
    "border": "#d7dce5",
    "divider": "#e0e0e0"
  },
  "typography": {
    "fontFamily": "Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
    "fontFamily-mono": "ui-monospace, SFMono-Regular, 'Cascadia Code', Consolas, monospace",
    "h1": {
      "fontFamily": "Inter",
      "fontSize": "28px",
      "fontWeight": 700,
      "lineHeight": 1.2
    },
    "h2": {
      "fontFamily": "Inter",
      "fontSize": "1.5rem",
      "fontWeight": 700,
      "lineHeight": 1.25
    },
    "h3": {
      "fontFamily": "Inter",
      "fontSize": "1.25rem",
      "fontWeight": 600,
      "lineHeight": 1.3
    },
    "h6": {
      "fontFamily": "Inter",
      "fontSize": "1rem",
      "fontWeight": 600,
      "lineHeight": 1.4
    },
    "body": {
      "fontFamily": "Inter",
      "fontSize": "1rem",
      "fontWeight": 400,
      "lineHeight": 1.5
    },
    "body-sm": {
      "fontFamily": "Inter",
      "fontSize": "0.875rem",
      "fontWeight": 400,
      "lineHeight": 1.5
    },
    "caption": {
      "fontFamily": "Inter",
      "fontSize": "0.75rem",
      "fontWeight": 400,
      "lineHeight": 1.4
    },
    "code": {
      "fontFamily": "mono",
      "fontSize": "0.85em"
    }
  },
  "spacing": {
    "xs": "4px",
    "sm": "8px",
    "md": "16px",
    "lg": "24px",
    "xl": "32px",
    "xxl": "48px"
  },
  "rounded": {
    "none": "0",
    "sm": "4px",
    "md": "6px",
    "lg": "8px",
    "xl": "12px",
    "pill": "999px"
  },
  "elevation": {
    "card": "0 1px 3px rgba(23, 32, 51, 0.06), 0 1px 2px rgba(23, 32, 51, 0.04)",
    "hero": "0 8px 24px rgba(29, 78, 216, 0.18)"
  }
} as const;
