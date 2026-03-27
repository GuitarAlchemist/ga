// src/components/PrimeRadiant/CourseViewer.tsx
// Streeling University course browser — embedded in Prime Radiant as a full-screen modal

import React, { useState, useMemo, useCallback, useEffect, useRef } from 'react';
import Markdown from 'react-markdown';
import { departments, totalCourses, totalDepartments, type Course } from './courseData';

interface CourseViewerProps {
  open: boolean;
  onClose: () => void;
}

/* ---------- Queue types ---------- */

interface CourseRequest {
  id: string;
  department: string;
  topic: string;
  level: 'introductory' | 'intermediate' | 'advanced' | 'phd';
  requestedAt: string;
  status: 'queued' | 'generating' | 'completed';
  assignedTo?: string;   // agent/model handling it
  eta?: string;          // estimated completion
  progress?: number;     // 0-100
}

interface ResearchRequest {
  id: string;
  department: string;
  topic: string;
  depth: 'survey' | 'systematic-review' | 'deep-dive' | 'frontier';
  sources: string[];
  requestedAt: string;
  status: 'queued' | 'researching' | 'completed';
  assignedTo?: string;
  eta?: string;
  progress?: number;
}

interface TranslationRequest {
  courseId: string;
  department: string;
  targetLanguage: string;
  requestedAt: string;
  status: 'queued' | 'in_progress' | 'completed';
  assignedTo?: string;
  eta?: string;
  progress?: number;
}

type DialogKind = 'generate-course' | 'deep-research' | null;

const COURSE_QUEUE_KEY = 'streeling-course-queue';
const RESEARCH_QUEUE_KEY = 'streeling-research-queue';
const TRANSLATION_QUEUE_KEY = 'streeling-translation-queue';

const ALL_LANGUAGES = ['en', 'es', 'fr', 'pt', 'de', 'it'] as const;

const LEVEL_OPTIONS: { value: CourseRequest['level']; label: string }[] = [
  { value: 'introductory', label: 'Introductory' },
  { value: 'intermediate', label: 'Intermediate' },
  { value: 'advanced', label: 'Advanced' },
  { value: 'phd', label: 'PhD / Research' },
];

const DEPTH_OPTIONS: { value: ResearchRequest['depth']; label: string }[] = [
  { value: 'survey', label: 'Survey' },
  { value: 'systematic-review', label: 'Systematic Review' },
  { value: 'deep-dive', label: 'Deep Dive' },
  { value: 'frontier', label: 'Frontier Research' },
];

const SOURCE_OPTIONS = ['arXiv', 'Semantic Scholar', 'Google Scholar', 'Cross-model validation'];

