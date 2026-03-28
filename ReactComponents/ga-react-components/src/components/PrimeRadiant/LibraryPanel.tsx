// src/components/PrimeRadiant/LibraryPanel.tsx
// Browse Jean-Pierre Petit's scientific comics (Savoir sans Frontières) as curriculum references

import React, { useEffect, useState, useCallback } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
interface CurriculumReference {
  title: string;
  topic: string;
  departments: string[];
  level: string;
  curriculum_fit: string;
}

interface CurriculumData {
  source: string;
  license: string;
  archive: string;
  website: string;
  acknowledgement: string;
  references: CurriculumReference[];
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------
const DEPT_COLORS: Record<string, string> = {
  mathematics: '#FFD700',
  physics: '#4FC3F7',
  'computer-science': '#73d13d',
  music: '#FF6B6B',
  philosophy: '#CE93D8',
  'cognitive-science': '#FFB300',
  'product-management': '#FF8A65',
  futurology: '#80DEEA',
};

const LEVEL_ICON: Record<string, string> = {
  'middle-school to high-school': 'I',
  'high-school': 'II',
  'high-school to undergraduate': 'III',
  'undergraduate': 'IV',
};

// ---------------------------------------------------------------------------
// Data fetching
// ---------------------------------------------------------------------------
async function fetchCurriculumRefs(): Promise<CurriculumData | null> {
  // Try backend API first
  try {
    const res = await fetch('/api/governance/file-content?path=governance/demerzel/state/streeling/curriculum-references.json');
    if (res.ok) {
      const text = await res.text();
      return JSON.parse(text);
    }
  } catch { /* fall through */ }

  // Fallback: embedded data
  return {
    source: 'Jean-Pierre Petit — Savoir sans Frontières (Knowledge Without Borders)',
    license: 'Free for non-commercial educational use',
    archive: 'https://archive.org/details/TheseAnglaise',
    website: 'https://www.savoir-sans-frontieres.com/JPP/telechargeables/free_downloads.htm',
    acknowledgement: 'Scientific comics by Jean-Pierre Petit, astrophysicist, distributed freely by the Association Savoir sans Frontières',
    references: [
      { title: 'Topo the World', topic: 'Topology — surfaces, homeomorphisms, Boy\'s surface', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Foundations of topological thinking for TDA and Poincaré embeddings' },
      { title: 'Here\'s Looking at Euclid', topic: 'Geometry — Euclidean constructions, geometric reasoning', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Geometric foundations for fretboard geometry and spatial reasoning' },
      { title: 'Bourbakof', topic: 'Abstract algebra — group theory, mathematical structures', departments: ['mathematics', 'music'], level: 'undergraduate', curriculum_fit: 'Group theory for pitch class groups, symmetry in music theory' },
      { title: 'Logotron', topic: 'Mathematical logic — formal systems, proof theory', departments: ['mathematics', 'philosophy', 'computer-science'], level: 'undergraduate', curriculum_fit: 'Foundations for tetravalent logic, formal verification, grammar theory' },
      { title: 'Computer Magic', topic: 'Computing — information theory, digital logic, algorithms', departments: ['computer-science'], level: 'high-school', curriculum_fit: 'Fundamentals for understanding constrained generation and digital signal processing' },
      { title: 'Run Robot Run', topic: 'Robotics — automation, control systems, agent behavior', departments: ['computer-science', 'cognitive-science'], level: 'high-school', curriculum_fit: 'Agent behavior foundations, governance as control theory analogy' },
      { title: 'Everything is Relative', topic: 'Special and general relativity — spacetime, reference frames', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Reference frame thinking applicable to multi-perspective governance' },
      { title: 'The Silence Barrier', topic: 'Wave mechanics — sound, barriers, propagation', departments: ['physics', 'music'], level: 'high-school', curriculum_fit: 'Acoustics foundations for understanding string vibration and harmonics' },
      { title: 'The Black Hole', topic: 'Astrophysics — gravitational collapse, spacetime curvature', departments: ['physics', 'mathematics'], level: 'undergraduate', curriculum_fit: 'Extreme physics as thinking tool for boundary conditions and singularities' },
      { title: 'The Economicon', topic: 'Economics — market dynamics, resource allocation', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Resource allocation thinking for budget management and prioritization' },
      { title: 'Big Bang', topic: 'Cosmology — origin of the universe, early physics', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Large-scale systems thinking, initial conditions and emergence' },
    ],
  };
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const LibraryPanel: React.FC = () => {
  const [data, setData] = useState<CurriculumData | null>(null);
  const [collapsed, setCollapsed] = useState(false);
  const [expandedItems, setExpandedItems] = useState<Set<string>>(new Set());
  const [filter, setFilter] = useState<string | null>(null);

  useEffect(() => {
    fetchCurriculumRefs().then(setData);
  }, []);

  const toggleItem = useCallback((title: string) => {
    setExpandedItems(prev => {
      const next = new Set(prev);
      if (next.has(title)) next.delete(title);
      else next.add(title);
      return next;
    });
  }, []);

  const refs = data?.references ?? [];
  const filtered = filter
    ? refs.filter(r => r.departments.includes(filter))
    : refs;

  // Collect all departments for filter chips
  const allDepts = [...new Set(refs.flatMap(r => r.departments))].sort();

  return (
    <div className="prime-radiant__library">
      <div
        className="prime-radiant__library-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__library-title">
          Library
          <span className="prime-radiant__library-count">{refs.length}</span>
        </span>
        <span className="prime-radiant__library-toggle">{collapsed ? '>' : 'v'}</span>
      </div>

      {!collapsed && data && (
        <div className="prime-radiant__library-body">
          {/* Source attribution */}
          <div className="prime-radiant__library-source">
            <span className="prime-radiant__library-source-label">Jean-Pierre Petit</span>
            <span className="prime-radiant__library-source-sub">Savoir sans Fronti&egrave;res</span>
            <div className="prime-radiant__library-source-links">
              <a href={data.website} target="_blank" rel="noopener noreferrer" className="prime-radiant__library-link">Downloads</a>
              <a href={data.archive} target="_blank" rel="noopener noreferrer" className="prime-radiant__library-link">Archive.org</a>
            </div>
            <span className="prime-radiant__library-license">{data.license}</span>
          </div>

          {/* Department filter chips */}
          <div className="prime-radiant__library-filters">
            <button
              className={`prime-radiant__library-filter ${filter === null ? 'prime-radiant__library-filter--active' : ''}`}
              onClick={() => setFilter(null)}
            >All</button>
            {allDepts.map(dept => (
              <button
                key={dept}
                className={`prime-radiant__library-filter ${filter === dept ? 'prime-radiant__library-filter--active' : ''}`}
                style={filter === dept ? { borderColor: DEPT_COLORS[dept] ?? '#888', color: DEPT_COLORS[dept] ?? '#888' } : undefined}
                onClick={() => setFilter(filter === dept ? null : dept)}
              >{dept.replace('-', ' ')}</button>
            ))}
          </div>

          {/* Reference list */}
          <div className="prime-radiant__library-list">
            {filtered.map(ref => {
              const expanded = expandedItems.has(ref.title);
              const levelIcon = Object.entries(LEVEL_ICON).find(([k]) => ref.level.includes(k))?.[1] ?? 'II';
              return (
                <div key={ref.title} className="prime-radiant__library-item">
                  <div
                    className="prime-radiant__library-item-header"
                    onClick={() => toggleItem(ref.title)}
                  >
                    <span className="prime-radiant__library-item-level" title={ref.level}>{levelIcon}</span>
                    <span className="prime-radiant__library-item-title">{ref.title}</span>
                    <span className="prime-radiant__library-item-chevron">{expanded ? 'v' : '>'}</span>
                  </div>
                  <div className="prime-radiant__library-item-topic">{ref.topic}</div>
                  <div className="prime-radiant__library-item-depts">
                    {ref.departments.map(d => (
                      <span
                        key={d}
                        className="prime-radiant__library-dept-tag"
                        style={{ color: DEPT_COLORS[d] ?? '#888', borderColor: `${DEPT_COLORS[d] ?? '#888'}44` }}
                      >{d.replace('-', ' ')}</span>
                    ))}
                  </div>
                  {expanded && (
                    <div className="prime-radiant__library-item-detail">
                      <div className="prime-radiant__library-item-fit">
                        <span className="prime-radiant__library-item-fit-label">Curriculum fit</span>
                        {ref.curriculum_fit}
                      </div>
                      <div className="prime-radiant__library-item-meta">
                        Level: {ref.level}
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>

          {filtered.length === 0 && (
            <div className="prime-radiant__library-empty">No references in this department</div>
          )}
        </div>
      )}
    </div>
  );
};
