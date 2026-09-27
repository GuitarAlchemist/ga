import { useMemo } from 'react';
import type { Theme } from '@mui/material/styles';
import useMediaQuery from '@mui/material/useMediaQuery';
import { darkTheme, theme as lightTheme } from '../theme';

/**
 * The GA theme that matches the OS colour scheme, following it live. Both
 * themes are generated from DESIGN.md (`colors` / `colors-dark`).
 */
export function useOsTheme(): Theme {
  const prefersDark = useMediaQuery('(prefers-color-scheme: dark)', { noSsr: true });
  return useMemo(() => (prefersDark ? darkTheme : lightTheme), [prefersDark]);
}
