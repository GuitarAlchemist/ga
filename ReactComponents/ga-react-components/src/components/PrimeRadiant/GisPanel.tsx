// src/components/PrimeRadiant/GisPanel.tsx
// AI GIS control panel — manage pins, paths, clusters on planet surfaces.
// Connects to the GisLayerManager for the active planet.

import React, { useCallback, useEffect, useRef, useState } from 'react';
import type { GisLayerManager, GisPin, GisPath } from './GisLayer';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface GisPanelProps {
  /** GIS managers keyed by planet name */
  managers?: Map<string, GisLayerManager>;
}

// ---------------------------------------------------------------------------
// Preset pin collections for quick demos
// ---------------------------------------------------------------------------

const PRESET_PINS: Record<string, GisPin[]> = {
  'World Capitals': [
    { id: 'dc', lat: 38.9, lon: -77.0, label: 'Washington DC', icon: '🏛', color: '#3388ff', category: 'capital' },
    { id: 'london', lat: 51.5, lon: -0.1, label: 'London', icon: '🇬🇧', color: '#3388ff', category: 'capital' },
    { id: 'tokyo', lat: 35.7, lon: 139.7, label: 'Tokyo', icon: '🇯🇵', color: '#3388ff', category: 'capital' },
    { id: 'paris', lat: 48.9, lon: 2.3, label: 'Paris', icon: '🇫🇷', color: '#3388ff', category: 'capital' },
    { id: 'beijing', lat: 39.9, lon: 116.4, label: 'Beijing', icon: '🇨🇳', color: '#3388ff', category: 'capital' },
    { id: 'canberra', lat: -35.3, lon: 149.1, label: 'Canberra', icon: '🇦🇺', color: '#3388ff', category: 'capital' },
    { id: 'brasilia', lat: -15.8, lon: -47.9, label: 'Brasília', icon: '🇧🇷', color: '#3388ff', category: 'capital' },
  ],
  'AI Data Centers': [
    { id: 'aws-us', lat: 39.0, lon: -77.5, label: 'AWS US-East', icon: '☁', color: '#FF9900', category: 'cloud', pulse: true },
    { id: 'aws-eu', lat: 53.3, lon: -6.3, label: 'AWS EU-West', icon: '☁', color: '#FF9900', category: 'cloud', pulse: true },
    { id: 'gcp-us', lat: 33.4, lon: -112.0, label: 'GCP US', icon: '☁', color: '#4285F4', category: 'cloud', pulse: true },
    { id: 'azure-eu', lat: 52.4, lon: 4.9, label: 'Azure NL', icon: '☁', color: '#0078D4', category: 'cloud', pulse: true },
    { id: 'gcp-asia', lat: 1.3, lon: 103.8, label: 'GCP Singapore', icon: '☁', color: '#4285F4', category: 'cloud', pulse: true },
  ],
  'Governance Nodes': [
    { id: 'demerzel-hq', lat: 40.7, lon: -74.0, label: 'Demerzel HQ', icon: '🛡', color: '#FFD700', category: 'governance', pulse: true },
    { id: 'ix-forge', lat: 37.4, lon: -122.1, label: 'ix Forge', icon: '⚙', color: '#73D117', category: 'governance' },
    { id: 'tars-lab', lat: 47.6, lon: -122.3, label: 'TARS Lab', icon: '🧠', color: '#4FC3F7', category: 'governance' },
    { id: 'ga-studio', lat: 34.0, lon: -118.2, label: 'GA Studio', icon: '🎸', color: '#FFA726', category: 'governance' },
    { id: 'seldon-uni', lat: 51.8, lon: -1.3, label: 'Streeling U', icon: '🎓', color: '#AB47BC', category: 'governance', pulse: true },
  ],
};

