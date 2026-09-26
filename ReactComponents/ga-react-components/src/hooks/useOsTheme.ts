import { useMemo } from 'react';
import { createTheme, type Theme } from '@mui/material/styles';
import useMediaQuery from '@mui/material/useMediaQuery';
import { theme as lightTheme } from '../theme';

// DESIGN.md defines light tokens only ("Dark mode: not yet defined"), so the
// dark palette lives here until it gets dark counterparts there. It keeps the
// generated theme's typography, shape and spacing; only colours change.
export const darkTheme: Theme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#58a6ff' },
    secondary: { main: '#22d3ee' },
    success: { main: '#3fb950' },
    warning: { main: '#d29922' },
    error: { main: '#ff7b72' },
    background: { default: '#0f141b', paper: '#161b22' },
    text: { primary: '#e6edf3', secondary: '#9da7b3', disabled: '#6e7681' },
    divider: '#30363d',
  },
  typography: { fontFamily: lightTheme.typography.fontFamily },
  shape: lightTheme.shape,
  spacing: 8,
});

/** The GA theme that matches the OS colour scheme, following it live. */
export function useOsTheme(): Theme {
  const prefersDark = useMediaQuery('(prefers-color-scheme: dark)', { noSsr: true });
  return useMemo(() => (prefersDark ? darkTheme : lightTheme), [prefersDark]);
}
