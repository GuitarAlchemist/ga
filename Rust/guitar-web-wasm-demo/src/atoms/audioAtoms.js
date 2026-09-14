import { atom } from 'jotai';

export const audioContextAtom = atom(null);
export const workletNodeAtom = atom(null);
export const isAudioReadyAtom = atom(false);
// Per-type `decay` from rust-engine set_guitar_profile — keep in sync, so the
// slider shows what the engine plays after a guitar-type switch.
export const GUITAR_PROFILE_DECAY = [0.986, 0.9982, 0.9985, 0.9987];
export const decayAtom = atom(GUITAR_PROFILE_DECAY[0]);
export const logAtom = atom([]);

const defaultStrings = [
  { id: 'E2', freq: 82.41, twelveFreq: 164.82 },
  { id: 'A2', freq: 110.0, twelveFreq: 220.0 },
  { id: 'D3', freq: 146.83, twelveFreq: 293.66 },
  { id: 'G3', freq: 196.0, twelveFreq: 392.0 },
  { id: 'B3', freq: 246.94, twelveFreq: 493.88 },
  { id: 'E4', freq: 329.63, twelveFreq: 659.26 },
];

export const stringsAtom = atom(defaultStrings);

