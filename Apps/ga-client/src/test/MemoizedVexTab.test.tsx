import { describe, it, expect, beforeAll } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import MemoizedVexTab from '../components/Chat/MemoizedVexTab';
import { parseChatVexTab } from '../components/Chat/vextabChords';

// jsdom has no SVG layout; VexFlow measures fret numbers with getBBox. A width proportional to
// the text is enough to lay the notes out.
beforeAll(() => {
  const proto = SVGElement.prototype as unknown as { getBBox?: () => DOMRect };
  if (!proto.getBBox) {
    proto.getBBox = function (this: SVGElement) {
      return { x: 0, y: 0, width: 7 * (this.textContent?.length ?? 0), height: 10 } as DOMRect;
    };
  }
});

// What PlayableNotationFormatter writes for open C (x-3-2-0-1-0).
const openC = 'tabstave\nnotes :w (3/5.2/4.0/3.1/2.0/1)\n';

describe('parseChatVexTab', () => {
  it('reads a VexTab chord, fret first', () => {
    expect(parseChatVexTab(openC)).toEqual({
      format: 'vextab',
      chords: [
        [
          { str: 5, fret: 3 },
          { str: 4, fret: 2 },
          { str: 3, fret: 0 },
          { str: 2, fret: 1 },
          { str: 1, fret: 0 },
        ],
      ],
    });
  });

  it('reads single notes, durations and bars', () => {
    expect(parseChatVexTab('tabstave notation=true\nnotes :q 5/3 7/3 | :h 5/2')?.chords).toEqual([
      [{ str: 3, fret: 5 }],
      [{ str: 3, fret: 7 }],
      [{ str: 2, fret: 5 }],
    ]);
  });

  it('reads the old token list, string first', () => {
    expect(parseChatVexTab('5/3 4/2 3/0')).toEqual({
      format: 'ga-tokens',
      chords: [[{ str: 5, fret: 3 }], [{ str: 4, fret: 2 }], [{ str: 3, fret: 0 }]],
    });
  });

  it.each([
    ['techniques', 'tabstave\nnotes 5h7/3'],
    ['standard notes', 'tabstave\nnotes :q C/4'],
    ['an annotation', 'tabstave\nnotes :q 5/5 $Am$'],
    ['a string out of range', 'tabstave\nnotes 5/9'],
    ['prose', 'not a tab'],
    ['nothing', '  \n'],
  ])('leaves %s to the text', (_, text) => {
    expect(parseChatVexTab(text)).toBeNull();
  });
});

describe('MemoizedVexTab', () => {
  it('draws the chatbot block with VexFlow instead of printing it', async () => {
    const { container } = render(<MemoizedVexTab content={openC} />);

    await waitFor(() => expect(container.querySelector('[data-renderer="vexflow"] svg')).not.toBeNull());
    expect(screen.queryByText(/notes :w/)).toBeNull();
    // The five fret numbers of the chord are drawn as text in the SVG.
    const frets = Array.from(container.querySelectorAll('svg text')).map((t) => t.textContent);
    expect(frets).toEqual(expect.arrayContaining(['3', '2', '0', '1']));
  });

  it('draws the old token list too', async () => {
    const { container } = render(<MemoizedVexTab content={'6/0 5/2 4/2\n'} />);

    await waitFor(() => expect(container.querySelector('[data-renderer="vexflow"] svg')).not.toBeNull());
    expect(screen.queryByText('6/0 5/2 4/2')).toBeNull();
  });

  it('shows the text when it can not draw it all', async () => {
    const { container } = render(<MemoizedVexTab content={'tabstave\nnotes 5h7/3\n'} />);

    expect(container.querySelector('[data-renderer="text"] pre')?.textContent).toContain('notes 5h7/3');
    expect(container.querySelector('svg')).toBeNull();
  });
});