/* --- AI topic suggestions per department --- */
const AI_SUGGESTIONS: Record<string, { topics: string[]; research: string[] }> = {
  'Guitar Studies': {
    topics: ['CAGED System Deep Dive', 'Sweep Picking Biomechanics', 'Jazz Comping Voicings', 'Fingerstyle Independence Exercises', 'Modes of Melodic Minor'],
    research: ['Biomechanical Optimization of Left-Hand Fretting Postures', 'Neural Correlates of Guitar Improvisation vs Composed Performance'],
  },
  'Music': {
    topics: ['Negative Harmony and Ernst Levy', 'Neo-Riemannian Transformations', 'Spectral Music Composition', 'Microtonality and 31-TET', 'Polyrhythmic Structures in West African Music'],
    research: ['Algebraic Structures in Post-Tonal Music Theory', 'Category-Theoretic Foundations of Harmonic Analysis'],
  },
  'Mathematics': {
    topics: ['Lie Groups in Music Theory', 'Topological Data Analysis for Chord Spaces', 'Sheaf Theory for Harmonic Progressions', 'Clifford Algebras and Pitch-Class Sets'],
    research: ['Homotopy Type Theory Applied to Musical Transformations', 'Persistent Homology of Voice Leading Spaces'],
  },
  'Computer Science': {
    topics: ['Transformer Architectures for Music Generation', 'Reinforcement Learning for Composition', 'WebGPU Compute Shaders for Audio DSP', 'Graph Neural Networks for Chord Prediction'],
    research: ['Self-Supervised Learning of Musical Structure from Raw Audio', 'Differentiable Digital Signal Processing for Instrument Synthesis'],
  },
  'Physics': {
    topics: ['Psychoacoustics of Guitar Timbre', 'Coupled Oscillator Models of String Vibration', 'Room Acoustics Simulation with FEM', 'Nonlinear Dynamics in Feedback Systems'],
    research: ['Quantum-Inspired Optimization for Audio Source Separation', 'Chaotic Dynamics in Guitar Feedback Loops'],
  },
  'Cognitive Science': {
    topics: ['Music Cognition and Working Memory', 'Motor Learning in Instrument Practice', 'Absolute vs Relative Pitch Processing', 'Flow States in Musical Performance'],
    research: ['Neural Entrainment to Complex Rhythmic Patterns', 'Predictive Processing Models of Musical Expectation'],
  },
  'Philosophy': {
    topics: ['Aesthetics of Dissonance', 'Ethics of AI-Generated Music', 'Phenomenology of Listening', 'Music and Consciousness'],
    research: ['Computational Aesthetics: Can Machines Judge Musical Beauty?', 'The Hard Problem of Musical Qualia'],
  },
  'Cybernetics': {
    topics: ['Ashby\'s Law of Requisite Variety in Software', 'Second-Order Cybernetics for AI Agents', 'Autopoiesis in Self-Governing Systems', 'Feedback Control in Multi-Agent Orchestration'],
    research: ['VSM-Based Governance for Autonomous AI Agent Swarms', 'Algedonic Signal Processing in Neural-Symbolic Hybrid Systems'],
  },
  'Musicology': {
    topics: ['Ethnomusicology of Flamenco Guitar', 'Historical Temperaments and Their Effects', 'Analysis of Bach\'s Counterpoint Techniques', 'Blues Scale Evolution in American Music'],
    research: ['Computational Ethnomusicology: ML Classification of World Guitar Traditions'],
  },
  'Product Management': {
    topics: ['AI-First Product Strategy', 'Developer Experience as Product', 'Metrics That Matter for Open Source', 'Community-Driven Roadmapping'],
    research: ['Measuring Developer Productivity in AI-Augmented Workflows'],
  },
  'Futurology': {
    topics: ['AGI Timeline Analysis', 'Post-Scarcity Music Creation', 'Brain-Computer Interfaces for Music', 'Digital Twins for Musicians'],
    research: ['Forecasting AI Music Generation Quality: When Will AI Compose Better Than Humans?'],
  },
  'Psychohistory': {
    topics: ['Seldon Plan for Software Ecosystems', 'Crisis Prediction in Open Source Projects', 'Mathematical Sociology of Developer Communities'],
    research: ['Statistical Mechanics of Code Repository Evolution'],
  },
  'Guitar Alchemist Academy': {
    topics: ['Your First Barre Chord', 'Reading Standard Notation for Guitar', 'Basic Ear Training', 'Introduction to Music Theory for Guitarists'],
    research: ['Optimal Sequencing of Guitar Pedagogy: A Cognitive Load Theory Approach'],
  },
  'World Music & Languages': {
    topics: ['Bossa Nova Guitar Patterns', 'Celtic Fingerpicking DADGAD', 'Flamenco Rasgueados', 'Turkish Maqam on Guitar', 'Hindustani Raga Adaptation'],
    research: ['Cross-Cultural Transfer Learning for Musical Style Classification'],
  },
  'Audio Engineering': {
    topics: ['Mastering for Streaming Platforms', 'Mid-Side Processing Techniques', 'Convolution Reverb Design', 'Loudness Normalization Standards'],
    research: ['Perceptual Audio Coding: Neural vs Traditional Compression at Ultra-Low Bitrates'],
  },
  'Information Theory': {
    topics: ['Shannon Entropy of Musical Sequences', 'Kolmogorov Complexity of Compositions', 'Mutual Information in Harmonic Progressions'],
    research: ['Information-Theoretic Measures of Musical Creativity and Novelty'],
  },
  'Network Science': {
    topics: ['Scale-Free Networks in Music Collaboration', 'Community Detection in Genre Networks', 'Influence Propagation in Musical Trends'],
    research: ['Temporal Network Analysis of Music Style Evolution Across Decades'],
  },
  'Semiotics': {
    topics: ['Musical Signs and Meaning', 'Icon, Index, Symbol in Music', 'Semiotics of Guitar Tone'],
    research: ['Computational Semiotics: Extracting Meaning from Musical Structure'],
  },
};

