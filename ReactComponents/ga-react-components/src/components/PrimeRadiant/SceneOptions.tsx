// src/components/PrimeRadiant/SceneOptions.tsx
// Left-side scene options rail — toggle visual features in the 3D scene.

import React, { useState, useCallback } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface SceneOptionsDef {
  id: string;
  label: string;
  icon: string;    // emoji or short text
  default: boolean;
  group: string;
}

export type SkyboxMode = 'milky-way' | 'hubble-deep-field' | 'jwst-deep-field';
export type VoicingSplatsMode = 'off' | 'backdrop' | 'solo';

export interface SceneOptionsState {
  [key: string]: boolean | SkyboxMode | VoicingSplatsMode | undefined;
  skyboxMode?: SkyboxMode;
  voicingSplatsMode?: VoicingSplatsMode;
}

export interface SceneOptionsProps {
  onChange: (options: SceneOptionsState) => void;
}

// ---------------------------------------------------------------------------
// Option definitions
// ---------------------------------------------------------------------------

const OPTIONS: SceneOptionsDef[] = [
  // Cosmic
  { id: 'stars',         label: 'Star Field',         icon: '\u2728', default: true,  group: 'Cosmic' },
  { id: 'nearbyStars',   label: 'Nearby Stars',       icon: '\u22C6', default: true,  group: 'Cosmic' },
  { id: 'laniakeaHud',   label: 'Laniakea HUD',        icon: 'L', default: true,  group: 'Cosmic' },
  { id: 'dust',          label: 'Ambient Dust',       icon: '\u2601', default: true,  group: 'Cosmic' },
  { id: 'milkyway',      label: 'Milky Way',          icon: '\uD83C\uDF0C', default: true,  group: 'Cosmic' },
  { id: 'orbits',        label: 'Orbit Lines',        icon: '\u25EF', default: true,  group: 'Cosmic' },
  { id: 'autorotate',    label: 'Auto Rotate',        icon: '\uD83D\uDD04', default: true,  group: 'Cosmic' },
  // Governance
  { id: 'filaments',     label: 'Filaments',          icon: '\uD83E\uDEB8', default: true,  group: 'Governance' },
  { id: 'constellations',label: 'Constellations',     icon: '\u2B50', default: false, group: 'Governance' },
  { id: 'weather',       label: 'Belief Weather',     icon: '\u26C8',  default: false, group: 'Governance' },
  { id: 'auras',         label: 'Signal Auras',       icon: '\uD83D\uDCA0', default: false, group: 'Governance' },
  // Effects
  { id: 'bloom',         label: 'Bloom',              icon: '\uD83D\uDD06', default: true,  group: 'Effects' },
  { id: 'godray',        label: 'God Rays',           icon: '\u2600', default: true,  group: 'Effects' },
  { id: 'tower',         label: 'Crystal Tower',      icon: '\uD83D\uDDFC', default: false, group: 'Effects' },
  // Cast / Presentation
  { id: 'presentation',  label: 'Presentation Mode',  icon: '\uD83D\uDCFA', default: false, group: 'Cast' },
];

