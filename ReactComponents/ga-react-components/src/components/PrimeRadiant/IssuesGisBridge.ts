// src/components/PrimeRadiant/IssuesGisBridge.ts
// Bridge from GitHubPollingManager issues/PRs to IX-planet GIS pins.
// Each repo owns a sector on the fictional IX forge-world; items are scattered
// within the sector with deterministic jitter from their IDs.

import type { GisLayerManager, GisPin } from './GisLayer';
import { gitHubPollingManager, type GitHubDataType } from './GitHubPollingManager';
import { GITHUB_REPOS, type GitHubItem, transformGitHubItems } from './IssuesPanel';

// ---------------------------------------------------------------------------
// Sector placement on the IX surface (lat/lon)
// ---------------------------------------------------------------------------
export const IX_REPO_SECTORS: Record<string, { lat: number; lon: number; color: string }> = {
  Demerzel: { lat: 40, lon: -70, color: '#FFD700' },
  ga:       { lat: 40, lon: 70,  color: '#FFA726' },
  tars:     { lat: -40, lon: -70, color: '#4FC3F7' },
  ix:       { lat: -40, lon: 70,  color: '#73d13d' },
};

const STALE_COLORS = { fresh: '#33CC66', aging: '#FFB300', stale: '#FF4444' };
const KIND_ICON: Record<string, string> = { issue: '●', pr: '⛓' };
const PIN_SIZE = 1.2;

function hashStringToNumber(str: string): number {
  let hash = 2166136261;
  for (let i = 0; i < str.length; i++) {
    hash ^= str.charCodeAt(i);
    hash = (hash * 16777619) >>> 0;
  }
  return hash;
}

export function idToJitter(id: string): { lat: number; lon: number } {
  const h1 = hashStringToNumber(id);
  const h2 = hashStringToNumber(`${id}:lon`);
  return {
    lat: ((h1 / 0xFFFFFFFF) * 2 - 1) * 12, // ±12°
    lon: ((h2 / 0xFFFFFFFF) * 2 - 1) * 18, // ±18°
  };
}

export function itemToPin(item: GitHubItem): GisPin {
  const sector = IX_REPO_SECTORS[item.repo] ?? IX_REPO_SECTORS.ga;
  const jitter = idToJitter(item.id);
  const lat = Math.max(-80, Math.min(80, sector.lat + jitter.lat));
  const lon = ((sector.lon + jitter.lon + 180) % 360) - 180;
  const title = item.title.length > 28 ? `${item.title.slice(0, 25)}...` : item.title;
  return {
    id: `ix-${item.id}`,
    lat,
    lon,
    label: `#${item.number} ${title}`,
    color: STALE_COLORS[item.staleness],
    icon: KIND_ICON[item.kind] ?? '●',
    size: PIN_SIZE + item.ageDays * 0.02,
    category: item.repo,
    pulse: item.staleness === 'stale',
    data: { kind: item.kind, repo: item.repo, url: item.url, ageDays: item.ageDays },
  };
}

export function itemsToPins(items: GitHubItem[]): GisPin[] {
  return items.map(itemToPin);
}

function getAllItems(): GitHubItem[] {
  const issues = transformGitHubItems(
    new Map(GITHUB_REPOS.map(r => [r, gitHubPollingManager.getLastData('issues', r) ?? []])),
    'issue',
  );
  const prs = transformGitHubItems(
    new Map(GITHUB_REPOS.map(r => [r, gitHubPollingManager.getLastData('pulls-open', r) ?? []])),
    'pr',
  );
  return [...issues, ...prs];
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

export interface IssuesGisBridgeHandle {
  /** Resync pins from the current GitHub polling state. */
  refresh: () => void;
  /** Stop listening and clear all pins. */
  dispose: () => void;
}

/**
 * Start the issues/PRs → IX GIS bridge.
 * Returns a handle with refresh/dispose methods. The bridge listens to both
 * 'issues' and 'pulls-open' polling channels and updates IX pins accordingly.
 */
export function startIssuesGisBridge(gisManager: GisLayerManager): IssuesGisBridgeHandle {
  let disposed = false;

  const sync = () => {
    if (disposed) return;
    const items = getAllItems();
    const pins = items.map(itemToPin);
    gisManager.clearAll();
    if (pins.length > 0) gisManager.addPins(pins);
  };

  const unsubIssues = gitHubPollingManager.subscribe('issues' as GitHubDataType, GITHUB_REPOS, sync);
  const unsubPrs = gitHubPollingManager.subscribe('pulls-open' as GitHubDataType, GITHUB_REPOS, sync);

  sync();

  return {
    refresh: sync,
    dispose: () => {
      disposed = true;
      unsubIssues();
      unsubPrs();
      gisManager.clearAll();
    },
  };
}
