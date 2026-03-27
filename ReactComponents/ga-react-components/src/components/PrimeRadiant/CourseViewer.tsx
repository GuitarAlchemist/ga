// src/components/PrimeRadiant/CourseViewer.tsx
// Streeling University course browser — embedded in Prime Radiant as a full-screen modal

import React, { useState, useMemo, useCallback } from 'react';
import Markdown from 'react-markdown';
import { departments, totalCourses, totalDepartments, type Course, type Department } from './courseData';

interface CourseViewerProps {
  open: boolean;
  onClose: () => void;
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
                {expandedDepts.has(dept.id) && dept.courses.length > 0 && (
                  <div className="course-viewer__course-list">
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
          </nav>

          {/* Content area */}
          <div className="course-viewer__content">
            {selectedCourse ? (
              <div className="course-viewer__reader">
                <div className="course-viewer__reader-meta">
                  <span className="course-viewer__reader-dept">
                    {departments.find(d => d.id === selectedCourse.department)?.name}
                  </span>
                  {(() => {
                    const dept = departments.find(d => d.id === selectedCourse.department);
                    if (!dept || dept.languages.length <= 1) return null;
                    return (
                      <span className="course-viewer__reader-langs">
                        {dept.languages.map(lang => (
                          <span
                            key={lang}
                            className={`course-viewer__lang-tag ${lang === 'en' ? 'course-viewer__lang-tag--active' : ''}`}
                            title={LANGUAGE_LABELS[lang] ?? lang}
                          >
                            {lang.toUpperCase()}
                          </span>
                        ))}
                      </span>
                    );
                  })()}
                </div>
                <article className="course-viewer__markdown">
                  <Markdown>{selectedCourse.content}</Markdown>
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
      </div>
    </div>
  );
};
