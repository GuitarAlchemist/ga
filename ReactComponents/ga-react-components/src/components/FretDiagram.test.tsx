// FretDiagram.test — covers the two things the component used to get wrong:
//
//  1. String order. The component documents its `frets` array as low-E-first, but every
//     array that reaches it from the backend is high-e-first: `Str` 1 is the highest-pitched
//     string, voicings are generated over `Str.Range(6)`, and `VoicingFilterService` emits
//     `voicing.Positions` in that order. Open C major arrives as `0-1-0-2-3-x`, so the
//     diagram was drawn mirrored left-to-right.
//
//  2. Dropped dots. See fretWindow.test — this file checks the rendered output really does
//     carry one dot per fretted note.

import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import FretDiagram from './FretDiagram';

/** Centre-x of every fretted dot in the rendered diagram, left to right. */
function dotXs(container: HTMLElement): number[] {
  return Array.from(container.querySelectorAll('circle[fill="#333"]'))
    .map(c => Number(c.getAttribute('cx')))
    .sort((a, b) => a - b);
}

function dotCount(container: HTMLElement): number {
  return container.querySelectorAll('circle[fill="#333"]').length;
}

describe('FretDiagram string order', () => {
  // Open C major. Low-to-high: x-3-2-0-1-0. High-to-low (what the API sends): 0-1-0-2-3-x.
  const lowToHigh = [-1, 3, 2, 0, 1, 0];
  const highToLow = [0, 1, 0, 2, 3, -1];

  it('renders the same diagram for both orders when each is labelled correctly', () => {
    const a = render(<FretDiagram chordName="C" frets={lowToHigh} />);
    const b = render(<FretDiagram chordName="C" frets={highToLow} stringOrder="high-to-low" />);

    expect(b.container.innerHTML).toBe(a.container.innerHTML);
  });

  it('mirrors the diagram when a high-to-low array is mislabelled as low-to-high', () => {
    // This is the bug: feeding the API array in without saying so draws C major backwards.
    const correct = render(<FretDiagram chordName="C" frets={highToLow} stringOrder="high-to-low" />);
    const wrong = render(<FretDiagram chordName="C" frets={highToLow} />);

    expect(wrong.container.innerHTML).not.toBe(correct.container.innerHTML);
  });

  it('puts the muted low E on the left for a low-to-high array', () => {
    const { container } = render(<FretDiagram chordName="C" frets={lowToHigh} />);
    const mutes = Array.from(container.querySelectorAll('text'))
      .filter(t => t.textContent === '×')
      .map(t => Number(t.getAttribute('x')));

    expect(mutes).toHaveLength(1);
    // Leftmost string sits at MARGIN_LEFT (22), and the × is drawn 4px to its left.
    expect(mutes[0]).toBe(18);
  });

  it('defaults to low-to-high', () => {
    const explicit = render(<FretDiagram chordName="C" frets={lowToHigh} stringOrder="low-to-high" />);
    const implicit = render(<FretDiagram chordName="C" frets={lowToHigh} />);

    expect(implicit.container.innerHTML).toBe(explicit.container.innerHTML);
  });
});

describe('FretDiagram dot rendering', () => {
  it('draws one dot per fretted note for an open chord', () => {
    const { container } = render(<FretDiagram chordName="C" frets={[-1, 3, 2, 0, 1, 0]} />);
    expect(dotCount(container)).toBe(3);
  });

  // Regression: min fret 2 forced the window to frets 1..5, so fret 6 was silently dropped.
  it('draws the high note of a voicing that used to fall outside the window', () => {
    const frets = [-1, 2, 2, 2, 6, -1];
    const { container } = render(<FretDiagram chordName="X" frets={frets} />);

    expect(dotCount(container)).toBe(4);
    expect(dotXs(container)).toHaveLength(4);
  });

  it('draws every note of a wide-span voicing by growing the grid', () => {
    const frets = [1, -1, -1, -1, 5, 8];
    const { container } = render(<FretDiagram chordName="Wide" frets={frets} />);

    expect(dotCount(container)).toBe(3);

    // The SVG must be tall enough to hold the extra rows.
    const svg = container.querySelector('svg')!;
    expect(Number(svg.getAttribute('height'))).toBeGreaterThan(30 + 5 * 22 + 10);
  });
});
