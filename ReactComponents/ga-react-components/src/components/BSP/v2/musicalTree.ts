/**
 * Musically-meaningful BSP tree for the Doom Explorer.
 *
 * Each leaf is a real key+mode that a guitarist might play in. Each
 * partition is a real harmonic boundary (circle-of-fifths distance,
 * mode-of-relative, common-tone count). The tree shape is hand-tuned
 * so the floor layout puts harmonically adjacent keys in adjacent
 * rooms — walking forward through a doorway models a real modulation.
 *
 * Why hand-tuned rather than auto-generated: the backend BSP tree is
 * topologically faithful but it doesn't ship to the demo (the live page
 * runs without a backend). The whole point of the rebuild is to make
 * the demo *useful* — so we ship a curated 8-leaf world that teaches
 * something a guitarist actually wants to learn (the ii-V-I orbit and
 * its near neighbours).
 */

import type { BSPNode, BSPRegion, BSPTreeStructureResponse } from '../BSPApiService';

/** Extra musical metadata we attach onto each leaf's region.
 *  Stored under a non-conflicting property so the original BSPRegion
 *  contract is untouched. */
export interface MusicalLeafMeta {
  key: string;              // "C major", "A minor", "D dorian"
  mode: string;             // "Ionian", "Aeolian", "Dorian", ...
  shortName: string;        // "C maj", "A min" — for HUD/minimap
  tonic: string;            // "C", "A", "F#"
  scaleNotes: string[];     // ["C", "D", "E", "F", "G", "A", "B"]
  diatonicRoman: string[];  // ["I", "ii", "iii", "IV", "V", "vi", "vii°"]
  diatonicChords: string[]; // ["C", "Dm", "Em", "F", "G", "Am", "Bdim"]
  /** Names of leaves you can modulate to from here.
   *  Keys = labels shown to the user ("relative minor", "V", "parallel"). */
  neighbours: { label: string; key: string }[];
  /** Hue 0-360 for chromatic colour wheel (circle-of-fifths position). */
  hue: number;
}

export interface MusicalRegion extends BSPRegion {
  music?: MusicalLeafMeta;
}

// ---- Curated key set ---------------------------------------------------
//
// Chosen so the natural BFS layout (depth-3 balanced) keeps neighbours
// adjacent. Left half = the C major family + its closest dominant-side
// neighbours; right half = the parallel minor side + a couple of jazz
// modes (D Dorian, A Mixolydian) that share notes with C major.
//
// Floor cells (depth 3) walking forward (z-) from the player spawn:
//   row back (-z):    [F maj] [C maj]  ‖  [G maj] [D dor]
//   row front (+z):   [D min] [A min]  ‖  [E min] [A mix]

const PCS = ['C','C#','D','D#','E','F','F#','G','G#','A','A#','B'];
const MAJOR_STEPS = [0,2,4,5,7,9,11];          // Ionian
const MINOR_STEPS = [0,2,3,5,7,8,10];          // Aeolian
const DORIAN_STEPS = [0,2,3,5,7,9,10];
const MIXOLYDIAN_STEPS = [0,2,4,5,7,9,10];

function notesFor(tonic: string, steps: number[]): string[] {
  const start = PCS.indexOf(tonic);
  return steps.map((s) => PCS[(start + s) % 12]);
}

const TRIAD_QUALITIES_MAJOR = ['', 'm', 'm', '', '', 'm', 'dim'];
const TRIAD_QUALITIES_MINOR = ['m', 'dim', '', 'm', 'm', '', ''];
const TRIAD_QUALITIES_DORIAN = ['m', 'm', '', '', 'm', 'dim', ''];
const TRIAD_QUALITIES_MIXOLYDIAN = ['', 'm', 'dim', '', 'm', 'm', ''];