function getDefaults(): SceneOptionsState {
  const state: SceneOptionsState = {};
  for (const opt of OPTIONS) state[opt.id] = opt.default;
  state.skyboxMode = 'milky-way';
  state.voicingSplatsMode = 'backdrop';
  // Check URL params for overrides
  if (typeof window !== 'undefined') {
    const params = new URLSearchParams(window.location.search);
    if (params.has('tower')) state.tower = true;
    if (params.has('constellations')) state.constellations = true;
    if (params.has('weather')) state.weather = true;
    const skybox = params.get('skybox');
    if (skybox === 'hubble' || skybox === 'hubble-deep-field') state.skyboxMode = 'hubble-deep-field';
    if (skybox === 'jwst' || skybox === 'jwt' || skybox === 'webb' || skybox === 'jwst-deep-field') state.skyboxMode = 'jwst-deep-field';
    const splats = params.get('splats');
    if (splats === 'off' || splats === 'backdrop' || splats === 'solo') state.voicingSplatsMode = splats;
  }
  // Check localStorage for saved preferences
  try {
    const saved = localStorage.getItem('prime-radiant-scene-options');
    if (saved) Object.assign(state, JSON.parse(saved));
  } catch { /* ignore */ }
  return state;
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const SceneOptions: React.FC<SceneOptionsProps> = ({ onChange }) => {
  const [options, setOptions] = useState<SceneOptionsState>(getDefaults);
  const [collapsed, setCollapsed] = useState(true);

  const toggle = useCallback((id: string) => {
    setOptions(prev => {
      const next = { ...prev, [id]: !prev[id] };
      // Persist to localStorage
      try { localStorage.setItem('prime-radiant-scene-options', JSON.stringify(next)); } catch { /* */ }
      onChange(next);
      return next;
    });
  }, [onChange]);

  const setSkyboxMode = useCallback((mode: SkyboxMode) => {
    setOptions(prev => {
      const next = { ...prev, skyboxMode: mode };
      try { localStorage.setItem('prime-radiant-scene-options', JSON.stringify(next)); } catch { /* */ }
      onChange(next);
      return next;
    });
  }, [onChange]);

  const setVoicingSplatsMode = useCallback((mode: VoicingSplatsMode) => {
    setOptions(prev => {
      const next = { ...prev, voicingSplatsMode: mode };
      try { localStorage.setItem('prime-radiant-scene-options', JSON.stringify(next)); } catch { /* */ }
      onChange(next);
      return next;
    });
  }, [onChange]);

  // Initial emit
  React.useEffect(() => { onChange(options); }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Group options
  const groups = new Map<string, SceneOptionsDef[]>();
  for (const opt of OPTIONS) {
    const list = groups.get(opt.group) ?? [];
    list.push(opt);
    groups.set(opt.group, list);
  }

  return (
    <div className={`scene-options ${collapsed ? 'scene-options--collapsed' : ''}`}>
      <button
        className="scene-options__toggle"
        onClick={() => setCollapsed(v => !v)}
        title={collapsed ? 'Show scene options' : 'Hide scene options'}
      >
        {collapsed ? '\u2699' : '\u2715'}
      </button>

      {!collapsed && (
        <div className="scene-options__panel">
          <div className="scene-options__title">Scene</div>
          <label className="scene-options__select-row">
            <span className="scene-options__select-label">Skybox</span>
            <select
              className="scene-options__select"
              value={(options.skyboxMode as SkyboxMode) ?? 'milky-way'}
              onChange={e => setSkyboxMode(e.target.value as SkyboxMode)}
            >
              <option value="milky-way">Milky Way</option>
              <option value="hubble-deep-field">Hubble Deep Field</option>
              <option value="jwst-deep-field">JWST Deep Field</option>
            </select>
          </label>
          <div className="scene-options__group">
            <div className="scene-options__group-label">Voicing Cloud</div>
            <div
              className="scene-options__segmented"
              role="radiogroup"
              aria-label="Voicing cloud rendering mode"
              style={{ display: 'flex', gap: 4, padding: '4px 8px' }}
            >
              {(['off', 'backdrop', 'solo'] as const).map(mode => {
                const active = ((options.voicingSplatsMode as VoicingSplatsMode | undefined) ?? 'backdrop') === mode;
                return (
                  <button
                    key={mode}
                    type="button"
                    role="radio"
                    aria-checked={active}
                    className={`scene-options__item ${active ? 'scene-options__item--on' : ''}`}
                    onClick={() => setVoicingSplatsMode(mode)}
                    title={
                      mode === 'off' ? 'Hide voicing splats'
                      : mode === 'backdrop' ? 'Translucent backdrop behind governance graph'
                      : 'Voicing splats only; graph dimmed'
                    }
                    style={{ flex: 1, justifyContent: 'center' }}
                  >
                    <span className="scene-options__label" style={{ textTransform: 'capitalize' }}>{mode}</span>
                  </button>
                );
              })}
            </div>
          </div>
          {[...groups.entries()].map(([group, opts]) => (
            <div key={group} className="scene-options__group">
              <div className="scene-options__group-label">{group}</div>
              {opts.map(opt => (
                <button
                  key={opt.id}
                  className={`scene-options__item ${options[opt.id] ? 'scene-options__item--on' : ''}`}
                  onClick={() => toggle(opt.id)}
                  title={opt.label}
                >
                  <span className="scene-options__icon">{opt.icon}</span>
                  <span className="scene-options__label">{opt.label}</span>
                  <span className={`scene-options__dot ${options[opt.id] ? 'scene-options__dot--on' : ''}`} />
                </button>
              ))}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export { OPTIONS as SCENE_OPTIONS };