function loadQueue<T>(key: string): T[] {
  try {
    const raw = localStorage.getItem(key);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function saveQueue<T>(key: string, items: T[]) {
  localStorage.setItem(key, JSON.stringify(items));
}

const LANGUAGE_LABELS: Record<string, string> = {
  en: 'English',
  es: 'Español',
  fr: 'Français',
  pt: 'Português',
  de: 'Deutsch',
  it: 'Italiano',
};

function estimateEta(type: 'course' | 'research' | 'translation', detail?: string): string {
  if (type === 'course') {
    const mins = detail === 'phd' ? 15 : detail === 'advanced' ? 10 : 5;
    return `~${mins} min`;
  }
  if (type === 'research') {
    const mins = detail === 'frontier' ? 30 : detail === 'deep-dive' ? 20 : detail === 'systematic-review' ? 15 : 8;
    return `~${mins} min`;
  }
  return '~5 min';
}

const AGENT_MODELS = ['Claude Opus', 'Seldon-Plan', 'ChatGPT-4o'] as const;

function pickRandomModel(): string {
  return AGENT_MODELS[Math.floor(Math.random() * AGENT_MODELS.length)];
}

function timeAgo(isoDate: string): string {
  const seconds = Math.floor((Date.now() - new Date(isoDate).getTime()) / 1000);
  if (seconds < 5) return 'just now';
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}

export const CourseViewer: React.FC<CourseViewerProps> = ({ open, onClose }) => {
  const [selectedDept, setSelectedDept] = useState<string | null>(null);
  const [selectedCourse, setSelectedCourse] = useState<Course | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [expandedDepts, setExpandedDepts] = useState<Set<string>>(new Set());
  const [activeLang, setActiveLang] = useState<string>(() => {
    try { return localStorage.getItem('streeling-preferred-language') ?? 'en'; }
    catch { return 'en'; }
  });
  const [langDropdownOpen, setLangDropdownOpen] = useState(false);

  // Escape to close
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onClose]);

  /* --- Queue state --- */
  const [courseQueue, setCourseQueue] = useState<CourseRequest[]>(() => loadQueue(COURSE_QUEUE_KEY));
  const [researchQueue, setResearchQueue] = useState<ResearchRequest[]>(() => loadQueue(RESEARCH_QUEUE_KEY));
  const [translationQueue, setTranslationQueue] = useState<TranslationRequest[]>(() => loadQueue(TRANSLATION_QUEUE_KEY));
  const [, setTick] = useState(0); // force re-render for timeAgo updates

  // Auto-expand queue when pending items exist on mount
  const [queueExpanded, setQueueExpanded] = useState(() => {
    const cq: CourseRequest[] = loadQueue(COURSE_QUEUE_KEY);
    const rq: ResearchRequest[] = loadQueue(RESEARCH_QUEUE_KEY);
    const tq: TranslationRequest[] = loadQueue(TRANSLATION_QUEUE_KEY);
    return cq.some(r => r.status !== 'completed')
      || rq.some(r => r.status !== 'completed')
      || tq.some(r => r.status !== 'completed');
  });

  /* --- Dialog state --- */
  const [dialogKind, setDialogKind] = useState<DialogKind>(null);
  const [dialogDept, setDialogDept] = useState('');

  // Generate Course form
  const [gcTopic, setGcTopic] = useState('');
  const [gcLevel, setGcLevel] = useState<CourseRequest['level']>('introductory');

  // Deep Research form
  const [drTopic, setDrTopic] = useState('');
  const [drDepth, setDrDepth] = useState<ResearchRequest['depth']>('survey');
  const [drSources, setDrSources] = useState<Set<string>>(new Set());

  // Persist queues
  useEffect(() => { saveQueue(COURSE_QUEUE_KEY, courseQueue); }, [courseQueue]);
  useEffect(() => { saveQueue(RESEARCH_QUEUE_KEY, researchQueue); }, [researchQueue]);
  useEffect(() => { saveQueue(TRANSLATION_QUEUE_KEY, translationQueue); }, [translationQueue]);

  // Tick every 10s to keep timeAgo displays fresh
  useEffect(() => {
    const id = setInterval(() => setTick(t => t + 1), 10_000);
    return () => clearInterval(id);
  }, []);

  // Status simulation: advance queued items through stages
  const simTimers = useRef<Set<ReturnType<typeof setTimeout>>>(new Set());
  useEffect(() => {
    return () => { simTimers.current.forEach(clearTimeout); };
  }, []);

  const scheduleSimulation = useCallback((type: 'course' | 'research' | 'translation', id: string) => {
    // Phase 1: after 2s, assign agent and set in-progress status
    const t1 = setTimeout(() => {
      const model = pickRandomModel();
      if (type === 'course') {
        setCourseQueue(prev => prev.map(r => r.id === id && r.status === 'queued'
          ? { ...r, status: 'generating', assignedTo: model, progress: 5 } : r));
      } else if (type === 'research') {
        setResearchQueue(prev => prev.map(r => r.id === id && r.status === 'queued'
          ? { ...r, status: 'researching', assignedTo: model, progress: 5 } : r));
      } else {
        const [, cid, lang] = id.split('::');
        setTranslationQueue(prev => prev.map(r => r.courseId === cid && r.targetLanguage === lang && r.status === 'queued'
          ? { ...r, status: 'in_progress', assignedTo: model, progress: 5 } : r));
      }
    }, 2000);
    simTimers.current.add(t1);

    // Phase 2: progress ticks every 3s
    const progressTicks = [20, 38, 55, 72, 88, 100];
    progressTicks.forEach((pct, i) => {
      const t = setTimeout(() => {
        const advance = <T extends { status: string; progress?: number }>(r: T, activeStatuses: string[]): T => {
          if (r.status === 'completed' || !activeStatuses.includes(r.status)) return r;
          const next = { ...r, progress: pct };
          if (pct >= 100) (next as any).status = 'completed';
          return next;
        };
        if (type === 'course') {
          setCourseQueue(prev => prev.map(r => r.id === id ? advance(r, ['generating', 'queued']) : r));
        } else if (type === 'research') {
          setResearchQueue(prev => prev.map(r => r.id === id ? advance(r, ['researching', 'queued']) : r));
        } else {
          const [, cid, lang] = id.split('::');
          setTranslationQueue(prev => prev.map(r =>
            r.courseId === cid && r.targetLanguage === lang ? advance(r, ['in_progress', 'queued']) : r
          ));
        }
      }, 2000 + 3000 * (i + 1));
      simTimers.current.add(t);
    });
  }, []);

  // Rescue stuck items: on mount, re-schedule simulation for any items still 'queued'
  const simRescuedRef = useRef(false);
  useEffect(() => {
    if (simRescuedRef.current) return;
    simRescuedRef.current = true;
    courseQueue.filter(r => r.status === 'queued').forEach(r => scheduleSimulation('course', r.id));
    researchQueue.filter(r => r.status === 'queued').forEach(r => scheduleSimulation('research', r.id));
    translationQueue.filter(r => r.status === 'queued').forEach(r =>
      scheduleSimulation('translation', `tq::${r.courseId}::${r.targetLanguage}`)
    );
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Cancel helpers
  const cancelCourseItem = useCallback((id: string) => {
    setCourseQueue(prev => prev.filter(r => r.id !== id));
  }, []);
  const cancelResearchItem = useCallback((id: string) => {
    setResearchQueue(prev => prev.filter(r => r.id !== id));
  }, []);
  const cancelTranslationItem = useCallback((courseId: string, lang: string) => {
    setTranslationQueue(prev => prev.filter(r => !(r.courseId === courseId && r.targetLanguage === lang)));
  }, []);

  // Persist preferred language globally
  useEffect(() => {
    try { localStorage.setItem('streeling-preferred-language', activeLang); }
    catch { /* ignore */ }
  }, [activeLang]);

  // Close language dropdown on outside click
  useEffect(() => {
    if (!langDropdownOpen) return;
    const handler = (e: MouseEvent) => {
      const target = e.target as HTMLElement;
      if (!target.closest('.course-viewer__global-lang')) {
        setLangDropdownOpen(false);
      }
    };
    document.addEventListener('click', handler, true);
    return () => document.removeEventListener('click', handler, true);
  }, [langDropdownOpen]);

  const openDialog = useCallback((kind: DialogKind, deptName: string) => {
    setDialogKind(kind);
    setDialogDept(deptName);
    setGcTopic('');
    setGcLevel('introductory');
    setDrTopic('');
    setDrDepth('survey');
    setDrSources(new Set());
  }, []);

  const closeDialog = useCallback(() => setDialogKind(null), []);

  const submitCourseRequest = useCallback(() => {
    if (!gcTopic.trim()) return;
    const req: CourseRequest = {
      id: `cq-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      department: dialogDept,
      topic: gcTopic.trim(),
      level: gcLevel,
      requestedAt: new Date().toISOString(),
      status: 'queued',
      assignedTo: undefined,
      eta: estimateEta('course', gcLevel),
      progress: 0,
    };
    setCourseQueue(prev => [...prev, req]);
    setQueueExpanded(true);
    scheduleSimulation('course', req.id);
    closeDialog();
  }, [gcTopic, gcLevel, dialogDept, closeDialog, scheduleSimulation]);

  const submitResearchRequest = useCallback(() => {
    if (!drTopic.trim()) return;
    const req: ResearchRequest = {
      id: `rq-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
      department: dialogDept,
      topic: drTopic.trim(),
      depth: drDepth,
      sources: Array.from(drSources),
      requestedAt: new Date().toISOString(),
      status: 'queued',
      assignedTo: undefined,
      eta: estimateEta('research', drDepth),
      progress: 0,
    };
    setResearchQueue(prev => [...prev, req]);
    closeDialog();
  }, [drTopic, drDepth, drSources, dialogDept, closeDialog]);

  const toggleSource = useCallback((src: string) => {
    setDrSources(prev => {
      const next = new Set(prev);
      if (next.has(src)) next.delete(src); else next.add(src);
      return next;
    });
  }, []);

  const queueTranslation = useCallback((course: Course, lang: string) => {
    const already = translationQueue.some(
      t => t.courseId === course.id && t.targetLanguage === lang
    );
    if (already) return;
    const req: TranslationRequest = {
      courseId: course.id,
      department: course.department,
      targetLanguage: lang,
      requestedAt: new Date().toISOString(),
      status: 'queued',
    };
    setTranslationQueue(prev => [...prev, req]);
  }, [translationQueue]);

  const isTranslationQueued = useCallback((courseId: string, lang: string) => {
    return translationQueue.some(t => t.courseId === courseId && t.targetLanguage === lang);
  }, [translationQueue]);

  const totalPending = courseQueue.filter(r => r.status !== 'completed').length
    + researchQueue.filter(r => r.status !== 'completed').length
    + translationQueue.filter(r => r.status !== 'completed').length;

  const pendingByDept = useMemo(() => {
    const map: Record<string, CourseRequest[]> = {};
    for (const r of courseQueue) {
      if (r.status === 'completed') continue;
      (map[r.department] ??= []).push(r);
    }
    return map;
  }, [courseQueue]);

  const toggleDept = useCallback((deptId: string) => {
    setExpandedDepts(prev => {
      const next = new Set(prev);
      if (next.has(deptId)) {
        next.delete(deptId);
      } else {
        next.add(deptId);
      }
      return next;
    });
    setSelectedDept(deptId);
  }, []);

  const selectCourse = useCallback((course: Course) => {
    setSelectedCourse(course);
  }, []);

  const filteredDepartments = useMemo(() => {
    if (!searchQuery.trim()) return departments;
    const q = searchQuery.toLowerCase();
    return departments
      .map(dept => ({
        ...dept,
        courses: dept.courses.filter(
          c => c.title.toLowerCase().includes(q) || c.content.toLowerCase().includes(q)
        ),
      }))
      .filter(dept => dept.courses.length > 0 || dept.name.toLowerCase().includes(q));
  }, [searchQuery]);

  // Strip YAML frontmatter (---...\n---) from markdown content
  const stripFrontmatter = useCallback((text: string): string => {
    const match = text.match(/^---\r?\n[\s\S]*?\r?\n---\r?\n?/);
    return match ? text.slice(match[0].length) : text;
  }, []);

  // Resolve displayed content based on active language
  const displayedContent = useMemo(() => {
    if (!selectedCourse) return null;
    if (activeLang === 'en') {
      return { title: selectedCourse.title, content: stripFrontmatter(selectedCourse.content) };
    }
    const t = selectedCourse.translations[activeLang];
    if (t) return { title: t.title, content: stripFrontmatter(t.content) };
    return { title: selectedCourse.title, content: stripFrontmatter(selectedCourse.content) };
  }, [selectedCourse, activeLang, stripFrontmatter]);

  if (!open) return null;

  return (
    <div className="course-viewer__overlay" onClick={onClose}>
      <div className="course-viewer" onClick={e => e.stopPropagation()}>
        {/* Header */}
        <div className="course-viewer__header">
          <div className="course-viewer__header-left">
            <svg className="course-viewer__logo" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="#FFD700" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
              <path d="M22 10v6M2 10l10-5 10 5-10 5z" />
              <path d="M6 12v5c0 1.66 2.69 3 6 3s6-1.34 6-3v-5" />
            </svg>
            <div>
              <h2 className="course-viewer__title">Streeling University</h2>
              <span className="course-viewer__subtitle">
                {totalDepartments} departments &middot; {totalCourses} courses
              </span>
            </div>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            {totalPending > 0 && (
              <button
                onClick={() => setQueueExpanded(true)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                  padding: '4px 10px',
                  background: 'rgba(255, 179, 0, 0.1)',
                  border: '1px solid rgba(255, 179, 0, 0.3)',
                  borderRadius: 6,
                  color: '#FFB300',
                  fontFamily: "'JetBrains Mono', monospace",
                  fontSize: '11px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  transition: 'all 0.15s',
                }}
                title="View pending queue"
              >
                <span style={{ fontSize: 13 }}>⏳</span>
                {totalPending} queued
              </button>
            )}
            {/* Global language selector */}
            <div className="course-viewer__global-lang" style={{ position: 'relative' }}>
              <button
                onClick={() => setLangDropdownOpen(prev => !prev)}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 5,
                  padding: '4px 10px',
                  background: langDropdownOpen ? 'rgba(255, 215, 0, 0.15)' : 'rgba(255, 215, 0, 0.06)',
                  border: '1px solid rgba(255, 215, 0, 0.25)',
                  borderRadius: 6,
                  color: '#FFD700',
                  fontFamily: "'JetBrains Mono', monospace",
                  fontSize: '11px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  transition: 'all 0.15s',
                }}
                title="Change preferred language"
                aria-label="Change preferred language"
                aria-expanded={langDropdownOpen}
              >
                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="10" />
                  <path d="M2 12h20" />
                  <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
                </svg>
                {activeLang.toUpperCase()}
              </button>
              {langDropdownOpen && (
                <div
                  style={{
                    position: 'absolute',
                    top: 'calc(100% + 6px)',
                    right: 0,
                    background: '#1a1a2e',
                    border: '1px solid rgba(255, 215, 0, 0.2)',
                    borderRadius: 8,
                    padding: '4px 0',
                    minWidth: 140,
                    zIndex: 100,
                    boxShadow: '0 8px 24px rgba(0, 0, 0, 0.5)',
                  }}
                >
                  {ALL_LANGUAGES.map(lang => (
                    <button
                      key={lang}
                      onClick={() => { setActiveLang(lang); setLangDropdownOpen(false); }}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: 8,
                        width: '100%',
                        padding: '6px 12px',
                        background: activeLang === lang ? 'rgba(255, 215, 0, 0.12)' : 'transparent',
                        border: 'none',
                        color: activeLang === lang ? '#FFD700' : '#c9d1d9',
                        fontFamily: "'JetBrains Mono', monospace",
                        fontSize: '11px',
                        fontWeight: activeLang === lang ? 700 : 400,
                        cursor: 'pointer',
                        textAlign: 'left',
                        transition: 'background 0.1s',
                      }}
                      onMouseEnter={e => { (e.currentTarget as HTMLButtonElement).style.background = 'rgba(255, 215, 0, 0.08)'; }}
                      onMouseLeave={e => { (e.currentTarget as HTMLButtonElement).style.background = activeLang === lang ? 'rgba(255, 215, 0, 0.12)' : 'transparent'; }}
                    >
                      <span style={{ width: 22, fontWeight: 700 }}>{lang.toUpperCase()}</span>
                      <span>{LANGUAGE_LABELS[lang]}</span>
                      {activeLang === lang && <span style={{ marginLeft: 'auto', fontSize: 10, opacity: 0.7 }}>&#10003;</span>}
                    </button>
                  ))}
                </div>
              )}
            </div>
            <button className="course-viewer__close" onClick={onClose} aria-label="Close course viewer">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </div>
        </div>

        {/* Search */}
        <div className="course-viewer__search-bar">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#8b949e" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="11" cy="11" r="8" />
            <line x1="21" y1="21" x2="16.65" y2="16.65" />
          </svg>
          <input
            className="course-viewer__search-input"
            type="text"
            placeholder="Search courses by title or content..."
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
            autoFocus
          />
          {searchQuery && (
            <button className="course-viewer__search-clear" onClick={() => setSearchQuery('')}>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          )}
        </div>

        <div className="course-viewer__body">
          {/* Sidebar */}
          <nav className="course-viewer__sidebar">
            {filteredDepartments.map(dept => (
              <div key={dept.id} className="course-viewer__dept">
                <div className="course-viewer__dept-header">
                  <button
                    className={`course-viewer__dept-btn ${selectedDept === dept.id ? 'course-viewer__dept-btn--active' : ''}`}
                    onClick={() => toggleDept(dept.id)}
                  >
                    <svg
                      className={`course-viewer__dept-chevron ${expandedDepts.has(dept.id) ? 'course-viewer__dept-chevron--open' : ''}`}
                      width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
                    >
                      <polyline points="9 18 15 12 9 6" />
                    </svg>
                    <span className="course-viewer__dept-name">{dept.name}</span>
                    <span className="course-viewer__dept-badge">{dept.courses.length}</span>
                  </button>
                  {expandedDepts.has(dept.id) && (
                    <div className="course-viewer__dept-actions">
                      <button
                        className="course-viewer__action-btn course-viewer__action-btn--generate"
                        title="Generate Course"
                        onClick={e => { e.stopPropagation(); openDialog('generate-course', dept.name); }}
                      >
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
                          <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
                        </svg>
                        Course
                      </button>
                      <button
                        className="course-viewer__action-btn course-viewer__action-btn--research"
                        title="Deep Research"
                        onClick={e => { e.stopPropagation(); openDialog('deep-research', dept.name); }}
                      >
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                          <circle cx="11" cy="11" r="8" /><line x1="21" y1="21" x2="16.65" y2="16.65" />
                        </svg>
                        Research
                      </button>
                    </div>
                  )}
                </div>
                {expandedDepts.has(dept.id) && (
                  <div className="course-viewer__course-list">
                    {/* Pending course requests */}
                    {(pendingByDept[dept.name] ?? []).map(req => (
                      <div key={req.id} className="course-viewer__pending-item">
                        <span className={`course-viewer__status-dot course-viewer__status-dot--${req.status === 'queued' ? 'amber' : req.status === 'generating' ? 'blue' : 'green'}`} />
                        <span className="course-viewer__pending-label">{req.topic}</span>
                        <span className="course-viewer__pending-status">
                          {req.status === 'queued' ? 'Queued' : req.status === 'generating' ? 'Generating...' : 'Done'}
                        </span>
                      </div>
                    ))}
                    {dept.courses.map(course => (
                      <button
                        key={course.id}
                        className={`course-viewer__course-btn ${selectedCourse?.id === course.id ? 'course-viewer__course-btn--active' : ''}`}
                        onClick={() => selectCourse(course)}
                      >
                        {course.title}
                      </button>
                    ))}
                  </div>
                )}
              </div>
            ))}

            {/* Queue dashboard */}
            <div className="course-viewer__queue-section">
              <button
                className="course-viewer__queue-header"
                onClick={() => setQueueExpanded(prev => !prev)}
              >
                <svg
                  className={`course-viewer__dept-chevron ${queueExpanded ? 'course-viewer__dept-chevron--open' : ''}`}
                  width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
                >
                  <polyline points="9 18 15 12 9 6" />
                </svg>
                <span className="course-viewer__dept-name">Queue</span>
                {totalPending > 0 && (
                  <span className="course-viewer__queue-badge">{totalPending}</span>
                )}
              </button>
              {queueExpanded && (
                <div className="course-viewer__queue-list">
                  {courseQueue.length === 0 && researchQueue.length === 0 && translationQueue.length === 0 && (
                    <div className="course-viewer__queue-empty">No pending requests</div>
                  )}
                  {courseQueue.length > 0 && (
                    <>
                      <div className="course-viewer__queue-group-title">Course Generation</div>
                      {courseQueue.map(req => (
                        <div key={req.id} className="course-viewer__queue-item">
                          <span className={`course-viewer__status-dot course-viewer__status-dot--${req.status === 'queued' ? 'amber' : req.status === 'generating' ? 'blue' : 'green'}`} />
                          <div className="course-viewer__queue-item-info">
                            <span className="course-viewer__queue-item-topic">{req.topic}</span>
                            <span className="course-viewer__queue-item-meta">{req.department} &middot; {req.level}</span>
                            <span className="course-viewer__queue-item-agent">
                              {req.assignedTo ? `Agent: ${req.assignedTo}` : 'Awaiting assignment'}
                              {req.eta && <> &middot; ETA: {req.eta}</>}
                            </span>
                            {req.progress != null && req.progress > 0 && (
                              <div className="course-viewer__queue-progress">
                                <div className="course-viewer__queue-progress-bar" style={{ width: `${req.progress}%` }} />
                              </div>
                            )}
                          </div>
                        </div>
                      ))}
                    </>
                  )}
                  {researchQueue.length > 0 && (
                    <>
                      <div className="course-viewer__queue-group-title">Research</div>
                      {researchQueue.map(req => (
                        <div key={req.id} className="course-viewer__queue-item">
                          <span className={`course-viewer__status-dot course-viewer__status-dot--${req.status === 'queued' ? 'amber' : req.status === 'researching' ? 'blue' : 'green'}`} />
                          <div className="course-viewer__queue-item-info">
                            <span className="course-viewer__queue-item-topic">{req.topic}</span>
                            <span className="course-viewer__queue-item-meta">{req.department} &middot; {req.depth}</span>
                            <span className="course-viewer__queue-item-agent">
                              {req.assignedTo ? `Agent: ${req.assignedTo}` : 'Awaiting assignment'}
                              {req.eta && <> &middot; ETA: {req.eta}</>}
                            </span>
                            {req.progress != null && req.progress > 0 && (
                              <div className="course-viewer__queue-progress">
                                <div className="course-viewer__queue-progress-bar" style={{ width: `${req.progress}%` }} />
                              </div>
                            )}
                          </div>
                        </div>
                      ))}
                    </>
                  )}
                  {translationQueue.length > 0 && (
                    <>
                      <div className="course-viewer__queue-group-title">Translations</div>
                      {translationQueue.map((req, i) => (
                        <div key={`tq-${i}`} className="course-viewer__queue-item">
                          <span className={`course-viewer__status-dot course-viewer__status-dot--${req.status === 'queued' ? 'amber' : req.status === 'in_progress' ? 'blue' : 'green'}`} />
                          <div className="course-viewer__queue-item-info">
                            <span className="course-viewer__queue-item-topic">{req.courseId}</span>
                            <span className="course-viewer__queue-item-meta">
                              {req.department} &middot; {LANGUAGE_LABELS[req.targetLanguage] ?? req.targetLanguage}
                            </span>
                            <span className="course-viewer__queue-item-agent">
                              {req.assignedTo ? `Agent: ${req.assignedTo}` : 'Awaiting assignment'}
                              {req.eta && <> &middot; ETA: {req.eta}</>}
                            </span>
                            {req.progress != null && req.progress > 0 && (
                              <div className="course-viewer__queue-progress">
                                <div className="course-viewer__queue-progress-bar" style={{ width: `${req.progress}%` }} />
                              </div>
                            )}
                          </div>
                        </div>
                      ))}
                    </>
                  )}
                </div>
              )}
            </div>
          </nav>

          {/* Content area */}
          <div className="course-viewer__content">
            {selectedCourse && displayedContent ? (
              <div className="course-viewer__reader">
                <div className="course-viewer__reader-meta">
                  <span className="course-viewer__reader-dept">
                    {departments.find(d => d.id === selectedCourse.department)?.name}
                  </span>
                </div>
                {/* Language switcher bar */}
                <div className="course-viewer__lang-bar">
                  {ALL_LANGUAGES.map(lang => {
                    const available = selectedCourse.availableLanguages.includes(lang);
                    const queued = isTranslationQueued(selectedCourse.id, lang);
                    const isActive = activeLang === lang;
                    return (
                      <button
                        key={lang}
                        className={[
                          'course-viewer__lang-btn',
                          isActive ? 'course-viewer__lang-btn--active' : '',
                          !available ? 'course-viewer__lang-btn--disabled' : '',
                        ].join(' ')}
                        onClick={() => { if (available) setActiveLang(lang); }}
                        disabled={!available}
                        title={
                          available
                            ? LANGUAGE_LABELS[lang] ?? lang
                            : queued
                              ? `${LANGUAGE_LABELS[lang]} — Queued`
                              : `${LANGUAGE_LABELS[lang]} — Not available`
                        }
                      >
                        {lang.toUpperCase()}
                        {queued && !available && (
                          <span className="course-viewer__lang-queued-dot" />
                        )}
                      </button>
                    );
                  })}
                  {/* Request Translation dropdown for missing languages */}
                  {(() => {
                    const missingLangs = ALL_LANGUAGES.filter(
                      l => !selectedCourse.availableLanguages.includes(l) && !isTranslationQueued(selectedCourse.id, l)
                    );
                    if (missingLangs.length === 0) return null;
                    return (
                      <div className="course-viewer__translate-menu">
                        <button className="course-viewer__translate-btn">
                          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round">
                            <path d="M5 8l6 6" /><path d="M4 14l6-6 2-3" /><path d="M2 5h12" /><path d="M7 2v3" />
                            <path d="M22 22l-5-10-5 10" /><path d="M14 18h6" />
                          </svg>
                          Translate
                        </button>
                        <div className="course-viewer__translate-dropdown">
                          {missingLangs.map(lang => (
                            <button
                              key={lang}
                              className="course-viewer__translate-option"
                              onClick={() => queueTranslation(selectedCourse, lang)}
                            >
                              {lang.toUpperCase()} &mdash; {LANGUAGE_LABELS[lang]}
                            </button>
                          ))}
                        </div>
                      </div>
                    );
                  })()}
                </div>
                <article className="course-viewer__markdown">
                  <Markdown>{displayedContent.content}</Markdown>
                </article>
              </div>
            ) : (
              <div className="course-viewer__empty">
                <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="#FFD70066" strokeWidth="1" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M22 10v6M2 10l10-5 10 5-10 5z" />
                  <path d="M6 12v5c0 1.66 2.69 3 6 3s6-1.34 6-3v-5" />
                </svg>
                <p className="course-viewer__empty-title">Select a course</p>
                <p className="course-viewer__empty-hint">
                  Browse {totalDepartments} departments and {totalCourses} courses from Streeling University
                </p>
              </div>
            )}
          </div>
        </div>
        {/* Generate Course dialog */}
        {dialogKind === 'generate-course' && (
          <div className="course-viewer__dialog-overlay" onClick={closeDialog}>
            <div className="course-viewer__dialog" onClick={e => e.stopPropagation()}>
              <h3 className="course-viewer__dialog-title">Generate Course</h3>
              <label className="course-viewer__field">
                <span className="course-viewer__field-label">Department</span>
                <input className="course-viewer__field-input" type="text" value={dialogDept} readOnly />
              </label>
              <label className="course-viewer__field">
                <span className="course-viewer__field-label">Topic</span>
                <input
                  className="course-viewer__field-input"
                  type="text"
                  placeholder="e.g. Advanced Harmonic Analysis"
                  value={gcTopic}
                  onChange={e => setGcTopic(e.target.value)}
                  autoFocus
                />
              </label>
              {/* AI topic suggestions */}
              {AI_SUGGESTIONS[dialogDept]?.topics && (
                <div className="course-viewer__suggestions">
                  <span className="course-viewer__suggestions-label">AI Suggestions</span>
                  <div className="course-viewer__suggestions-list">
                    {AI_SUGGESTIONS[dialogDept].topics.map(topic => (
                      <button
                        key={topic}
                        className="course-viewer__suggestion-chip"
                        onClick={() => setGcTopic(topic)}
                      >
                        {topic}
                      </button>
                    ))}
                  </div>
                </div>
              )}
              <label className="course-viewer__field">
                <span className="course-viewer__field-label">Level</span>
                <select
                  className="course-viewer__field-select"
                  value={gcLevel}
                  onChange={e => setGcLevel(e.target.value as CourseRequest['level'])}
                >
                  {LEVEL_OPTIONS.map(o => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
              </label>
              <div className="course-viewer__dialog-actions">
                <button className="course-viewer__dialog-cancel" onClick={closeDialog}>Cancel</button>
                <button
                  className="course-viewer__dialog-submit"
                  onClick={submitCourseRequest}
                  disabled={!gcTopic.trim()}
                >
                  Generate
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Deep Research dialog */}
        {dialogKind === 'deep-research' && (
          <div className="course-viewer__dialog-overlay" onClick={closeDialog}>
            <div className="course-viewer__dialog course-viewer__dialog--wide" onClick={e => e.stopPropagation()}>
              <h3 className="course-viewer__dialog-title">Deep Research</h3>
              <label className="course-viewer__field">
                <span className="course-viewer__field-label">Department</span>
                <input className="course-viewer__field-input" type="text" value={dialogDept} readOnly />
              </label>
              <label className="course-viewer__field">
                <span className="course-viewer__field-label">Research Question / Topic</span>
                <textarea
                  className="course-viewer__field-textarea"
                  placeholder="e.g. What are the latest advances in neural audio synthesis for guitar timbres?"
                  value={drTopic}
                  onChange={e => setDrTopic(e.target.value)}
                  rows={4}
                  autoFocus
                />
              </label>
              {/* AI research suggestions */}
              {AI_SUGGESTIONS[dialogDept]?.research && (
                <div className="course-viewer__suggestions">
                  <span className="course-viewer__suggestions-label">AI Suggestions (PhD-level)</span>
                  <div className="course-viewer__suggestions-list">
                    {AI_SUGGESTIONS[dialogDept].research.map(topic => (
                      <button
                        key={topic}
                        className="course-viewer__suggestion-chip course-viewer__suggestion-chip--research"
                        onClick={() => setDrTopic(topic)}
                      >
                        {topic}
                      </button>
                    ))}
                  </div>
                </div>
              )}
              <label className="course-viewer__field">
                <span className="course-viewer__field-label">Depth</span>
                <select
                  className="course-viewer__field-select"
                  value={drDepth}
                  onChange={e => setDrDepth(e.target.value as ResearchRequest['depth'])}
                >
                  {DEPTH_OPTIONS.map(o => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
              </label>
              <fieldset className="course-viewer__field course-viewer__fieldset">
                <legend className="course-viewer__field-label">Sources</legend>
                <div className="course-viewer__checkbox-group">
                  {SOURCE_OPTIONS.map(src => (
                    <label key={src} className="course-viewer__checkbox-label">
                      <input
                        type="checkbox"
                        checked={drSources.has(src)}
                        onChange={() => toggleSource(src)}
                        className="course-viewer__checkbox"
                      />
                      {src}
                    </label>
                  ))}
                </div>
              </fieldset>
              <div className="course-viewer__dialog-actions">
                <button className="course-viewer__dialog-cancel" onClick={closeDialog}>Cancel</button>
                <button
                  className="course-viewer__dialog-submit"
                  onClick={submitResearchRequest}
                  disabled={!drTopic.trim()}
                >
                  Start Research
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
