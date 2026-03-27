// src/components/PrimeRadiant/CourseViewer.tsx
// Streeling University course browser — embedded in Prime Radiant as a full-screen modal

import React, { useState, useMemo, useCallback, useEffect } from 'react';
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
}

interface ResearchRequest {
  id: string;
  department: string;
  topic: string;
  depth: 'survey' | 'systematic-review' | 'deep-dive' | 'frontier';
  sources: string[];
  requestedAt: string;
  status: 'queued' | 'researching' | 'completed';
}

interface TranslationRequest {
  courseId: string;
  department: string;
  targetLanguage: string;
  requestedAt: string;
  status: 'queued' | 'in_progress' | 'completed';
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

export const CourseViewer: React.FC<CourseViewerProps> = ({ open, onClose }) => {
  const [selectedDept, setSelectedDept] = useState<string | null>(null);
  const [selectedCourse, setSelectedCourse] = useState<Course | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [expandedDepts, setExpandedDepts] = useState<Set<string>>(new Set());
  const [activeLang, setActiveLang] = useState<string>('en');

  /* --- Queue state --- */
  const [courseQueue, setCourseQueue] = useState<CourseRequest[]>(() => loadQueue(COURSE_QUEUE_KEY));
  const [researchQueue, setResearchQueue] = useState<ResearchRequest[]>(() => loadQueue(RESEARCH_QUEUE_KEY));
  const [translationQueue, setTranslationQueue] = useState<TranslationRequest[]>(() => loadQueue(TRANSLATION_QUEUE_KEY));
  const [queueExpanded, setQueueExpanded] = useState(false);

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

  // Reset language when course changes
  useEffect(() => { setActiveLang('en'); }, [selectedCourse?.id]);

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
    };
    setCourseQueue(prev => [...prev, req]);
    closeDialog();
  }, [gcTopic, gcLevel, dialogDept, closeDialog]);

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

  // Resolve displayed content based on active language
  const displayedContent = useMemo(() => {
    if (!selectedCourse) return null;
    if (activeLang === 'en') {
      return { title: selectedCourse.title, content: selectedCourse.content };
    }
    const t = selectedCourse.translations[activeLang];
    if (t) return { title: t.title, content: t.content };
    return { title: selectedCourse.title, content: selectedCourse.content };
  }, [selectedCourse, activeLang]);

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
          <button className="course-viewer__close" onClick={onClose} aria-label="Close course viewer">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
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
