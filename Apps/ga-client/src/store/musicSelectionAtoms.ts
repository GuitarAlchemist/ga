import { atom } from 'jotai';
import type { ChordInContext, ChordProgressionDefinition } from '../types/music';

export const selectedChordAtom = atom<ChordInContext | null>(null);

export const selectedProgressionAtom = atom<ChordProgressionDefinition | null>(null);

export const highlightedRomanNumeralAtom = atom<string | null>(null);

export const pinnedNotesAtom = atom<string[]>([]);