const PRESET_PATHS: Record<string, GisPath[]> = {
  'Governance Network': [
    { id: 'gp-1', points: [{ lat: 40.7, lon: -74.0 }, { lat: 37.4, lon: -122.1 }], color: '#FFD700', animated: true, dashed: true, label: 'Galactic Protocol' },
    { id: 'gp-2', points: [{ lat: 40.7, lon: -74.0 }, { lat: 47.6, lon: -122.3 }], color: '#4FC3F7', animated: true, dashed: true },
    { id: 'gp-3', points: [{ lat: 40.7, lon: -74.0 }, { lat: 34.0, lon: -118.2 }], color: '#FFA726', animated: true, dashed: true },
    { id: 'gp-4', points: [{ lat: 40.7, lon: -74.0 }, { lat: 51.8, lon: -1.3 }], color: '#AB47BC', animated: true, dashed: true },
  ],
  'Data Flow': [
    { id: 'df-1', points: [{ lat: 39.0, lon: -77.5 }, { lat: 53.3, lon: -6.3 }, { lat: 52.4, lon: 4.9 }], color: '#33FF88', width: 3, animated: true, dashed: true },
    { id: 'df-2', points: [{ lat: 39.0, lon: -77.5 }, { lat: 1.3, lon: 103.8 }, { lat: 33.4, lon: -112.0 }], color: '#33FF88', width: 3, animated: true, dashed: true },
  ],
};