const ROMAN_MAJOR = ['I', 'ii', 'iii', 'IV', 'V', 'vi', 'vii°'];
const ROMAN_MINOR = ['i', 'ii°', 'III', 'iv', 'v', 'VI', 'VII'];
const ROMAN_DORIAN = ['i', 'ii', 'III', 'IV', 'v', 'vi°', 'VII'];
const ROMAN_MIXOLYDIAN = ['I', 'ii', 'iii°', 'IV', 'v', 'vi', 'VII'];

function diatonicChords(notes: string[], qualities: string[]): string[] {
  return notes.map((n, i) => `${n}${qualities[i]}`);
}

// Hue on circle of fifths. 0 = C, +30° per fifth clockwise.
function circleOfFifthsHue(tonic: string): number {
  const order = ['C','G','D','A','E','B','F#','C#','G#','D#','A#','F'];
  const idx = order.indexOf(tonic);
  return idx === -1 ? 0 : idx * 30;
}

function buildKey(
  key: string,
  mode: 'Ionian' | 'Aeolian' | 'Dorian' | 'Mixolydian',
  tonic: string,
  shortName: string,
  neighbours: { label: string; key: string }[],
): MusicalLeafMeta {
  const steps =
    mode === 'Ionian' ? MAJOR_STEPS :
    mode === 'Aeolian' ? MINOR_STEPS :
    mode === 'Dorian' ? DORIAN_STEPS : MIXOLYDIAN_STEPS;
  const qualities =
    mode === 'Ionian' ? TRIAD_QUALITIES_MAJOR :
    mode === 'Aeolian' ? TRIAD_QUALITIES_MINOR :
    mode === 'Dorian' ? TRIAD_QUALITIES_DORIAN : TRIAD_QUALITIES_MIXOLYDIAN;
  const roman =
    mode === 'Ionian' ? ROMAN_MAJOR :
    mode === 'Aeolian' ? ROMAN_MINOR :
    mode === 'Dorian' ? ROMAN_DORIAN : ROMAN_MIXOLYDIAN;
  const scaleNotes = notesFor(tonic, steps);

  return {
    key,
    mode,
    shortName,
    tonic,
    scaleNotes,
    diatonicRoman: roman,
    diatonicChords: diatonicChords(scaleNotes, qualities),
    neighbours,
    hue: circleOfFifthsHue(tonic),
  };
}

// Ordering matters: index = floor cell index in the BFS layout.
const KEYS: MusicalLeafMeta[] = [
  buildKey('F major', 'Ionian', 'F', 'F maj', [
    { label: 'V (dominant)', key: 'C major' },
    { label: 'relative minor', key: 'D minor' },
  ]),
  buildKey('C major', 'Ionian', 'C', 'C maj', [
    { label: 'V (dominant)', key: 'G major' },
    { label: 'IV (subdominant)', key: 'F major' },
    { label: 'relative minor', key: 'A minor' },
    { label: 'parallel minor', key: 'A minor' },
  ]),
  buildKey('G major', 'Ionian', 'G', 'G maj', [
    { label: 'V (dominant)', key: 'D dorian' },
    { label: 'IV (subdominant)', key: 'C major' },
    { label: 'relative minor', key: 'E minor' },
  ]),
  buildKey('D dorian', 'Dorian', 'D', 'D dor', [
    { label: 'parent major', key: 'C major' },
    { label: 'common tones', key: 'A minor' },
    { label: 'ii of C maj', key: 'C major' },
  ]),
  buildKey('D minor', 'Aeolian', 'D', 'D min', [
    { label: 'relative major', key: 'F major' },
    { label: 'common tones', key: 'A minor' },
  ]),
  buildKey('A minor', 'Aeolian', 'A', 'A min', [
    { label: 'relative major', key: 'C major' },
    { label: 'parallel major', key: 'A mixolydian' },
    { label: 'iv (subdominant)', key: 'D minor' },
    { label: 'v (dominant)', key: 'E minor' },
  ]),
  buildKey('E minor', 'Aeolian', 'E', 'E min', [
    { label: 'relative major', key: 'G major' },
    { label: 'iv (subdominant)', key: 'A minor' },
  ]),
  buildKey('A mixolydian', 'Mixolydian', 'A', 'A mix', [
    { label: 'parent major', key: 'D dorian' },
    { label: 'parallel minor', key: 'A minor' },
  ]),
];

