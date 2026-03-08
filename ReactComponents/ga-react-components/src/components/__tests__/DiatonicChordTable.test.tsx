import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import DiatonicChordTable from '../DiatonicChordTable';
import type { ChordInContext } from '../../types/agent-state';

// G major diatonic set (7 chords)
const G_MAJOR_CHORDS: ChordInContext[] = [
  {
    templateName: 'Major',
    root: 'G',
    contextualName: 'G',
    scaleDegree: 1,
    function: 'Tonic',
    commonality: 1.0,
    isNaturallyOccurring: true,
    alternateNames: [],
    notes: ['G', 'B', 'D'],
    romanNumeral: 'I',
    functionalDescription: 'Tonic',
  },
  {
    templateName: 'Minor',
    root: 'A',
    contextualName: 'Am',
    scaleDegree: 2,
    function: 'Subdominant',
    commonality: 0.9,
    isNaturallyOccurring: true,
    alternateNames: [],
    notes: ['A', 'C', 'E'],
    romanNumeral: 'ii',
    functionalDescription: 'Supertonic',
  },
  {
    templateName: 'Minor',
    root: 'B',
    contextualName: 'Bm',
    scaleDegree: 3,
    function: 'Tonic',
    commonality: 0.8,
    isNaturallyOccurring: true,
    alternateNames: [],
    notes: ['B', 'D', 'F#'],
    romanNumeral: 'iii',
    functionalDescription: 'Mediant',
  },
  {
    templateName: 'Major',
    root: 'C',
    contextualName: 'C',
    scaleDegree: 4,
    function: 'Subdominant',
    commonality: 0.95,
    isNaturallyOccurring: true,
    alternateNames: [],
    notes: ['C', 'E', 'G'],
    romanNumeral: 'IV',
    functionalDescription: 'Subdominant',
  },
  {
    templateName: 'Major',
    root: 'D',
    contextualName: 'D',
    scaleDegree: 5,
    function: 'Dominant',
    commonality: 1.0,
    isNaturallyOccurring: true,
    alternateNames: [],
    notes: ['D', 'F#', 'A'],
    romanNumeral: 'V',
    functionalDescription: 'Dominant',
  },
  {
    templateName: 'Minor',
    root: 'E',
    contextualName: 'Em',
    scaleDegree: 6,
    function: 'Tonic',
    commonality: 0.9,
    isNaturallyOccurring: true,
    alternateNames: [],
    notes: ['E', 'G', 'B'],
    romanNumeral: 'vi',
    functionalDescription: 'Relative minor',
  },
  {
    templateName: 'Diminished',
    root: 'F#',
    contextualName: 'F#dim',
    scaleDegree: 7,
    function: 'LeadingTone',
    commonality: 0.5,
    isNaturallyOccurring: true,
    alternateNames: [],
    notes: ['F#', 'A', 'C'],
    romanNumeral: 'vii°',
    functionalDescription: 'Leading tone',
  },
];

describe('DiatonicChordTable', () => {
  it('renders null when chords array is empty', () => {
    const { container } = render(<DiatonicChordTable chords={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders all 7 chords', () => {
    render(<DiatonicChordTable chords={G_MAJOR_CHORDS} />);
    // Each chord renders its contextualName — check all 7 appear
    expect(screen.getByText('G')).toBeInTheDocument();
    expect(screen.getByText('Am')).toBeInTheDocument();
    expect(screen.getByText('Bm')).toBeInTheDocument();
    expect(screen.getByText('C')).toBeInTheDocument();
    expect(screen.getByText('D')).toBeInTheDocument();
    expect(screen.getByText('Em')).toBeInTheDocument();
    expect(screen.getByText('F#dim')).toBeInTheDocument();
  });

  it('displays roman numeral for each chord', () => {
    render(<DiatonicChordTable chords={G_MAJOR_CHORDS} />);
    expect(screen.getByText('I')).toBeInTheDocument();
    expect(screen.getByText('ii')).toBeInTheDocument();
    expect(screen.getByText('iii')).toBeInTheDocument();
    expect(screen.getByText('IV')).toBeInTheDocument();
    expect(screen.getByText('V')).toBeInTheDocument();
    expect(screen.getByText('vi')).toBeInTheDocument();
    expect(screen.getByText('vii°')).toBeInTheDocument();
  });

  it('displays contextual name for each chord', () => {
    render(<DiatonicChordTable chords={G_MAJOR_CHORDS} />);
    const contextualNames = ['G', 'Am', 'Bm', 'C', 'D', 'Em', 'F#dim'];
    for (const name of contextualNames) {
      expect(screen.getByText(name)).toBeInTheDocument();
    }
  });

  it('calls onChordClick with the correct chord when clicked', async () => {
    const user = userEvent.setup();
    const onChordClick = vi.fn();
    render(<DiatonicChordTable chords={G_MAJOR_CHORDS} onChordClick={onChordClick} />);

    // Click the "D" chord (index 4, Dominant)
    await user.click(screen.getByText('D'));
    expect(onChordClick).toHaveBeenCalledOnce();
    expect(onChordClick).toHaveBeenCalledWith(G_MAJOR_CHORDS[4]);
  });

  it('falls back to scaleDegree when romanNumeral is null', () => {
    const chordWithoutRoman: ChordInContext = {
      ...G_MAJOR_CHORDS[0],
      romanNumeral: null,
      scaleDegree: 1,
    };
    render(<DiatonicChordTable chords={[chordWithoutRoman]} />);
    // Should show scaleDegree "1" instead of roman numeral
    expect(screen.getByText('1')).toBeInTheDocument();
  });

  it('falls back to em dash when both romanNumeral and scaleDegree are null', () => {
    const chordWithoutBoth: ChordInContext = {
      ...G_MAJOR_CHORDS[0],
      romanNumeral: null,
      scaleDegree: null,
    };
    render(<DiatonicChordTable chords={[chordWithoutBoth]} />);
    expect(screen.getByText('—')).toBeInTheDocument();
  });
});
