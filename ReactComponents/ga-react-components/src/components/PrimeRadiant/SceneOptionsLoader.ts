// src/components/PrimeRadiant/SceneOptionsLoader.ts
// Initial scene options state — kept out of SceneOptions.tsx so that file
// only exports components (React fast refresh).

import type { SceneOptionsDef, SceneOptionsState, SkyboxMode, VoicingSplatsMode } from './SceneOptions';

const SKYBOX_MODES: readonly string[] = ['milky-way', 'hubble-deep-field', 'jwst-deep-field'];
const VOICING_SPLATS_MODES: readonly string[] = ['off', 'backdrop', 'solo'];

/**
 * Initial scene options: defaults, then saved preferences (the JSON stored in
 * localStorage), then URL params, which are explicit overrides and win.
 * Only known keys with a valid value are taken from the saved JSON.
 */
export function resolveSceneOptions(
  options: readonly SceneOptionsDef[],
  search: string,
  saved: string | null,
): SceneOptionsState {
  const state: SceneOptionsState = {};
  for (const opt of options) state[opt.id] = opt.default;
  state.skyboxMode = 'milky-way';
  state.voicingSplatsMode = 'backdrop';
  // Saved preferences
  if (saved) {
    let parsed: unknown;
    try { parsed = JSON.parse(saved); } catch { /* ignore */ }
    if (parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed)) {
      const record = parsed as Record<string, unknown>;
      for (const opt of options) {
        const value = record[opt.id];
        if (typeof value === 'boolean') state[opt.id] = value;
      }
      if (typeof record.skyboxMode === 'string' && SKYBOX_MODES.includes(record.skyboxMode)) {
        state.skyboxMode = record.skyboxMode as SkyboxMode;
      }
      if (typeof record.voicingSplatsMode === 'string' && VOICING_SPLATS_MODES.includes(record.voicingSplatsMode)) {
        state.voicingSplatsMode = record.voicingSplatsMode as VoicingSplatsMode;
      }
    }
  }
  // URL params override saved preferences
  const params = new URLSearchParams(search);
  if (params.has('tower')) state.tower = true;
  if (params.has('constellations')) state.constellations = true;
  if (params.has('weather')) state.weather = true;
  const skybox = params.get('skybox');
  if (skybox === 'hubble' || skybox === 'hubble-deep-field') state.skyboxMode = 'hubble-deep-field';
  if (skybox === 'jwst' || skybox === 'jwt' || skybox === 'webb' || skybox === 'jwst-deep-field') state.skyboxMode = 'jwst-deep-field';
  const splats = params.get('splats');
  if (splats === 'off' || splats === 'backdrop' || splats === 'solo') state.voicingSplatsMode = splats;
  return state;
}
