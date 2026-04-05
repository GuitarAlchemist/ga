import React from 'react';
import {
  BLENDERKIT_MODEL_MANIFEST,
  CANONICAL_SOLAR_SYSTEM_BODIES,
  resolveTextureSet,
} from '../../assets/space';

const ACTIVE_CANONICAL_BODY_IDS = ['sun', 'earth', 'moon', 'jupiter', 'saturn', 'milky-way', 'stars'] as const;

export const AssetProvenancePanel: React.FC = () => {
  const approvedModels = BLENDERKIT_MODEL_MANIFEST.filter(asset => asset.approvedForRuntime);

  return (
    <div className="prime-radiant__asset-provenance">
      <div className="prime-radiant__asset-provenance-header">
        <span className="prime-radiant__asset-provenance-title">Asset Provenance</span>
        <span className="prime-radiant__asset-provenance-count">
          {ACTIVE_CANONICAL_BODY_IDS.length} canonical bodies
        </span>
      </div>

      <div className="prime-radiant__asset-provenance-section">
        <div className="prime-radiant__asset-provenance-section-title">Canonical Solar Bodies</div>
        <div className="prime-radiant__asset-provenance-list">
          {ACTIVE_CANONICAL_BODY_IDS.map((bodyId) => {
            const asset = CANONICAL_SOLAR_SYSTEM_BODIES.find(item => item.id === bodyId)!;
            const textureSet = resolveTextureSet(bodyId, '2k');
            const maps = Object.keys(textureSet).sort();

            return (
              <div key={asset.id} className="prime-radiant__asset-provenance-item">
                <div className="prime-radiant__asset-provenance-item-head">
                  <span className="prime-radiant__asset-provenance-item-name">{asset.displayName}</span>
                  <span className="prime-radiant__asset-provenance-item-license">{asset.license}</span>
                </div>
                <div className="prime-radiant__asset-provenance-item-meta">
                  <span>{asset.source}</span>
                  <span className="prime-radiant__asset-provenance-dot">•</span>
                  <span>{maps.join(', ') || 'no maps'}</span>
                </div>
                <a
                  className="prime-radiant__asset-provenance-link"
                  href={asset.sourceUrl}
                  target="_blank"
                  rel="noreferrer"
                >
                  Source
                </a>
              </div>
            );
          })}
        </div>
      </div>

      <div className="prime-radiant__asset-provenance-section">
        <div className="prime-radiant__asset-provenance-section-title">BlenderKit Runtime Assets</div>
        {approvedModels.length === 0 ? (
          <div className="prime-radiant__asset-provenance-empty">
            No BlenderKit assets are approved for runtime yet.
          </div>
        ) : (
          <div className="prime-radiant__asset-provenance-list">
            {approvedModels.map((asset) => (
              <div key={asset.id} className="prime-radiant__asset-provenance-item">
                <div className="prime-radiant__asset-provenance-item-head">
                  <span className="prime-radiant__asset-provenance-item-name">{asset.displayName}</span>
                  <span className="prime-radiant__asset-provenance-item-license">{asset.license}</span>
                </div>
                <div className="prime-radiant__asset-provenance-item-meta">
                  <span>{asset.author}</span>
                  <span className="prime-radiant__asset-provenance-dot">•</span>
                  <span>{asset.category}</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
