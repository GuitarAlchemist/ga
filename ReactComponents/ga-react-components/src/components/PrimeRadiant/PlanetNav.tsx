// src/components/PrimeRadiant/PlanetNav.tsx
// Collapsible planet navigation + GIS layer toggles — left-side overlay

import React, { useState, useCallback, useEffect, useRef } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
export interface PlanetNavProps {
  onNavigateToPlanet: (target: string) => void;
  onLoadArcGIS?: (layer: string) => void;
  onRemoveArcGIS?: (layer: string) => void;
  onResetView?: () => void;
  onLaunchGodot?: () => void;
  onLaunchLunarLander?: () => void;
}

const PLANETS = [
  { icon: '\u2604', name: 'Demerzel', target: 'demerzel-head' },
  { icon: '\u2600', name: 'Sun', target: 'sun' },
  { icon: '\u25CF', name: 'Mercury', target: 'mercury', color: '#9e9e9e' },
  { icon: '\u25CF', name: 'Venus', target: 'venus', color: '#e3d500' },
  { icon: '\u25CF', name: 'Earth', target: 'earth', color: '#4d88ff' },
  { icon: '\u263D', name: 'Moon', target: 'moon', color: '#cccccc' },
  { icon: '\u25CF', name: 'Mars', target: 'mars', color: '#ff4422' },
  { icon: '\u25CF', name: 'Jupiter', target: 'jupiter', color: '#ffaa77' },
  { icon: '\u25CF', name: 'Saturn', target: 'saturn', color: '#ffeecc' },
  { icon: '\u25CF', name: 'Uranus', target: 'uranus', color: '#88ccdd' },
  { icon: '\u25CF', name: 'Neptune', target: 'neptune', color: '#4444cc' },
] as const;

const GIS_LAYERS = [
  { id: 'borders', label: 'Borders' },
  { id: 'imagery', label: 'Satellite' },
  { id: 'streets', label: 'Streets' },
  { id: 'topo', label: 'Topo' },
  { id: 'darkgray', label: 'Dark' },
] as const;

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const PlanetNav: React.FC<PlanetNavProps> = ({ onNavigateToPlanet, onLoadArcGIS, onRemoveArcGIS, onResetView, onLaunchGodot, onLaunchLunarLander }) => {
  const [expanded, setExpanded] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // Close panel when clicking outside
  useEffect(() => {
    if (!expanded) return;
    const handler = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setExpanded(false);
      }
    };
    // Delay to avoid closing on the toggle click itself
    const timer = setTimeout(() => document.addEventListener('mousedown', handler), 50);
    return () => { clearTimeout(timer); document.removeEventListener('mousedown', handler); };
  }, [expanded]);
  const [showGIS, setShowGIS] = useState(false);
  const [activeLayers, setActiveLayers] = useState<Set<string>>(new Set());
  const [gisLoading, setGisLoading] = useState<string | null>(null);

  const handlePlanet = useCallback((target: string) => {
    onNavigateToPlanet(target);
  }, [onNavigateToPlanet]);

  const handleGISToggle = useCallback((layerId: string) => {
    if (activeLayers.has(layerId)) {
      // Remove layer
      onRemoveArcGIS?.(layerId);
      setActiveLayers(prev => { const next = new Set(prev); next.delete(layerId); return next; });
    } else {
      // Add layer
      if (!onLoadArcGIS) return;
      setGisLoading(layerId);
      onLoadArcGIS(layerId);
      setActiveLayers(prev => new Set(prev).add(layerId));
      setTimeout(() => setGisLoading(null), 3000);
    }
  }, [onLoadArcGIS, onRemoveArcGIS, activeLayers]);

  return (
    <div className="planet-nav">
      {/* Toggle button */}
      <button
        className={`planet-nav__toggle ${expanded ? 'planet-nav__toggle--active' : ''}`}
        onClick={() => setExpanded(v => !v)}
        title="Solar System Navigation"
        aria-label="Toggle planet navigation"
      >
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="12" cy="12" r="5" />
          <circle cx="12" cy="12" r="10" strokeDasharray="4 3" />
        </svg>
      </button>

      {/* Expanded planet list */}
      {expanded && (
        <div className="planet-nav__menu" ref={menuRef}>
          <div className="planet-nav__title">Solar System</div>
          {onResetView && (
            <button
              className="planet-nav__item planet-nav__item--reset"
              onClick={onResetView}
              title="Reset to default view"
            >
              <span className="planet-nav__dot" style={{ color: '#8b949e' }}>
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
                  <path d="M3 3v5h5" />
                </svg>
              </span>
              <span className="planet-nav__name">Reset View</span>
            </button>
          )}
          {PLANETS.map((p) => (
            <div key={p.target} className="planet-nav__item-row">
              <button
                className="planet-nav__item"
                onClick={() => handlePlanet(p.target)}
                title={`Navigate to ${p.name}`}
              >
                <span
                  className="planet-nav__dot"
                  style={{ color: p.color ?? '#FFD700' }}
                >
                  {p.icon}
                </span>
                <span className="planet-nav__name">{p.name}</span>
              </button>
              {p.target === 'moon' && onLaunchLunarLander && (
                <button
                  className="planet-nav__play-btn"
                  onClick={(e) => { e.stopPropagation(); onLaunchLunarLander(); }}
                  title="Land on the Moon — Apollo LM Simulator"
                >
                  ▶
                </button>
              )}
            </div>
          ))}

          {/* GIS layers sub-section */}
          {onLoadArcGIS && (
            <>
              <button
                className="planet-nav__gis-header"
                onClick={() => setShowGIS(v => !v)}
              >
                <span className="planet-nav__gis-icon">
                  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="12" cy="12" r="10" />
                    <path d="M2 12h20" />
                    <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
                  </svg>
                </span>
                <span className="planet-nav__name">Earth Layers</span>
                <span className="planet-nav__chevron">{showGIS ? '\u25BC' : '\u25B6'}</span>
              </button>
              {showGIS && GIS_LAYERS.map((l) => (
                <button
                  key={l.id}
                  className={`planet-nav__gis-item ${activeLayers.has(l.id) ? 'planet-nav__gis-item--active' : ''}`}
                  onClick={() => handleGISToggle(l.id)}
                  disabled={gisLoading === l.id}
                >
                  <span className="planet-nav__gis-status">
                    {gisLoading === l.id ? '\u23F3' : activeLayers.has(l.id) ? '\u25CF' : '\u25CB'}
                  </span>
                  <span className="planet-nav__name">{l.label}</span>
                </button>
              ))}
            </>
          )}

          {/* Launch Lunar Lander simulation */}
          {onLaunchLunarLander && (
            <button
              className="planet-nav__item planet-nav__lunar-btn"
              onClick={onLaunchLunarLander}
              title="Land on the Moon — Apollo LM Descent Simulator"
            >
              <span className="planet-nav__dot" style={{ color: '#cccccc' }}>🚀</span>
              <span className="planet-nav__name">Land on Moon</span>
            </button>
          )}

          {/* Launch Godot 3D governance engine */}
          {onLaunchGodot && (
            <button
              className="planet-nav__item planet-nav__godot-btn"
              onClick={onLaunchGodot}
              title="Launch Godot 3D Governance Engine"
            >
              <span className="planet-nav__dot" style={{ color: '#478cbf' }}>
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <polygon points="5 3 19 12 5 21 5 3" />
                </svg>
              </span>
              <span className="planet-nav__name">3D Engine</span>
            </button>
          )}
        </div>
      )}
    </div>
  );
};
