import { atom } from 'jotai';

// Musical key state
export interface MusicalKey {
  root: string;  // e.g., "C", "C#", "D", etc.
  mode: 'Major' | 'Minor';
}

export type KeyMode = MusicalKey['mode'];

export const selectedKeyAtom = atom<MusicalKey>({
  root: 'C',
  mode: 'Major',
});

export const selectedKeyNameAtom = atom((get) => {
  const key = get(selectedKeyAtom);
  return `${key.root} ${key.mode}`;
});

export type NavigationLevel = 'key' | 'scale' | 'mode';

export const navigationLevelAtom = atom<NavigationLevel>('key');

export const selectedScaleAtom = atom<string | null>(null);
export const selectedModeAtom = atom<string | null>(null);

export const musicalContextAtom = atom((get) => {
  const level = get(navigationLevelAtom);
  const key = get(selectedKeyAtom);
  const scale = get(selectedScaleAtom);
  const mode = get(selectedModeAtom);

  return {
    level,
    keyName: `${key.root} ${key.mode}`,
    root: key.root,
    scale: scale || (key.mode === 'Major' ? 'Major' : 'Minor'),
    mode: mode || (key.mode === 'Major' ? 'Ionian' : 'Aeolian'),
  };
});

// All available note roots (chromatic scale)
export const NOTE_ROOTS = [
  'C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B',
] as const;

export const NOTE_ROOTS_FLAT = [
  'C', 'Db', 'D', 'Eb', 'E', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'B',
] as const;

export type NoteRoot = (typeof NOTE_ROOTS)[number];

export interface KeyChordFilters {
  onlyNaturallyOccurring: boolean;
  includeBorrowedChords: boolean;
  includeSecondaryDominants: boolean;
  includeSecondaryTwoFive: boolean;
  minCommonality: number;
  limit: number;
}

export const keyChordFiltersAtom = atom<KeyChordFilters>({
  onlyNaturallyOccurring: false,
  includeBorrowedChords: true,
  includeSecondaryDominants: true,
  includeSecondaryTwoFive: true,
  minCommonality: 0.35,
  limit: 24,
});

export type ProgressionCategory =
  | 'Pop'
  | 'Rock'
  | 'Jazz'
  | 'Blues'
  | 'Classical'
  | 'Funk'
  | 'Latin'
  | 'Other';

export const progressionCategoryAtom = atom<ProgressionCategory | 'All'>('All');

export const progressionDifficultyAtom = atom<'Beginner' | 'Intermediate' | 'Advanced' | 'All'>('All');

export const showAIInsightsAtom = atom<boolean>(true);

export const fretboardModeAtom = atom<'shape' | 'scale' | 'progression'>('shape');
