// src/components/PrimeRadiant/LibraryPanel.tsx
// Browse Jean-Pierre Petit's scientific comics (Savoir sans Frontieres) as curriculum references

import React, { useState, useCallback, useMemo } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
interface CurriculumReference {
  title: string;
  topic: string;
  departments: string[];
  level: string;
  curriculum_fit: string;
  archiveId?: string;  // Archive.org filename for PDF embed
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
  undergraduate: 'IV',
};

// ---------------------------------------------------------------------------
// Embedded curriculum data (static reference — no fetch needed)
// ---------------------------------------------------------------------------
const CURRICULUM_DATA: CurriculumData = {
  source: 'Jean-Pierre Petit \u2014 Savoir sans Fronti\u00e8res (Knowledge Without Borders)',
  license: 'Free for non-commercial educational use \u2014 duplication permitted, no profit, no political/sectarian/confessional connotations',
  archive: 'https://archive.org/details/TheseAnglaise',
  website: 'https://www.savoir-sans-frontieres.com/JPP/telechargeables/free_downloads.htm',
  acknowledgement: 'Scientific comics by Jean-Pierre Petit, astrophysicist, distributed freely by the Association Savoir sans Fronti\u00e8res',
  references: [
    { title: 'Topo the World', topic: 'Topology \u2014 surfaces, homeomorphisms, Boy\u2019s surface', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Foundations of topological thinking for TDA and Poincar\u00e9 embeddings', archiveId: 'Topo_the_world_eng' },
    { title: 'Here\u2019s Looking at Euclid', topic: 'Geometry \u2014 Euclidean constructions, geometric reasoning', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Geometric foundations for fretboard geometry and spatial reasoning', archiveId: 'HERE_S_LOOKING_AT_EUCLID' },
    { title: 'Bourbakof', topic: 'Abstract algebra \u2014 group theory, mathematical structures', departments: ['mathematics', 'music'], level: 'undergraduate', curriculum_fit: 'Group theory for pitch class groups, symmetry in music theory', archiveId: 'Bourbakof_en' },
    { title: 'Logotron', topic: 'Mathematical logic \u2014 formal systems, proof theory', departments: ['mathematics', 'philosophy', 'computer-science'], level: 'undergraduate', curriculum_fit: 'Foundations for tetravalent logic, formal verification, grammar theory', archiveId: 'logotron_eng' },
    { title: 'Computer Magic', topic: 'Computing \u2014 information theory, digital logic, algorithms', departments: ['computer-science'], level: 'high-school', curriculum_fit: 'Fundamentals for understanding constrained generation and digital signal processing', archiveId: 'COMPUTER_MAGIC' },
    { title: 'Run Robot Run', topic: 'Robotics \u2014 automation, control systems, agent behavior', departments: ['computer-science', 'cognitive-science'], level: 'high-school', curriculum_fit: 'Agent behavior foundations, governance as control theory analogy', archiveId: 'RUN_ROBOT_RUN' },
    { title: 'Everything is Relative', topic: 'Special and general relativity \u2014 spacetime, reference frames', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Reference frame thinking applicable to multi-perspective governance', archiveId: 'EVERYTHING_IS_RELATIVE' },
    { title: 'The Silence Barrier', topic: 'Wave mechanics \u2014 sound, barriers, propagation', departments: ['physics', 'music'], level: 'high-school', curriculum_fit: 'Acoustics foundations for understanding string vibration and harmonics', archiveId: 'THE_SILENCE_BARRIER' },
    { title: 'The Black Hole', topic: 'Astrophysics \u2014 gravitational collapse, spacetime curvature', departments: ['physics', 'mathematics'], level: 'undergraduate', curriculum_fit: 'Extreme physics as thinking tool for boundary conditions and singularities', archiveId: 'THE_BLACK_HOLE' },
    { title: 'The Economicon', topic: 'Economics \u2014 market dynamics, resource allocation', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Resource allocation thinking for budget management and prioritization', archiveId: 'The_Economicon' },
    { title: 'Big Bang', topic: 'Cosmology \u2014 origin of the universe, early physics', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Large-scale systems thinking, initial conditions and emergence', archiveId: 'BIG_BANG' },
    { title: 'A Cosmic Story', topic: 'Cosmology \u2014 history of the universe from Big Bang to life', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Narrative cosmology for big-picture systems thinking', archiveId: 'cosmicstory_eng-1' },
    { title: 'Flight of Fancy', topic: 'Aerodynamics \u2014 flight mechanics, lift and drag', departments: ['physics'], level: 'high-school', curriculum_fit: 'Fluid dynamics intuition applicable to signal flow analysis', archiveId: 'FLIGHT_OF_FANCY' },
    { title: 'A Thousand Billion Suns', topic: 'Stellar physics \u2014 star formation, nuclear fusion, supernovae', departments: ['physics'], level: 'high-school to undergraduate', curriculum_fit: 'Energy transformation and lifecycle thinking for system evolution', archiveId: 'thousand_billion_suns_eng' },
    { title: 'The Dark Side of the Universe', topic: 'Dark matter and dark energy \u2014 cosmological mysteries', departments: ['physics', 'futurology'], level: 'undergraduate', curriculum_fit: 'Unknown unknowns \u2014 epistemic humility in governance', archiveId: 'The_Dark_Side_of_the_Universe' },
    { title: 'For a Fistful of Amperes', topic: 'Electromagnetism \u2014 circuits, magnetism, EM waves', departments: ['physics'], level: 'high-school', curriculum_fit: 'Electromagnetic foundations for signal processing analogies', archiveId: 'For_a_fistful_of_amperes' },
  ],
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const LibraryPanel: React.FC = () => {
  const [collapsed, setCollapsed] = useState(false);
  const [expandedItems, setExpandedItems] = useState<Set<string>>(new Set());
  const [deptFilter, setDeptFilter] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [readingComic, setReadingComic] = useState<CurriculumReference | null>(null);

  const data = CURRICULUM_DATA;
  const refs = data.references;

  const toggleItem = useCallback((title: string) => {
    setExpandedItems(prev => {
      const next = new Set(prev);
      if (next.has(title)) next.delete(title);
      else next.add(title);
      return next;
    });
  }, []);

  // Apply both department and text search filters
  const filtered = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return refs.filter(r => {
      if (deptFilter && !r.departments.includes(deptFilter)) return false;
      if (q && !r.title.toLowerCase().includes(q) && !r.topic.toLowerCase().includes(q)) return false;
      return true;
    });
  }, [refs, deptFilter, searchQuery]);

  // Collect all departments for filter chips
  const allDepts = useMemo(
    () => [...new Set(refs.flatMap(r => r.departments))].sort(),
    [refs],
  );

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

      {!collapsed && (
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

          {/* Text search */}
          <div className="prime-radiant__library-search">
            <input
              type="text"
              className="prime-radiant__library-search-input"
              placeholder="Search by title or topic..."
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
            />
            {searchQuery && (
              <button
                className="prime-radiant__library-search-clear"
                onClick={() => setSearchQuery('')}
                aria-label="Clear search"
              >&#x2715;</button>
            )}
          </div>

          {/* Department filter chips */}
          <div className="prime-radiant__library-filters">
            <button
              className={`prime-radiant__library-filter ${deptFilter === null ? 'prime-radiant__library-filter--active' : ''}`}
              onClick={() => setDeptFilter(null)}
            >All</button>
            {allDepts.map(dept => (
              <button
                key={dept}
                className={`prime-radiant__library-filter ${deptFilter === dept ? 'prime-radiant__library-filter--active' : ''}`}
                style={deptFilter === dept ? { borderColor: DEPT_COLORS[dept] ?? '#888', color: DEPT_COLORS[dept] ?? '#888' } : undefined}
                onClick={() => setDeptFilter(deptFilter === dept ? null : dept)}
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
                      {ref.archiveId && (
                        <div className="prime-radiant__library-item-actions">
                          <button
                            className="prime-radiant__library-read-btn"
                            onClick={(e) => { e.stopPropagation(); setReadingComic(ref); }}
                          >
                            Read Comic
                          </button>
                          <a
                            href={`https://archive.org/download/TheseAnglaise/${ref.archiveId}.pdf`}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="prime-radiant__library-download-btn"
                            onClick={(e) => e.stopPropagation()}
                          >
                            PDF
                          </a>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
          </div>

          {filtered.length === 0 && (
            <div className="prime-radiant__library-empty">
              {searchQuery ? 'No comics match your search' : 'No references in this department'}
            </div>
          )}

          {/* License attribution footer */}
          <div className="prime-radiant__library-footer">
            {data.acknowledgement}
          </div>
        </div>
      )}

      {/* PDF Reader overlay */}
      {readingComic && readingComic.archiveId && (
        <div className="prime-radiant__library-reader-overlay">
          <div className="prime-radiant__library-reader">
            <div className="prime-radiant__library-reader-header">
              <div className="prime-radiant__library-reader-title">
                <span className="prime-radiant__library-reader-book">{readingComic.title}</span>
                <span className="prime-radiant__library-reader-author">Jean-Pierre Petit &mdash; Savoir sans Fronti&egrave;res</span>
              </div>
              <div className="prime-radiant__library-reader-actions">
                <a
                  href={`https://archive.org/download/TheseAnglaise/${readingComic.archiveId}.pdf`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="prime-radiant__library-reader-dl"
                >Download PDF</a>
                <button
                  className="prime-radiant__library-reader-close"
                  onClick={() => setReadingComic(null)}
                >&#x2715;</button>
              </div>
            </div>
            <div className="prime-radiant__library-reader-legal">
              Public Domain (Mark 1.0) &mdash; Free for educational use. Scientific comics by Jean-Pierre Petit, astrophysicist.
              Distributed by Association Savoir sans Fronti&egrave;res. Source: Archive.org
            </div>
            <iframe
              className="prime-radiant__library-reader-frame"
              src={`https://archive.org/download/TheseAnglaise/${readingComic.archiveId}.pdf`}
              title={readingComic.title}
            />
          </div>
        </div>
      )}
    </div>
  );
};
