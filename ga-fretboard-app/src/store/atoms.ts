import { atom } from 'jotai';

/**
 * Atom for controlling the navigation drawer open/closed state
 */
export const drawerOpenAtom = atom<boolean>(true);

/**
 * Atom for the currently selected menu item
 */
export const selectedMenuItemAtom = atom<string>('fretboard');