const PLANETS = ['earth', 'mars', 'venus', 'jupiter', 'saturn', 'mercury', 'uranus', 'neptune'];

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export const GisPanel: React.FC<GisPanelProps> = ({ managers }) => {
  const [activePlanet, setActivePlanet] = useState('earth');
  const [pinCount, setPinCount] = useState(0);
  const [pathCount, setPathCount] = useState(0);
  const [clusteringEnabled, setClusteringEnabled] = useState(false);
  const [clusterRadius, setClusterRadius] = useState(10);

  // Add Pin form
  const [showAddPin, setShowAddPin] = useState(false);
  const [pinLat, setPinLat] = useState('');
  const [pinLon, setPinLon] = useState('');
  const [pinLabel, setPinLabel] = useState('');
  const [pinColor, setPinColor] = useState('#ff4444');
  const [pinIcon, setPinIcon] = useState('📍');

  // Add Path form
  const [showAddPath, setShowAddPath] = useState(false);
  const [pathPointsStr, setPathPointsStr] = useState('');
  const [pathColor, setPathColor] = useState('#33ccff');

  const manager = managers?.get(activePlanet) ?? null;

  // Sync stats
  useEffect(() => {
    if (!manager) { setPinCount(0); setPathCount(0); return; }
    const unsub = manager.onChange(() => {
      setPinCount(manager.pinCount);
      setPathCount(manager.pathCount);
    });
    setPinCount(manager.pinCount);
    setPathCount(manager.pathCount);
    return unsub;
  }, [manager]);

  const handleAddPin = useCallback(() => {
    if (!manager || !pinLat || !pinLon) return;
    manager.addPin({
      id: `pin-${Date.now()}`,
      lat: parseFloat(pinLat),
      lon: parseFloat(pinLon),
      label: pinLabel || 'Pin',
      color: pinColor,
      icon: pinIcon,
      pulse: true,
    });
    setShowAddPin(false);
    setPinLabel('');
    setPinLat('');
    setPinLon('');
  }, [manager, pinLat, pinLon, pinLabel, pinColor, pinIcon]);

  const handleAddPath = useCallback(() => {
    if (!manager || !pathPointsStr.trim()) return;
    try {
      const points = JSON.parse(pathPointsStr) as { lat: number; lon: number }[];
      manager.addPath({
        id: `path-${Date.now()}`,
        points,
        color: pathColor,
        animated: true,
        dashed: true,
      });
      setShowAddPath(false);
      setPathPointsStr('');
    } catch { /* invalid JSON */ }
  }, [manager, pathPointsStr, pathColor]);

  const handlePresetPins = useCallback((name: string) => {
    if (!manager) return;
    const pins = PRESET_PINS[name];
    if (pins) manager.addPins(pins);
  }, [manager]);

  const handlePresetPaths = useCallback((name: string) => {
    if (!manager) return;
    const paths = PRESET_PATHS[name];
    if (paths) paths.forEach(p => manager.addPath(p));
  }, [manager]);

  const toggleClustering = useCallback(() => {
    if (!manager) return;
    if (clusteringEnabled) {
      manager.disableClustering();
      setClusteringEnabled(false);
    } else {
      manager.enableClustering(clusterRadius);
      setClusteringEnabled(true);
    }
  }, [manager, clusteringEnabled, clusterRadius]);

  return (
    <div className="gis-panel">
      {/* Planet selector */}
      <div className="gis-panel__planet-bar">
        {PLANETS.filter(p => !managers || managers.has(p)).map(p => (
          <button
            key={p}
            className={`gis-panel__planet-btn ${activePlanet === p ? 'gis-panel__planet-btn--active' : ''}`}
            onClick={() => setActivePlanet(p)}
          >
            {p.charAt(0).toUpperCase() + p.slice(1)}
          </button>
        ))}
      </div>

      {/* Stats bar */}
      <div className="gis-panel__stats">
        <span>{pinCount} pins</span>
        <span>{pathCount} paths</span>
        <span>{manager?.clusterCount ?? 0} clusters</span>
      </div>

      {/* Preset datasets */}
      <div className="gis-panel__section">
        <div className="gis-panel__section-header">Presets</div>
        <div className="gis-panel__presets">
          {Object.keys(PRESET_PINS).map(name => (
            <button key={name} className="gis-panel__preset-btn" onClick={() => handlePresetPins(name)}>
              📍 {name}
            </button>
          ))}
          {Object.keys(PRESET_PATHS).map(name => (
            <button key={name} className="gis-panel__preset-btn" onClick={() => handlePresetPaths(name)}>
              🛤 {name}
            </button>
          ))}
        </div>
      </div>

      {/* Clustering controls */}
      <div className="gis-panel__section">
        <div className="gis-panel__section-header">Clustering</div>
        <div className="gis-panel__cluster-row">
          <button
            className={`gis-panel__toggle ${clusteringEnabled ? 'gis-panel__toggle--on' : ''}`}
            onClick={toggleClustering}
          >
            {clusteringEnabled ? 'ON' : 'OFF'}
          </button>
          <input
            type="range"
            min="3"
            max="30"
            value={clusterRadius}
            onChange={e => {
              setClusterRadius(parseInt(e.target.value));
              if (clusteringEnabled && manager) manager.enableClustering(parseInt(e.target.value));
            }}
            className="gis-panel__slider"
          />
          <span className="gis-panel__slider-val">{clusterRadius}°</span>
        </div>
      </div>

      {/* Add Pin */}
      <div className="gis-panel__section">
        <div className="gis-panel__section-header">
          Pins
          <button className="gis-panel__add-btn" onClick={() => setShowAddPin(!showAddPin)}>+ Add</button>
        </div>
        {showAddPin && (
          <div className="gis-panel__form">
            <div className="gis-panel__form-row">
              <input className="gis-panel__input" placeholder="Lat" value={pinLat} onChange={e => setPinLat(e.target.value)} />
              <input className="gis-panel__input" placeholder="Lon" value={pinLon} onChange={e => setPinLon(e.target.value)} />
            </div>
            <div className="gis-panel__form-row">
              <input className="gis-panel__input" placeholder="Label" value={pinLabel} onChange={e => setPinLabel(e.target.value)} />
              <input className="gis-panel__input gis-panel__input--icon" placeholder="Icon" value={pinIcon} onChange={e => setPinIcon(e.target.value)} />
            </div>
            <div className="gis-panel__form-row">
              <input type="color" value={pinColor} onChange={e => setPinColor(e.target.value)} className="gis-panel__color" />
              <button className="gis-panel__btn" onClick={handleAddPin}>Add Pin</button>
            </div>
          </div>
        )}
      </div>

      {/* Add Path */}
      <div className="gis-panel__section">
        <div className="gis-panel__section-header">
          Paths
          <button className="gis-panel__add-btn" onClick={() => setShowAddPath(!showAddPath)}>+ Add</button>
        </div>
        {showAddPath && (
          <div className="gis-panel__form">
            <textarea
              className="gis-panel__textarea"
              placeholder='[{"lat":40.7,"lon":-74},{"lat":51.5,"lon":-0.1}]'
              value={pathPointsStr}
              onChange={e => setPathPointsStr(e.target.value)}
              rows={3}
            />
            <div className="gis-panel__form-row">
              <input type="color" value={pathColor} onChange={e => setPathColor(e.target.value)} className="gis-panel__color" />
              <button className="gis-panel__btn" onClick={handleAddPath}>Add Path</button>
            </div>
          </div>
        )}
      </div>

      {/* Clear all */}
      <div className="gis-panel__section">
        <button className="gis-panel__btn gis-panel__btn--danger" onClick={() => manager?.clearAll()}>
          Clear All GIS Data
        </button>
      </div>
    </div>
  );
};