const PARTITION_STRATEGIES = [
  'CircleOfFifths',          // depth 0: major/dominant side vs minor/modal side
  'TonalCentre',             // depth 1: split by tonic distance
  'ModeFamily',              // depth 2: Ionian vs modal-cousin
];

function leafNode(meta: MusicalLeafMeta, depth: number): BSPNode {
  const region: MusicalRegion = {
    name: meta.shortName,
    tonalityType:
      meta.mode === 'Ionian' ? 'Major' :
      meta.mode === 'Aeolian' ? 'Minor' :
      'Modal',
    tonalCenter: PCS.indexOf(meta.tonic),
    pitchClasses: meta.scaleNotes.map((n) => n.replace('#', 'Sharp')),
    music: meta,
  };
  return {
    region,
    isLeaf: true,
    depth,
    elements: [],
  };
}

function branchNode(left: BSPNode, right: BSPNode, depth: number, strategyIdx: number, label: string): BSPNode {
  return {
    region: {
      name: label,
      tonalityType: 'Internal',
      tonalCenter: 0,
      pitchClasses: [],
    },
    partition: {
      strategy: PARTITION_STRATEGIES[strategyIdx % PARTITION_STRATEGIES.length],
      referencePoint: depth,
      threshold: 0.5,
      normal: [depth % 2, 0, (depth + 1) % 2],
    },
    left,
    right,
    isLeaf: false,
    depth,
    elements: [],
  };
}

/**
 * Build an 8-leaf BSP tree of real keys. Tree shape carefully chosen so
 * the deterministic depth-3 BFS layout places harmonically adjacent
 * leaves in spatially adjacent rooms.
 *
 * Tree (depth-first):
 *
 *   root [CircleOfFifths]
 *   ├── L: major-side [TonalCentre]
 *   │   ├── L: F maj  ┊ C maj          (siblings: I↔IV)
 *   │   └── R: G maj  ┊ D dor          (siblings: V↔modal)
 *   └── R: minor-side [TonalCentre]
 *       ├── L: D min  ┊ A min          (siblings: iv↔i)
 *       └── R: E min  ┊ A mix          (siblings: v↔parallel-modal)
 */
export function buildMusicalTree(): BSPTreeStructureResponse {
  const leaves = KEYS.map((k) => leafNode(k, 3));

  const d2 = [
    branchNode(leaves[0], leaves[1], 2, 2, 'IV ↔ I'),
    branchNode(leaves[2], leaves[3], 2, 2, 'V ↔ modal'),
    branchNode(leaves[4], leaves[5], 2, 2, 'iv ↔ i'),
    branchNode(leaves[6], leaves[7], 2, 2, 'v ↔ parallel-modal'),
  ];

  const d1 = [
    branchNode(d2[0], d2[1], 1, 1, 'major side'),
    branchNode(d2[2], d2[3], 1, 1, 'minor side'),
  ];

  const root = branchNode(d1[0], d1[1], 0, 0, 'major ↔ minor');

  return {
    root,
    nodeCount: 15,
    maxDepth: 3,
    regionCount: 8,
    partitionCount: 7,
  };
}

/**
 * O(K) lookup by display name; used by the "click-to-teleport" feature
 * in the HUD's neighbour-keys card.
 */
export function findKeyByName(name: string): MusicalLeafMeta | null {
  return KEYS.find((k) => k.key === name) ?? null;
}

export function getAllKeys(): MusicalLeafMeta[] {
  return KEYS.slice();
}
