// SceneOptionsLoader — initial scene options: defaults, saved preferences, URL overrides

import { describe, it, expect } from 'vitest';
import { resolveSceneOptions } from './SceneOptionsLoader';
import { SCENE_OPTIONS } from './SceneOptions';

const resolve = (search: string, saved: string | null) => resolveSceneOptions(SCENE_OPTIONS, search, saved);
const defaults = () => resolve('', null);

describe('resolveSceneOptions', () => {
  it('starts from the option defaults', () => {
    const state = defaults();
    for (const opt of SCENE_OPTIONS) expect(state[opt.id]).toBe(opt.default);
    expect(state.skyboxMode).toBe('milky-way');
    expect(state.voicingSplatsMode).toBe('backdrop');
  });

  it('applies saved preferences', () => {
    const saved = JSON.stringify({ ...defaults(), tower: true, bloom: false, skyboxMode: 'jwst-deep-field', voicingSplatsMode: 'solo' });
    const state = resolve('', saved);
    expect(state.tower).toBe(true);
    expect(state.bloom).toBe(false);
    expect(state.skyboxMode).toBe('jwst-deep-field');
    expect(state.voicingSplatsMode).toBe('solo');
  });

  it('lets URL params override a saved full state', () => {
    // Every toggle saves the full state, so tower/weather/skybox/splats are always present.
    const saved = JSON.stringify({ ...defaults(), tower: false, weather: false, skyboxMode: 'milky-way', voicingSplatsMode: 'off' });
    const state = resolve('?tower=1&constellations&weather&skybox=hubble&splats=solo', saved);
    expect(state.tower).toBe(true);
    expect(state.constellations).toBe(true);
    expect(state.weather).toBe(true);
    expect(state.skyboxMode).toBe('hubble-deep-field');
    expect(state.voicingSplatsMode).toBe('solo');
  });

  it('keeps saved values the URL does not mention', () => {
    const saved = JSON.stringify({ ...defaults(), bloom: false });
    const state = resolve('?tower', saved);
    expect(state.bloom).toBe(false);
    expect(state.tower).toBe(true);
  });

  it('drops unknown keys and values of the wrong type', () => {
    const saved = JSON.stringify({ bogus: true, stars: 'no', tower: 1, skyboxMode: 'nebula', voicingSplatsMode: 42 });
    const state = resolve('', saved);
    expect(state).toEqual(defaults());
    expect('bogus' in state).toBe(false);
  });

  it('does not let a saved __proto__ key change the prototype', () => {
    const saved = '{"__proto__": {"polluted": true}, "tower": true}';
    const state = resolve('', saved);
    expect(Object.getPrototypeOf(state)).toBe(Object.prototype);
    expect((state as Record<string, unknown>).polluted).toBeUndefined();
    expect(state.tower).toBe(true);
  });

  it('ignores saved values that are not a JSON object', () => {
    for (const saved of ['not json', 'null', '[true]', '"tower"', '7']) {
      expect(resolve('', saved)).toEqual(defaults());
    }
  });
});
