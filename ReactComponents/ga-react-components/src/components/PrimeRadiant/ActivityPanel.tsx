// src/components/PrimeRadiant/ActivityPanel.tsx
// Top-left activity feed showing main tasks, progress, and ETAs

import React, { useEffect, useState } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
export interface Activity {
  id: string;
  name: string;
  status: 'active' | 'pending' | 'completed' | 'blocked';
  progress: number;    // 0-100
  eta?: string;        // e.g. "~5m", "~2h", "done"
  category: 'governance' | 'research' | 'build' | 'test' | 'deploy';
}

// ---------------------------------------------------------------------------
// Mock activities — TODO: connect to real task system (GitHub Projects, ix)
// ---------------------------------------------------------------------------
function getMockActivities(): Activity[] {
  return [
    { id: '1', name: 'Hexavalent logic schemas', status: 'completed', progress: 100, eta: 'done', category: 'governance' },
    { id: '2', name: 'Prime Radiant v4', status: 'active', progress: 78, eta: '~30m', category: 'build' },
    { id: '3', name: 'ix GPU lattice ops', status: 'pending', progress: 0, eta: '~2h', category: 'build' },
    { id: '4', name: 'Red team cycle 004', status: 'pending', progress: 0, eta: '~1h', category: 'test' },
    { id: '5', name: 'Seldon research — 6-valued logic', status: 'active', progress: 45, eta: '~45m', category: 'research' },
    { id: '6', name: 'DNS propagation', status: 'blocked', progress: 0, eta: 'waiting', category: 'deploy' },
  ];
}

// ---------------------------------------------------------------------------
// Status colors
// ---------------------------------------------------------------------------
const STATUS_COLOR: Record<Activity['status'], string> = {
  active: '#33CC66',
  pending: '#888888',
  completed: '#33CC66',
  blocked: '#FF4444',
};

const CATEGORY_ICON: Record<Activity['category'], string> = {
  governance: 'G',
  research: 'R',
  build: 'B',
  test: 'T',
  deploy: 'D',
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const ActivityPanel: React.FC = () => {
  const [activities, setActivities] = useState<Activity[]>([]);
  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    setActivities(getMockActivities());
    // TODO: poll real task source every 30s
  }, []);

  const active = activities.filter((a) => a.status === 'active');
  const pending = activities.filter((a) => a.status === 'pending' || a.status === 'blocked');
  const completed = activities.filter((a) => a.status === 'completed');

  return (
    <div className="prime-radiant__activity">
      <div
        className="prime-radiant__activity-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__activity-title">
          Activities
          <span className="prime-radiant__activity-count">
            {active.length} active · {pending.length} queued
          </span>
        </span>
        <span className="prime-radiant__activity-toggle">
          {collapsed ? '▶' : '▼'}
        </span>
      </div>

      {!collapsed && (
        <div className="prime-radiant__activity-list">
          {activities.map((a) => (
            <div key={a.id} className="prime-radiant__activity-item">
              <span
                className="prime-radiant__activity-cat"
                style={{ color: STATUS_COLOR[a.status] }}
              >
                {CATEGORY_ICON[a.category]}
              </span>
              <div className="prime-radiant__activity-info">
                <div className="prime-radiant__activity-name" style={{
                  color: a.status === 'completed' ? '#6b7280' : '#c9d1d9',
                  textDecoration: a.status === 'completed' ? 'line-through' : 'none',
                }}>
                  {a.name}
                </div>
                {a.status !== 'completed' && (
                  <div className="prime-radiant__activity-bar">
                    <div
                      className="prime-radiant__activity-fill"
                      style={{
                        width: `${a.progress}%`,
                        backgroundColor: STATUS_COLOR[a.status],
                      }}
                    />
                  </div>
                )}
              </div>
              <span className="prime-radiant__activity-eta" style={{
                color: a.status === 'blocked' ? '#FF4444' : '#6b7280',
              }}>
                {a.eta}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
