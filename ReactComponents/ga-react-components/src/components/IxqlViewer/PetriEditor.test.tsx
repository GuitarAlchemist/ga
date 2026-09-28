import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { PetriEditor } from './PetriEditor';

beforeAll(() => {
  // React Flow measures its pane; jsdom has no ResizeObserver.
  globalThis.ResizeObserver ??= class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver;
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('PetriEditor', () => {
  it('lets a new net be analysed while an analysis of the old one is still running', () => {
    // An analysis that never answers, as a slow ix-mcp would look.
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})));
    render(<PetriEditor />);
    fireEvent.click(screen.getByRole('button', { name: 'Pipeline lifecycle (L14)' }));
    fireEvent.click(screen.getByRole('button', { name: /Analyse/ }));
    expect(screen.getByRole('button', { name: /Analysing/ })).toBeDisabled();

    // A new net drops the old analysis, which can no longer clear the flag.
    fireEvent.click(screen.getByRole('button', { name: 'Bounded buffer (L1)' }));
    expect(screen.getByRole('button', { name: /Analyse \(ix_petri_analyze\)/ })).toBeEnabled();
  });
});
