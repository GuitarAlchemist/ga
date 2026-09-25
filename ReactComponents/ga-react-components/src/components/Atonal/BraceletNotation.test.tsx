// src/components/Atonal/BraceletNotation.test.tsx
// Geometry of the bracelet: each spoke must end on its note's dot, and the
// dashed symmetry axes must be the scale's reflection axes, each drawn once,
// including the axes that fall between two notes.

import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import BraceletNotation from './BraceletNotation';

const SIZE = 200;
const C = SIZE / 2;

function num(el: Element, attr: string): number {
  return Number(el.getAttribute(attr));
}

function renderBracelet(scale: number) {
  const { container } = render(<BraceletNotation scale={scale} size={SIZE} />);
  const lines = [...container.querySelectorAll('line')];
  const spokes = lines.filter(l => !l.hasAttribute('stroke-dasharray'));
  const axes = lines.filter(l => l.hasAttribute('stroke-dasharray'));
  // The first circle is the outline; the next 12 are the pitch-class dots.
  const dots = [...container.querySelectorAll('circle')].slice(1);
  return { spokes, axes, dots };
}

/** Direction of a line through the centre, in degrees in [0, 180). */
function axisDirection(line: Element): number {
  const dx = num(line, 'x2') - num(line, 'x1');
  const dy = num(line, 'y2') - num(line, 'y1');
  const deg = (Math.atan2(dy, dx) * 180) / Math.PI;
  return Math.round(((deg % 180) + 180) % 180) % 180;
}

describe('BraceletNotation', () => {
  it('ends each spoke on its note dot', () => {
    const { spokes, dots } = renderBracelet(0b100010010001); // C E G B
    expect(spokes).toHaveLength(12);
    expect(dots).toHaveLength(12);
    spokes.forEach((spoke, i) => {
      expect(num(spoke, 'x1')).toBeCloseTo(C);
      expect(num(spoke, 'y1')).toBeCloseTo(C);
      expect(num(spoke, 'x2')).toBeCloseTo(num(dots[i], 'cx'));
      expect(num(spoke, 'y2')).toBeCloseTo(num(dots[i], 'cy'));
    });
  });

  it('draws the four axes of a diminished seventh, two of them between notes', () => {
    // C D# F# A = 0, 3, 6, 9: axes through C/F#, D#/A, and between them.
    const { axes } = renderBracelet((1 << 0) | (1 << 3) | (1 << 6) | (1 << 9));
    const directions = axes.map(axisDirection).sort((a, b) => a - b);
    // Pitch class 0 is at the top (screen direction 90°); pitch classes go
    // clockwise 30° apart, so half steps between notes are 15° apart.
    expect(directions).toEqual([0, 45, 90, 135]);
  });

  it('draws the single axis of the major scale once', () => {
    // C major: symmetric about D (and G#), axis through pitch classes 2 and 8.
    const major = [0, 2, 4, 5, 7, 9, 11].reduce((m, pc) => m | (1 << pc), 0);
    const { axes } = renderBracelet(major);
    expect(axes).toHaveLength(1);
    // D is 60° clockwise from the top: screen direction 90° + 60° = 150°.
    expect(axisDirection(axes[0])).toBe(150);
  });

  it('draws one axis per reflection for a single note', () => {
    const { axes } = renderBracelet(1); // C alone: only the axis through C
    expect(axes.map(axisDirection)).toEqual([90]);
  });
});
