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
  language: 'en' | 'fr';
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
    // --- English editions ---
    { title: 'Topo the World', topic: 'Topology \u2014 surfaces, homeomorphisms, Boy\u2019s surface', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Foundations of topological thinking for TDA and Poincar\u00e9 embeddings', archiveId: 'Topo_the_world_eng', language: 'en' },
    { title: 'Here\u2019s Looking at Euclid', topic: 'Geometry \u2014 Euclidean constructions, geometric reasoning', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Geometric foundations for fretboard geometry and spatial reasoning', archiveId: 'HERE_S_LOOKING_AT_EUCLID', language: 'en' },
    { title: 'Bourbakof', topic: 'Abstract algebra \u2014 group theory, mathematical structures', departments: ['mathematics', 'music'], level: 'undergraduate', curriculum_fit: 'Group theory for pitch class groups, symmetry in music theory', archiveId: 'Bourbakof_en', language: 'en' },
    { title: 'Logotron', topic: 'Mathematical logic \u2014 formal systems, proof theory', departments: ['mathematics', 'philosophy', 'computer-science'], level: 'undergraduate', curriculum_fit: 'Foundations for tetravalent logic, formal verification, grammar theory', archiveId: 'logotron_eng', language: 'en' },
    { title: 'Computer Magic', topic: 'Computing \u2014 information theory, digital logic, algorithms', departments: ['computer-science'], level: 'high-school', curriculum_fit: 'Fundamentals for understanding constrained generation and digital signal processing', archiveId: 'COMPUTER_MAGIC', language: 'en' },
    { title: 'Run Robot Run', topic: 'Robotics \u2014 automation, control systems, agent behavior', departments: ['computer-science', 'cognitive-science'], level: 'high-school', curriculum_fit: 'Agent behavior foundations, governance as control theory analogy', archiveId: 'RUN_ROBOT_RUN', language: 'en' },
    { title: 'Everything is Relative', topic: 'Special and general relativity \u2014 spacetime, reference frames', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Reference frame thinking applicable to multi-perspective governance', archiveId: 'EVERYTHING_IS_RELATIVE', language: 'en' },
    { title: 'The Silence Barrier', topic: 'Wave mechanics \u2014 sound, barriers, propagation', departments: ['physics', 'music'], level: 'high-school', curriculum_fit: 'Acoustics foundations for understanding string vibration and harmonics', archiveId: 'THE_SILENCE_BARRIER', language: 'en' },
    { title: 'The Black Hole', topic: 'Astrophysics \u2014 gravitational collapse, spacetime curvature', departments: ['physics', 'mathematics'], level: 'undergraduate', curriculum_fit: 'Extreme physics as thinking tool for boundary conditions and singularities', archiveId: 'THE_BLACK_HOLE', language: 'en' },
    { title: 'The Economicon', topic: 'Economics \u2014 market dynamics, resource allocation', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Resource allocation thinking for budget management and prioritization', archiveId: 'The_Economicon', language: 'en' },
    { title: 'Big Bang', topic: 'Cosmology \u2014 origin of the universe, early physics', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Large-scale systems thinking, initial conditions and emergence', archiveId: 'BIG_BANG', language: 'en' },
    { title: 'A Cosmic Story', topic: 'Cosmology \u2014 history of the universe from Big Bang to life', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Narrative cosmology for big-picture systems thinking', archiveId: 'cosmicstory_eng-1', language: 'en' },
    { title: 'Flight of Fancy', topic: 'Aerodynamics \u2014 flight mechanics, lift and drag', departments: ['physics'], level: 'high-school', curriculum_fit: 'Fluid dynamics intuition applicable to signal flow analysis', archiveId: 'FLIGHT_OF_FANCY', language: 'en' },
    { title: 'A Thousand Billion Suns', topic: 'Stellar physics \u2014 star formation, nuclear fusion, supernovae', departments: ['physics'], level: 'high-school to undergraduate', curriculum_fit: 'Energy transformation and lifecycle thinking for system evolution', archiveId: 'thousand_billion_suns_eng', language: 'en' },
    { title: 'The Dark Side of the Universe', topic: 'Dark matter and dark energy \u2014 cosmological mysteries', departments: ['physics', 'futurology'], level: 'undergraduate', curriculum_fit: 'Unknown unknowns \u2014 epistemic humility in governance', archiveId: 'The_Dark_Side_of_the_Universe', language: 'en' },
    { title: 'For a Fistful of Amperes', topic: 'Electromagnetism \u2014 circuits, magnetism, EM waves', departments: ['physics'], level: 'high-school', curriculum_fit: 'Electromagnetic foundations for signal processing analogies', archiveId: 'For_a_fistful_of_amperes', language: 'en' },
    // --- French editions (originals) ---
    { title: 'Le Topologicon', topic: 'Topologie \u2014 surfaces, hom\u00e9omorphismes, surface de Boy', departments: ['mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Fondations de la pens\u00e9e topologique pour TDA et plongements de Poincar\u00e9', archiveId: 'le_topologicon', language: 'fr' },
    { title: 'Le Geometricon', topic: 'G\u00e9om\u00e9trie \u2014 constructions euclidiennes, raisonnement g\u00e9om\u00e9trique', departments: ['mathematics', 'physics'], level: 'middle-school to high-school', curriculum_fit: 'Fondations g\u00e9om\u00e9triques pour la g\u00e9om\u00e9trie du manche et le raisonnement spatial', archiveId: 'LeGeometricon', language: 'fr' },
    { title: 'Les Aventures d\u2019Anselme Lanturlu: Bourbakof', topic: 'Alg\u00e8bre abstraite \u2014 th\u00e9orie des groupes, structures math\u00e9matiques', departments: ['mathematics', 'music'], level: 'undergraduate', curriculum_fit: 'Th\u00e9orie des groupes pour les classes de hauteurs, sym\u00e9trie en th\u00e9orie musicale', archiveId: 'Bourbakof_fr', language: 'fr' },
    { title: 'Le Logotron', topic: 'Logique math\u00e9matique \u2014 syst\u00e8mes formels, th\u00e9orie de la preuve', departments: ['mathematics', 'philosophy', 'computer-science'], level: 'undergraduate', curriculum_fit: 'Fondations pour la logique t\u00e9travalente, v\u00e9rification formelle, th\u00e9orie des grammaires', archiveId: 'logotron_fr', language: 'fr' },
    { title: 'L\u2019Informatique', topic: 'Informatique \u2014 th\u00e9orie de l\u2019information, logique num\u00e9rique, algorithmes', departments: ['computer-science'], level: 'high-school', curriculum_fit: 'Fondamentaux pour la g\u00e9n\u00e9ration contrainte et le traitement du signal num\u00e9rique', archiveId: 'linformatique', language: 'fr' },
    { title: 'Robot mais pas trop', topic: 'Robotique \u2014 automatisation, syst\u00e8mes de contr\u00f4le, comportement d\u2019agents', departments: ['computer-science', 'cognitive-science'], level: 'high-school', curriculum_fit: 'Fondations du comportement d\u2019agents, gouvernance comme analogie de contr\u00f4le', archiveId: 'robot_mais_pas_trop', language: 'fr' },
    { title: 'Tout est relatif', topic: 'Relativit\u00e9 restreinte et g\u00e9n\u00e9rale \u2014 espace-temps, r\u00e9f\u00e9rentiels', departments: ['physics', 'mathematics'], level: 'high-school to undergraduate', curriculum_fit: 'Pens\u00e9e en r\u00e9f\u00e9rentiels applicable \u00e0 la gouvernance multi-perspectives', archiveId: 'toutestrelatifjpp', language: 'fr' },
    { title: 'Le Mur du Silence', topic: 'M\u00e9canique des ondes \u2014 son, barri\u00e8res, propagation', departments: ['physics', 'music'], level: 'high-school', curriculum_fit: 'Fondations acoustiques pour la vibration des cordes et les harmoniques', archiveId: 'le_mur_du_silence', language: 'fr' },
    { title: 'Le Trou Noir', topic: 'Astrophysique \u2014 effondrement gravitationnel, courbure de l\u2019espace-temps', departments: ['physics', 'mathematics'], level: 'undergraduate', curriculum_fit: 'Physique extr\u00eame comme outil de r\u00e9flexion sur les conditions limites et singularit\u00e9s', archiveId: 'le_trou_noir', language: 'fr' },
    { title: 'L\u2019Economicon', topic: '\u00c9conomie \u2014 dynamiques de march\u00e9, allocation de ressources', departments: ['product-management'], level: 'high-school', curriculum_fit: 'Pens\u00e9e en allocation de ressources pour la gestion budg\u00e9taire et la priorisation', archiveId: 'leconomicon', language: 'fr' },
    { title: 'Big Bang', topic: 'Cosmologie \u2014 origine de l\u2019univers, physique primordiale', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Pens\u00e9e syst\u00e9mique \u00e0 grande \u00e9chelle, conditions initiales et \u00e9mergence', archiveId: 'big_bang_jpp', language: 'fr' },
    { title: 'Cosmic Story', topic: 'Cosmologie \u2014 histoire de l\u2019univers du Big Bang \u00e0 la vie', departments: ['physics', 'futurology'], level: 'high-school', curriculum_fit: 'Cosmologie narrative pour la pens\u00e9e syst\u00e9mique globale', archiveId: 'cosmic_story_fr', language: 'fr' },
    { title: 'Si on volait\u2009?', topic: 'A\u00e9rodynamique \u2014 m\u00e9canique du vol, portance et tra\u00een\u00e9e', departments: ['physics'], level: 'high-school', curriculum_fit: 'Intuition de dynamique des fluides applicable \u00e0 l\u2019analyse du flux de signaux', archiveId: 'si_on_volait', language: 'fr' },
    { title: 'Mille milliards de soleils', topic: 'Physique stellaire \u2014 formation d\u2019\u00e9toiles, fusion nucl\u00e9aire, supernovae', departments: ['physics'], level: 'high-school to undergraduate', curriculum_fit: 'Transformation d\u2019\u00e9nergie et pens\u00e9e en cycle de vie pour l\u2019\u00e9volution des syst\u00e8mes', archiveId: 'mille_milliards_de_soleils', language: 'fr' },
    { title: 'La Face cach\u00e9e de l\u2019Univers', topic: 'Mati\u00e8re noire et \u00e9nergie sombre \u2014 myst\u00e8res cosmologiques', departments: ['physics', 'futurology'], level: 'undergraduate', curriculum_fit: 'Inconnues inconnues \u2014 humilit\u00e9 \u00e9pist\u00e9mique en gouvernance', archiveId: 'la_face_cachee_de_lunivers', language: 'fr' },
    { title: 'Pour une poign\u00e9e d\u2019amp\u00e8res', topic: '\u00c9lectromagn\u00e9tisme \u2014 circuits, magn\u00e9tisme, ondes EM', departments: ['physics'], level: 'high-school', curriculum_fit: 'Fondations \u00e9lectromagn\u00e9tiques pour les analogies de traitement du signal', archiveId: 'pour_une_poignee_damperes', language: 'fr' },
  ],
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
/** Detect browser locale to pick default language */
function getDefaultLanguage(): 'en' | 'fr' {
  if (typeof navigator !== 'undefined') {
    const lang = navigator.language || '';
    if (lang.startsWith('fr')) return 'fr';
  }
  return 'en';
}

const FLAG_ICON: Record<'en' | 'fr', string> = {
  en: '\uD83C\uDDEC\uD83C\uDDE7',
  fr: '\uD83C\uDDEB\uD83C\uDDF7',
};

export const LibraryPanel: React.FC = () => {
  const [collapsed, setCollapsed] = useState(false);
  const [expandedItems, setExpandedItems] = useState<Set<string>>(new Set());
  const [deptFilter, setDeptFilter] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [readingComic, setReadingComic] = useState<CurriculumReference | null>(null);
  const [language, setLanguage] = useState<'en' | 'fr'>(getDefaultLanguage);

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

  // Apply language, department, and text search filters
  const filtered = useMemo(() => {
    const q = searchQuery.trim().toLowerCase();
    return refs.filter(r => {
      if (r.language !== language) return false;
      if (deptFilter && !r.departments.includes(deptFilter)) return false;
      if (q && !r.title.toLowerCase().includes(q) && !r.topic.toLowerCase().includes(q)) return false;
      return true;
    });
  }, [refs, deptFilter, searchQuery, language]);

  // Count only comics in the selected language
  const langCount = useMemo(() => refs.filter(r => r.language === language).length, [refs, language]);

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
          <span className="prime-radiant__library-count">{langCount}</span>
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

          {/* Language toggle */}
          <div className="prime-radiant__library-lang-toggle">
            {(['en', 'fr'] as const).map(lang => (
              <button
                key={lang}
                className={`prime-radiant__library-lang-btn${language === lang ? ' prime-radiant__library-lang-btn--active' : ''}`}
                onClick={(e) => { e.stopPropagation(); setLanguage(lang); }}
              >
                {FLAG_ICON[lang]} {lang === 'en' ? 'EN' : 'FR'}
              </button>
            ))}
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
                <div key={`${ref.language}-${ref.title}`} className="prime-radiant__library-item">
                  <div
                    className="prime-radiant__library-item-header"
                    onClick={() => toggleItem(ref.title)}
                  >
                    <span className="prime-radiant__library-item-level" title={ref.level}>{levelIcon}</span>
                    <span className="prime-radiant__library-item-title">{FLAG_ICON[ref.language]} {ref.title}</span>
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
              src={`https://archive.org/embed/${readingComic.archiveId}`}
              title={readingComic.title}
            />
          </div>
        </div>
      )}
    </div>
  );
};
