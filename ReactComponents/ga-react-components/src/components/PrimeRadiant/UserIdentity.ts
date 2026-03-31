// src/components/PrimeRadiant/UserIdentity.ts
// User identity — display name + optional avatar, stored in localStorage.
// Designed as upgrade path: display name now, GitHub/Discord OAuth later.
// The identity is sent to SignalR hub so other viewers see the name.

import { useSyncExternalStore, useCallback } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface UserIdentity {
  displayName: string;
  avatarUrl: string | null;
  provider: 'local' | 'github' | 'discord';
  providerId: string | null;   // GitHub user ID or Discord ID
  color: string;               // assigned viewer color
}

// ---------------------------------------------------------------------------
// Storage
// ---------------------------------------------------------------------------

const STORAGE_KEY = 'prime-radiant-user-identity';

// Viewer color palette — distinct, accessible colors
const VIEWER_COLORS = [
  '#FFD700', '#FF6B6B', '#4ECDC4', '#45B7D1', '#96CEB4',
  '#FFEAA7', '#DDA0DD', '#98D8C8', '#F7DC6F', '#BB8FCE',
  '#85C1E9', '#F1948A', '#82E0AA', '#F0B27A', '#AED6F1',
];

function generateColor(): string {
  return VIEWER_COLORS[Math.floor(Math.random() * VIEWER_COLORS.length)];
}

function generateDefaultName(): string {
  const adjectives = ['Swift', 'Bright', 'Silent', 'Bold', 'Keen', 'Wise', 'Calm', 'Sharp', 'True', 'Clear'];
  const nouns = ['Observer', 'Explorer', 'Architect', 'Guardian', 'Scholar', 'Seeker', 'Builder', 'Watcher', 'Thinker', 'Maker'];
  const adj = adjectives[Math.floor(Math.random() * adjectives.length)];
  const noun = nouns[Math.floor(Math.random() * nouns.length)];
  return `${adj} ${noun}`;
}

// ---------------------------------------------------------------------------
// Identity store (singleton with change notification)
// ---------------------------------------------------------------------------

type Listener = () => void;

class IdentityStore {
  private identity: UserIdentity;
  private listeners = new Set<Listener>();

  constructor() {
    this.identity = this.load();
  }

  get(): UserIdentity {
    return this.identity;
  }

  setDisplayName(name: string): void {
    if (!name.trim()) return;
    this.identity = { ...this.identity, displayName: name.trim() };
    this.save();
    this.notify();
  }

  setAvatar(url: string | null): void {
    this.identity = { ...this.identity, avatarUrl: url };
    this.save();
    this.notify();
  }

  /** Set identity from OAuth provider (GitHub/Discord) */
  setOAuthIdentity(provider: 'github' | 'discord', id: string, name: string, avatarUrl: string | null): void {
    this.identity = {
      displayName: name,
      avatarUrl,
      provider,
      providerId: id,
      color: this.identity.color, // keep existing color
    };
    this.save();
    this.notify();
  }

  /** Reset to anonymous */
  logout(): void {
    this.identity = {
      displayName: generateDefaultName(),
      avatarUrl: null,
      provider: 'local',
      providerId: null,
      color: this.identity.color,
    };
    this.save();
    this.notify();
  }

  subscribe(listener: Listener): () => void {
    this.listeners.add(listener);
    return () => { this.listeners.delete(listener); };
  }

  private load(): UserIdentity {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw) as Partial<UserIdentity>;
        return {
          displayName: parsed.displayName || generateDefaultName(),
          avatarUrl: parsed.avatarUrl ?? null,
          provider: parsed.provider ?? 'local',
          providerId: parsed.providerId ?? null,
          color: parsed.color || generateColor(),
        };
      }
    } catch { /* corrupted storage */ }
    return {
      displayName: generateDefaultName(),
      avatarUrl: null,
      provider: 'local',
      providerId: null,
      color: generateColor(),
    };
  }

  private save(): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(this.identity));
    } catch { /* SSR or quota */ }
  }

  private notify(): void {
    for (const fn of this.listeners) fn();
  }
}

export const identityStore = new IdentityStore();

// ---------------------------------------------------------------------------
// React hook
// ---------------------------------------------------------------------------

export function useUserIdentity(): [UserIdentity, (name: string) => void] {
  const identity = useSyncExternalStore(
    (cb) => identityStore.subscribe(cb),
    () => identityStore.get(),
  );
  const setName = useCallback((name: string) => {
    identityStore.setDisplayName(name);
  }, []);
  return [identity, setName];
}
