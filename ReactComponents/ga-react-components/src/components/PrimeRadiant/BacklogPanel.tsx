// src/components/PrimeRadiant/BacklogPanel.tsx
// Shows the project backlog (from BACKLOG.md via API) as an accordion in the Prime Radiant

import React, { useEffect, useState } from 'react';

interface BacklogSection {
  section: string;
  subsection?: string;
  items: string[];
}

interface BacklogData {
  sections: BacklogSection[];
  lastModified?: string;
}

export const BacklogPanel: React.FC<{ collapsed?: boolean }> = ({ collapsed: initialCollapsed = false }) => {
  const [data, setData] = useState<BacklogData | null>(null);
  const [collapsed, setCollapsed] = useState(initialCollapsed);
  const [expandedSections, setExpandedSections] = useState<Set<string>>(new Set());

  useEffect(() => {
    const fetchBacklog = async () => {
      try {
        const res = await fetch('/api/governance/backlog');
        if (res.ok) {
          setData(await res.json());
        }
      } catch {
        // Fallback: representative backlog when API is unavailable
        setData({
          sections: [
            { section: 'Governance', items: [
              'Cycle 005: Full governance audit with compliance scoring',
              'Streeling: Expand multilingual course coverage to 12 languages',
              'Proto-conscience: Observability dashboard for ethical decisions',
            ]},
            { section: 'Research', items: [
              'Hari: Lie algebra DNA architecture — prototype phase',
              'Seldon Plan: Long-horizon prediction engine validation',
              'Hexavalent logic: Integration with belief currency system',
            ]},
            { section: 'Build', items: [
              'Prime Radiant: Real-time WebSocket updates',
              'ix: Memristive Markov persistence layer',
              'tars: F# reasoning agent — belief propagation',
              'ga: Guitar Singularity — interactive fretboard',
            ]},
          ],
        });
      }
    };
    fetchBacklog();
  }, []);

  const toggleSection = (key: string) => {
    setExpandedSections(prev => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  // Always render — never return null (causes empty panel)
  const totalItems = data ? data.sections.reduce((sum, s) => sum + s.items.length, 0) : 0;

  return (
    <div className="prime-radiant__backlog">
      <div
        className="prime-radiant__backlog-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__backlog-title">
          Backlog
          <span className="prime-radiant__backlog-count">{totalItems}</span>
        </span>
        <span className="prime-radiant__backlog-toggle">{collapsed ? '>' : 'v'}</span>
      </div>

      {!collapsed && data && (
        <div className="prime-radiant__backlog-body">
          {data.sections.map((section, i) => {
            const key = `${section.section}-${section.subsection ?? i}`;
            const isExpanded = expandedSections.has(key);
            const label = section.subsection
              ? `${section.subsection}`
              : section.section;

            return (
              <div key={key} className="prime-radiant__backlog-section">
                <div
                  className="prime-radiant__backlog-section-header"
                  onClick={() => toggleSection(key)}
                >
                  <span>{isExpanded ? 'v' : '>'}</span>
                  <span className="prime-radiant__backlog-section-title">{label}</span>
                  <span className="prime-radiant__backlog-section-count">{section.items.length}</span>
                </div>

                {isExpanded && (
                  <div className="prime-radiant__backlog-items">
                    {section.items.map((item, j) => {
                      const colonIdx = item.indexOf(':');
                      const title = colonIdx > 0 ? item.slice(0, colonIdx) : item;
                      const desc = colonIdx > 0 ? item.slice(colonIdx + 1).trim() : '';

                      return (
                        <div key={j} className="prime-radiant__backlog-item" title={item}>
                          <span className="prime-radiant__backlog-item-title">{title}</span>
                          {desc && <span className="prime-radiant__backlog-item-desc">{desc}</span>}
                        </div>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
