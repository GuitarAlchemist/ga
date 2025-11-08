export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  error?: string | null;
  errorDetails?: string | null;
  metadata?: Record<string, unknown> | null;
}

export interface KeyNotes {
  keyName: string;
  root: string;
  mode: string;
  notes: string[];
  keySignature: number;
  accidentalKind: string;
}

export interface ScaleDegree {
  degree: number;
  romanNumeral: string;
  name: string;
}

export interface MusicalContext {
  level: string;
  name: string;
}

export interface SecondaryDominantInfo {
  targetDegree: number;
  targetChordName: string;
  notation: string;
  description: string;
  isPartOfTwoFive: boolean;
}

export interface ModulationInfo {
  targetKey: string;
  modulationType: string;
  pivotChords: string[];
  description: string;
}

export interface ChordInContext {
  templateName: string;
  root: string;
  contextualName: string;
  scaleDegree: number | null;
  function: string;
  commonality: number;
  isNaturallyOccurring: boolean;
  alternateNames: string[];
  romanNumeral?: string | null;
  functionalDescription?: string | null;
  context?: MusicalContext | null;
  secondaryDominant?: SecondaryDominantInfo | null;
  modulation?: ModulationInfo | null;
  intervals: number[];
  isCentral: boolean;
  isAttractor: boolean;
  centrality: number;
  dynamicalRole?: string | null;
}

export interface ChordProgressionVariation {
  name: string;
  romanNumerals: string[];
  chords: string[];
  context: string;
}

export interface ChordProgressionExample {
  song: string;
  artist: string;
  usage: string;
}

export interface ChordProgressionDefinition {
  name: string;
  description: string;
  romanNumerals: string[];
  category: string;
  difficulty: string;
  function: string[];
  inKey: string;
  chords: string[];
  voiceLeading: string;
  theory: string;
  variations: ChordProgressionVariation[];
  examples: ChordProgressionExample[];
  usedBy: string[];
}
