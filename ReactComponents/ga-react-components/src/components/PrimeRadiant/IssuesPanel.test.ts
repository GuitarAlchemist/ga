// IssuesPanel — unit tests for pure transform functions
// Component rendering is exercised by the Prime Radiant integration tests;
// these tests cover the staleness, transform, and grouping logic.

import { describe, it, expect } from 'vitest';
import {
  classifyStaleness,
  transformGitHubItems,
  groupByRepo,
  GITHUB_REPOS,
  STALE_DAYS,
  AGING_DAYS,
  type GitHubItem,
} from './IssuesPanel';

function daysAgo(days: number): string {
  return new Date(Date.now() - days * 24 * 60 * 60 * 1000).toISOString();
}

function makeRawIssue(overrides: Partial<Record<string, unknown>> = {}): Record<string, unknown> {
  return {
    number: 1,
    title: 'Test issue',
    html_url: 'https://github.com/GuitarAlchemist/ga/issues/1',
    user: { login: 'alice' },
    created_at: daysAgo(1),
    updated_at: daysAgo(1),
    labels: [{ name: 'bug' }],
    draft: false,
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Staleness classification
// ---------------------------------------------------------------------------

describe('classifyStaleness', () => {
  it('marks fresh items below aging threshold', () => {
    expect(classifyStaleness(0)).toBe('fresh');
    expect(classifyStaleness(AGING_DAYS - 1)).toBe('fresh');
  });

  it('marks aging items between aging and stale thresholds', () => {
    expect(classifyStaleness(AGING_DAYS)).toBe('aging');
    expect(classifyStaleness(STALE_DAYS - 1)).toBe('aging');
  });

  it('marks stale items at or above stale threshold', () => {
    expect(classifyStaleness(STALE_DAYS)).toBe('stale');
    expect(classifyStaleness(STALE_DAYS + 10)).toBe('stale');
  });
});

// ---------------------------------------------------------------------------
// Item transformation
// ---------------------------------------------------------------------------

describe('transformGitHubItems', () => {
  it('transforms issue payloads into GitHubItem shape', () => {
    const data = new Map([['ga', [makeRawIssue({ number: 42, title: 'Fretboard bug' })]]]);
    const items = transformGitHubItems(data, 'issue');
    expect(items).toHaveLength(1);
    expect(items[0]).toMatchObject({
      id: 'ga-issue-42',
      kind: 'issue',
      number: 42,
      title: 'Fretboard bug',
      repo: 'ga',
      author: 'alice',
      labels: ['bug'],
      draft: false,
    });
  });

  it('transforms PR payloads and preserves draft flag', () => {
    const data = new Map([['ix', [makeRawIssue({ number: 7, draft: true, labels: [{ name: 'draft' }] })]]]);
    const items = transformGitHubItems(data, 'pr');
    expect(items[0].kind).toBe('pr');
    expect(items[0].draft).toBe(true);
    expect(items[0].id).toBe('ix-pr-7');
  });

  it('calculates age and staleness from created_at', () => {
    const data = new Map([['Demerzel', [makeRawIssue({ created_at: daysAgo(STALE_DAYS + 2) })]]]);
    const items = transformGitHubItems(data, 'issue');
    expect(items[0].ageDays).toBeGreaterThanOrEqual(STALE_DAYS);
    expect(items[0].staleness).toBe('stale');
  });

  it('handles missing user gracefully', () => {
    const data = new Map([['tars', [makeRawIssue({ user: undefined })]]]);
    const items = transformGitHubItems(data, 'issue');
    expect(items[0].author).toBe('unknown');
  });

  it('filters out empty label names', () => {
    const data = new Map([['ga', [makeRawIssue({ labels: [{ name: '' }, { name: 'valid' }] })]]]);
    const items = transformGitHubItems(data, 'issue');
    expect(items[0].labels).toEqual(['valid']);
  });
});

// ---------------------------------------------------------------------------
// Grouping
// ---------------------------------------------------------------------------

describe('groupByRepo', () => {
  it('groups items by repo and sorts by age descending', () => {
    const items: GitHubItem[] = [
      { id: 'ga-1', kind: 'issue', number: 1, title: 'a', repo: 'ga', url: '#', author: 'a', createdAt: daysAgo(2), updatedAt: daysAgo(2), labels: [], draft: false, staleness: 'fresh', ageDays: 2 },
      { id: 'ix-1', kind: 'issue', number: 2, title: 'b', repo: 'ix', url: '#', author: 'b', createdAt: daysAgo(5), updatedAt: daysAgo(5), labels: [], draft: false, staleness: 'aging', ageDays: 5 },
      { id: 'ga-2', kind: 'pr', number: 3, title: 'c', repo: 'ga', url: '#', author: 'c', createdAt: daysAgo(10), updatedAt: daysAgo(10), labels: [], draft: false, staleness: 'aging', ageDays: 10 },
    ];
    const groups = groupByRepo(items);
    expect(groups).toHaveLength(2);

    const ga = groups.find(g => g.repo === 'ga')!;
    expect(ga.issueCount).toBe(1);
    expect(ga.prCount).toBe(1);
    expect(ga.items[0].ageDays).toBe(10); // oldest first

    const ix = groups.find(g => g.repo === 'ix')!;
    expect(ix.issueCount).toBe(1);
  });

  it('only includes configured repos with items', () => {
    const items: GitHubItem[] = [
      { id: 'ga-1', kind: 'issue', number: 1, title: 'a', repo: 'ga', url: '#', author: 'a', createdAt: daysAgo(1), updatedAt: daysAgo(1), labels: [], draft: false, staleness: 'fresh', ageDays: 1 },
    ];
    const groups = groupByRepo(items);
    expect(groups).toHaveLength(1);
    expect(groups[0].repo).toBe('ga');
    for (const repo of GITHUB_REPOS) {
      if (repo !== 'ga') {
        expect(groups.find(g => g.repo === repo)).toBeUndefined();
      }
    }
  });

  it('counts stale items per repo', () => {
    const items: GitHubItem[] = [
      { id: 'ga-1', kind: 'issue', number: 1, title: 'a', repo: 'ga', url: '#', author: 'a', createdAt: daysAgo(STALE_DAYS + 1), updatedAt: daysAgo(STALE_DAYS + 1), labels: [], draft: false, staleness: 'stale', ageDays: STALE_DAYS + 1 },
      { id: 'ga-2', kind: 'issue', number: 2, title: 'b', repo: 'ga', url: '#', author: 'b', createdAt: daysAgo(1), updatedAt: daysAgo(1), labels: [], draft: false, staleness: 'fresh', ageDays: 1 },
    ];
    const groups = groupByRepo(items);
    expect(groups[0].staleCount).toBe(1);
  });
});
