// src/components/PrimeRadiant/CommitTooltip.tsx
// Hover card for commits — shows diff stats, files changed, author
// Fetches from GitHub API on hover (with debounce)

import React, { useEffect, useState, useRef } from 'react';

interface CommitDetail {
  sha: string;
  message: string;
  author: string;
  authorAvatar?: string;
  date: string;
  stats?: {
    additions: number;
    deletions: number;
    total: number;
  };
  files?: { filename: string; status: string; additions: number; deletions: number }[];
}

const GITHUB_API = 'https://api.github.com';
const GITHUB_OWNER = 'GuitarAlchemist';

// Cache to avoid re-fetching
const commitCache = new Map<string, CommitDetail>();

async function fetchCommitDetail(repo: string, sha: string): Promise<CommitDetail | null> {
  const cacheKey = `${repo}/${sha}`;
  if (commitCache.has(cacheKey)) return commitCache.get(cacheKey)!;

  try {
    const token = typeof import.meta !== 'undefined'
      ? (import.meta as { env?: Record<string, string> }).env?.VITE_GITHUB_TOKEN
      : undefined;
    const localToken = (() => { try { return localStorage.getItem('ga-github-token'); } catch { return null; } })();
    const authToken = token || localToken;

    const headers: Record<string, string> = { 'Accept': 'application/vnd.github.v3+json' };
    if (authToken) headers['Authorization'] = `Bearer ${authToken}`;

    const res = await fetch(`${GITHUB_API}/repos/${GITHUB_OWNER}/${repo}/commits/${sha}`, { headers });
    if (!res.ok) return null;

    const data = await res.json();
    const detail: CommitDetail = {
      sha: data.sha?.substring(0, 7) ?? sha.substring(0, 7),
      message: data.commit?.message ?? '',
      author: data.commit?.author?.name ?? data.author?.login ?? 'unknown',
      authorAvatar: data.author?.avatar_url,
      date: data.commit?.author?.date ?? '',
      stats: data.stats ? {
        additions: data.stats.additions ?? 0,
        deletions: data.stats.deletions ?? 0,
        total: data.stats.total ?? 0,
      } : undefined,
      files: data.files?.slice(0, 8).map((f: { filename: string; status: string; additions: number; deletions: number }) => ({
        filename: f.filename,
        status: f.status,
        additions: f.additions,
        deletions: f.deletions,
      })),
    };

    commitCache.set(cacheKey, detail);
    return detail;
  } catch {
    return null;
  }
}

interface CommitTooltipProps {
  repo: string;
  sha: string;
  x: number;
  y: number;
  onClose: () => void;
}

export const CommitTooltip: React.FC<CommitTooltipProps> = ({ repo, sha, x, y, onClose }) => {
  const [detail, setDetail] = useState<CommitDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setLoading(true);
    fetchCommitDetail(repo, sha).then(d => {
      setDetail(d);
      setLoading(false);
    });
  }, [repo, sha]);

  // Close on click outside
  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose();
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [onClose]);

  // Position: ensure tooltip stays on screen
  const style: React.CSSProperties = {
    position: 'fixed',
    left: Math.min(x, window.innerWidth - 320),
    top: Math.min(y - 10, window.innerHeight - 300),
    zIndex: 1100,
  };

  return (
    <div ref={ref} className="prime-radiant__commit-tooltip" style={style}>
      {loading ? (
        <div className="prime-radiant__commit-tooltip-loading">Loading...</div>
      ) : detail ? (
        <>
          <div className="prime-radiant__commit-tooltip-header">
            {detail.authorAvatar && (
              <img
                src={detail.authorAvatar}
                alt={detail.author}
                className="prime-radiant__commit-tooltip-avatar"
              />
            )}
            <div>
              <div className="prime-radiant__commit-tooltip-author">{detail.author}</div>
              <div className="prime-radiant__commit-tooltip-sha">{detail.sha} &middot; {detail.date ? new Date(detail.date).toLocaleDateString() : ''}</div>
            </div>
          </div>
          <div className="prime-radiant__commit-tooltip-message">
            {detail.message.split('\n')[0]}
          </div>
          {detail.stats && (
            <div className="prime-radiant__commit-tooltip-stats">
              <span style={{ color: '#33CC66' }}>+{detail.stats.additions}</span>
              <span style={{ color: '#FF4444' }}>-{detail.stats.deletions}</span>
              <span style={{ color: '#8b949e' }}>{detail.stats.total} changes</span>
            </div>
          )}
          {detail.files && detail.files.length > 0 && (
            <div className="prime-radiant__commit-tooltip-files">
              {detail.files.map((f, i) => (
                <div key={i} className="prime-radiant__commit-tooltip-file">
                  <span className="prime-radiant__commit-tooltip-file-status" style={{
                    color: f.status === 'added' ? '#33CC66' : f.status === 'removed' ? '#FF4444' : '#FFB300',
                  }}>
                    {f.status === 'added' ? 'A' : f.status === 'removed' ? 'D' : 'M'}
                  </span>
                  <span className="prime-radiant__commit-tooltip-file-name">
                    {f.filename.split('/').pop()}
                  </span>
                  <span style={{ color: '#33CC66', fontSize: 8 }}>+{f.additions}</span>
                  <span style={{ color: '#FF4444', fontSize: 8 }}>-{f.deletions}</span>
                </div>
              ))}
              {detail.files.length >= 8 && (
                <div style={{ fontSize: 8, color: '#484f58', textAlign: 'center' }}>...</div>
              )}
            </div>
          )}
        </>
      ) : (
        <div className="prime-radiant__commit-tooltip-loading">Failed to load</div>
      )}
    </div>
  );
};
