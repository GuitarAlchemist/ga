/**
 * Domain types mirroring C# ChordInContext record.
 * Quality and roman numeral come from the backend — no string parsing heuristics.
 */
export interface ChordInContext {
  readonly templateName: string;
  readonly root: string;
  readonly contextualName: string;
  readonly scaleDegree: number | null;
  /** e.g. "Tonic" | "Subdominant" | "Dominant" | "LeadingTone" */
  readonly function: string;
  readonly commonality: number;
  readonly isNaturallyOccurring: boolean;
  readonly alternateNames: readonly string[];
  readonly notes: readonly string[];
  /** "I" | "ii" | "iii" | … — from backend, no heuristics */
  readonly romanNumeral: string | null;
  readonly functionalDescription: string | null;
}

export interface CandidateVoicing {
  readonly id: string;
  readonly displayName: string;
  readonly shape: string;
  readonly score: number;
}

/** Scale degree descriptor from the ga:scale custom event. */
export interface ScaleNote {
  readonly degree: number;
  readonly note: string;
  readonly pitchClass: number;
}

/** Live agent state maintained by useGAAgent. */
export interface GaAgentState {
  readonly key: string | null;
  readonly mode: string | null;
  readonly diatonicChords: readonly ChordInContext[];
  readonly candidates: readonly CandidateVoicing[];
  readonly progression: readonly unknown[];
  readonly analysisPhase: 'idle' | 'identifying' | 'complete';
  readonly lastError: string | null;
  /** Scale notes from the ga:scale custom event — used for live fretboard overlay. */
  readonly scaleNotes: readonly ScaleNote[];
}

export const EMPTY_GA_STATE: GaAgentState = {
  key:            null,
  mode:           null,
  diatonicChords: [],
  candidates:     [],
  progression:    [],
  analysisPhase:  'idle',
  lastError:      null,
  scaleNotes:     [],
};
