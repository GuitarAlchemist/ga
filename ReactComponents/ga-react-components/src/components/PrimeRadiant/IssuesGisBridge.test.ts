// IssuesGisBridge — unit tests for IX-planet GIS transforms
// Covers the deterministic mapping from GitHub issues/PRs to GisPin objects
// without touching the live GitHub polling manager or Three.js scene.

import { describe, it, expect } from 'vitest';
import { IX_REPO_SECTORS, idToJitter, itemToPin, itemsToPins } from './IssuesGisBridge';
import { GITHUB_REPOS, type GitHubItem } from './IssuesPanel';

function makeItem(overrides: Partial<GitHubItem> = {}): GitHubItem {
  return {
    id: 'ga-issue-1',
    kind: 'issue',
    number: 1,
    title: 'Test item',
    repo: 'ga',
    url: 'https://github.com/GuitarAlchemist/ga/issues/1',
    author: 'alice',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
    labels: [],
    draft: false,
    staleness: 'fresh',
    ageDays: 0,
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Sector configuration
// ---------------------------------------------------------------------------

describe('IX_REPO_SECTORS', () => {
  it('defines a sector for every tracked repo', () => {
    for (const repo of GITHUB_REPOS) {
      expect(IX_REPO_SECTORS[repo]).toBeDefined();
    }
  });

  it('keeps lat/lon within geographic bounds', () => {
    for (const sector of Object.values(IX_REPO_SECTORS)) {
      expect(sector.lat).toBeGreaterThanOrEqual(-80);
      expect(sector.lat).toBeLessThanOrEqual(80);
      expect(sector.lon).toBeGreaterThanOrEqual(-180);
      expect(sector.lon).toBeLessThanOrEqual(180);
      expect(sector.color).toMatch(/^#[0-9a-fA-F]{6}$/);
    }
  });

  it('places each repo in a distinct sector', () => {
    const positions = Object.values(IX_REPO_SECTORS).map(s => `${s.lat},${s.lon}`);
    expect(new Set(positions).size).toBe(positions.length);
  });
});

// ---------------------------------------------------------------------------
// Jitter / scatter
// ---------------------------------------------------------------------------

describe('idToJitter', () => {
  it('is deterministic for the same id', () => {
    const a = idToJitter('repo-123');
    const b = idToJitter('repo-123');
    expect(a).toEqual(b);
  });

  it('stays within declared bounds', () => {
    for (let i = 0; i < 100; i++) {
      const jitter = idToJitter(`test-${i}`);
      expect(Math.abs(jitter.lat)).toBeLessThanOrEqual(12);
      expect(Math.abs(jitter.lon)).toBeLessThanOrEqual(18);
    }
  });

  it('produces different jitter for different ids', () => {
    const j1 = idToJitter('a');
    const j2 = idToJitter('b');
    expect(j1.lat !== j2.lat || j1.lon !== j2.lon).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Item-to-pin conversion
// ---------------------------------------------------------------------------

describe('itemToPin', () => {
  it('maps each repo to its declared sector', () => {
    for (const repo of GITHUB_REPOS) {
      const pin = itemToPin(makeItem({ repo, id: `${repo}-issue-1` }));
      const sector = IX_REPO_SECTORS[repo];
      const latDelta = Math.abs(pin.lat - sector.lat);
      const lonDelta = Math.abs(pin.lon - sector.lon);
      expect(latDelta).toBeLessThanOrEqual(12);
      expect(lonDelta).toBeLessThanOrEqual(18);
      expect(pin.category).toBe(repo);
    }
  });

  it('falls back to the ga sector for unknown repos', () => {
    const pin = itemToPin(makeItem({ repo: 'unknown', id: 'unknown-issue-1' }));
    const ga = IX_REPO_SECTORS.ga;
    expect(Math.abs(pin.lat - ga.lat)).toBeLessThanOrEqual(12);
  });

  it('clamps latitude to [-80, 80] and wraps longitude to [-180, 180]', () => {
    const pin = itemToPin(makeItem({ repo: 'ga', id: 'polar-issue' }));
    expect(pin.lat).toBeGreaterThanOrEqual(-80);
    expect(pin.lat).toBeLessThanOrEqual(80);
    expect(pin.lon).toBeGreaterThanOrEqual(-180);
    expect(pin.lon).toBeLessThanOrEqual(180);
  });

  it('uses the hexavalent health color palette', () => {
    expect(itemToPin(makeItem({ staleness: 'fresh' })).color).toBe('#33CC66');
    expect(itemToPin(makeItem({ staleness: 'aging' })).color).toBe('#FFB300');
    expect(itemToPin(makeItem({ staleness: 'stale' })).color).toBe('#FF4444');
  });

  it('pulses only stale pins', () => {
    expect(itemToPin(makeItem({ staleness: 'fresh' })).pulse).toBe(false);
    expect(itemToPin(makeItem({ staleness: 'aging' })).pulse).toBe(false);
    expect(itemToPin(makeItem({ staleness: 'stale' })).pulse).toBe(true);
  });

  it('uses different icons for issues and PRs', () => {
    expect(itemToPin(makeItem({ kind: 'issue' })).icon).toBe('●');
    expect(itemToPin(makeItem({ kind: 'pr' })).icon).toBe('⛓');
  });

  it('grows pin size with age', () => {
    const fresh = itemToPin(makeItem({ ageDays: 0 }));
    const old = itemToPin(makeItem({ ageDays: 100 }));
    expect(old.size!).toBeGreaterThan(fresh.size!);
  });

  it('truncates long titles in the label', () => {
    const long = 'a'.repeat(50);
    const pin = itemToPin(makeItem({ title: long }));
    expect(pin.label).toContain('#1');
    expect(pin.label.length).toBeLessThan(long.length + 4);
  });

  it('prefixes pin ids with ix-', () => {
    const pin = itemToPin(makeItem({ id: 'ga-issue-42' }));
    expect(pin.id).toBe('ix-ga-issue-42');
  });
});

// ---------------------------------------------------------------------------
// Batch mapping
// ---------------------------------------------------------------------------

describe('itemsToPins', () => {
  it('maps an empty list to an empty list', () => {
    expect(itemsToPins([])).toEqual([]);
  });

  it('maps multiple items preserving repo grouping', () => {
    const items: GitHubItem[] = [
      makeItem({ id: 'ga-issue-1', repo: 'ga', kind: 'issue' }),
      makeItem({ id: 'ix-pr-3', repo: 'ix', kind: 'pr' }),
      makeItem({ id: 'Demerzel-issue-7', repo: 'Demerzel', kind: 'issue' }),
    ];
    const pins = itemsToPins(items);
    expect(pins).toHaveLength(3);
    expect(pins.map(p => p.data!.repo)).toEqual(['ga', 'ix', 'Demerzel']);
  });
});
