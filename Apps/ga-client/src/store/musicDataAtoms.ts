import { atom } from 'jotai';
import type { KeyNotes, ScaleDegree } from '../types/music';

export const keyNotesAtom = atom<KeyNotes | null>(null);

export const scaleDegreesAtom = atom<ScaleDegree[]>([]);
