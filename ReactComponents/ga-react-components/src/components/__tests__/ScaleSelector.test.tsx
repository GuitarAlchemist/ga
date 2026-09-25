// src/components/__tests__/ScaleSelector.test.tsx
// NotesSelector reports its notes to ScaleSelector, which reports them to its
// parent. The report must settle after one round instead of looping: a fresh
// handler on every render re-ran NotesSelector's effect, which stored a fresh
// array, which re-rendered ScaleSelector with a fresh handler...

import { describe, it, expect, vi } from 'vitest';
import { Profiler } from 'react';
import { render, screen, fireEvent, act } from '@testing-library/react';

// The diagrams are irrelevant here; keep the barrel's 3D components out.
vi.mock('../index.ts', () => ({
  BraceletNotation: () => null,
  KeyboardDiagram: () => null,
}));

import ScaleSelector from '../ScaleSelector';

const LIMIT = 20;

// A parent callback that stops a runaway loop instead of hanging the test
// (inside act(), an effect loop never yields).
function makeOnNotesChange() {
  const fn = vi.fn((_notes: string[]) => {
    if (fn.mock.calls.length > LIMIT) {
      throw new Error(`onNotesChange called more than ${LIMIT} times: the report does not settle`);
    }
  });
  return fn;
}

async function flush() {
  await act(async () => {
    await new Promise(resolve => setTimeout(resolve, 20));
  });
}

describe('ScaleSelector / NotesSelector', () => {
  it('settles after mounting', async () => {
    const onNotesChange = makeOnNotesChange();
    let commits = 0;
    render(
      <Profiler id="scale" onRender={() => { commits += 1; }}>
        <ScaleSelector onNotesChange={onNotesChange} />
      </Profiler>,
    );
    await flush();

    expect(onNotesChange.mock.calls.length).toBeLessThanOrEqual(2);
    expect(onNotesChange).toHaveBeenLastCalledWith([]);
    // No further commits once settled.
    const settled = commits;
    await flush();
    expect(commits).toBe(settled);
  });

  it('reports typed notes and settles', async () => {
    const onNotesChange = makeOnNotesChange();
    render(<ScaleSelector onNotesChange={onNotesChange} />);
    await flush();
    const before = onNotesChange.mock.calls.length;

    fireEvent.change(screen.getByPlaceholderText(/enter notes/i), { target: { value: 'C E G' } });
    await flush();

    expect(onNotesChange).toHaveBeenLastCalledWith(['C', 'E', 'G']);
    expect(onNotesChange.mock.calls.length - before).toBe(1);
  });
});
