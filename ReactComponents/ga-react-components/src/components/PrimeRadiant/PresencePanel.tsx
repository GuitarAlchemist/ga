// src/components/PrimeRadiant/PresencePanel.tsx
// Admin presence panel — shows connected viewers, Discord presence, session timeline.

import React, { useCallback, useEffect, useRef, useState } from 'react';
import type { ViewerInfo } from './DataLoader';
import { useConnectionLog, type ConnectionLogEntry } from './ConnectionLog';
import { useAgentPresence, AGENT_STATUS_COLORS, AGENT_STATUS_LABELS, type A2AAgent } from './AgentPresence';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface PresencePanelProps {
  viewers: ViewerInfo[];
  selfConnectionId?: string | null;
  onSendDiscordMessage?: (message: string) => void;
  /** Pre-fetched agents from parent — if omitted, panel polls internally */
  agents?: A2AAgent[];
}

interface DiscordMember {
  id: string;
  username: string;
  avatarUrl?: string;
  status: 'online' | 'idle' | 'dnd' | 'offline';
}

interface DiscordMessage {
  id: string;
  username: string;
  avatarUrl?: string;
  content: string;
  timestamp: string;
}

export interface PresenceEvent {
  id: string;
  time: string;      // HH:MM formatted
  description: string;
  timestamp: number;  // epoch ms for sorting
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function formatDuration(connectedAt: string): string {
  const ms = Date.now() - new Date(connectedAt).getTime();
  if (ms < 0) return 'just now';
  const secs = Math.floor(ms / 1000);
  if (secs < 60) return `${secs}s`;
  const mins = Math.floor(secs / 60);
  if (mins < 60) return `${mins}m`;
  const hrs = Math.floor(mins / 60);
  const remMins = mins % 60;
  return `${hrs}h ${remMins}m`;
}

function formatTime(date: Date): string {
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false });
}

function truncate(text: string, max: number): string {
  return text.length > max ? text.slice(0, max) + '...' : text;
}

function initials(username: string): string {
  return username.slice(0, 2).toUpperCase();
}

function formatConnLogTime(isoStr: string): string {
  const date = new Date(isoStr);
  const now = new Date();
  const time = date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false });
  // Show date prefix if not today
  if (date.toDateString() !== now.toDateString()) {
    const month = date.toLocaleString([], { month: 'short' });
    return `${month} ${date.getDate()} ${time}`;
  }
  return time;
}

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return `${seconds}s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

// ---------------------------------------------------------------------------
// Singleton event log — allows external pushes from ForceRadiant
// ---------------------------------------------------------------------------

const MAX_EVENTS = 20;
let _events: PresenceEvent[] = [];
let _eventListeners = new Set<() => void>();

export function pushPresenceEvent(description: string): void {
  const now = new Date();
  const event: PresenceEvent = {
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 6)}`,
    time: formatTime(now),
    description,
    timestamp: now.getTime(),
  };
  _events = [event, ..._events].slice(0, MAX_EVENTS);
  for (const fn of _eventListeners) fn();
}

function usePresenceEvents(): PresenceEvent[] {
  const [, forceUpdate] = useState(0);
  useEffect(() => {
    const cb = () => forceUpdate(n => n + 1);
    _eventListeners.add(cb);
    return () => { _eventListeners.delete(cb); };
  }, []);
  return _events;
}

// ---------------------------------------------------------------------------
// PresencePanel component
// ---------------------------------------------------------------------------

export const PresencePanel: React.FC<PresencePanelProps> = ({ viewers, selfConnectionId, onSendDiscordMessage, agents: agentsProp }) => {
  // A2A agent presence — use prop if parent provides it, otherwise poll internally
  const internalAgents = useAgentPresence();
  const agents = agentsProp ?? internalAgents;

  // Discord state
  const [discordMembers, setDiscordMembers] = useState<DiscordMember[]>([]);
  const [discordMessages, setDiscordMessages] = useState<DiscordMessage[]>([]);
  const [discordOnline, setDiscordOnline] = useState<boolean | null>(null); // null = loading
  const [discordInput, setDiscordInput] = useState('');
  const [sendingMessage, setSendingMessage] = useState(false);

  // Session activity timeline
  const events = usePresenceEvents();

  // Persistent connection log
  const connLog = useConnectionLog();
  const connEntries = connLog.getEntries();

  // Duration tick — re-render every 10s to update connection durations
  const [, setTick] = useState(0);
  useEffect(() => {
    const iv = setInterval(() => setTick(n => n + 1), 10_000);
    return () => clearInterval(iv);
  }, []);

  // ── Discord presence fetch ──
  const fetchDiscord = useCallback(async () => {
    try {
      const [presRes, msgRes] = await Promise.all([
        fetch('/api/discord/presence'),
        fetch('/api/discord/messages?limit=5'),
      ]);
      if (!presRes.ok || !msgRes.ok) throw new Error('Discord API unavailable');
      const members: DiscordMember[] = await presRes.json();
      const messages: DiscordMessage[] = await msgRes.json();
      setDiscordMembers(members);
      setDiscordMessages(messages);
      setDiscordOnline(true);
    } catch {
      setDiscordOnline(false);
    }
  }, []);

  useEffect(() => {
    fetchDiscord();
    const iv = setInterval(fetchDiscord, 30_000);
    return () => clearInterval(iv);
  }, [fetchDiscord]);

  // ── Send discord message ──
  const handleSendDiscord = useCallback(async () => {
    const text = discordInput.trim();
    if (!text) return;
    setSendingMessage(true);
    try {
      if (onSendDiscordMessage) {
        onSendDiscordMessage(text);
      } else {
        await fetch('/api/discord/send', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ content: text }),
        });
      }
      setDiscordInput('');
      // Refresh messages
      setTimeout(fetchDiscord, 1000);
    } catch {
      // silently fail
    } finally {
      setSendingMessage(false);
    }
  }, [discordInput, onSendDiscordMessage, fetchDiscord]);

  const handleDiscordKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSendDiscord();
    }
  }, [handleSendDiscord]);

  // ── Log panel open event on mount ──
  const didLogOpen = useRef(false);
  useEffect(() => {
    if (!didLogOpen.current) {
      pushPresenceEvent('Opened Presence panel');
      didLogOpen.current = true;
    }
  }, []);

  // ── Render ──

  const statusDotColor: Record<string, string> = {
    online: '#43b581',
    idle: '#faa61a',
    dnd: '#f04747',
    offline: '#747f8d',
  };

  return (
    <div className="presence-panel">
      {/* ── Connected Viewers ── */}
      <section className="presence-panel__section">
        <h3 className="presence-panel__heading">
          Viewers
          <span className="presence-panel__count">{viewers.length}</span>
        </h3>
        {viewers.length === 0 ? (
          <p className="presence-panel__empty">No other viewers connected</p>
        ) : (
          <ul className="presence-panel__viewer-list">
            {viewers.map(v => (
              <li key={v.connectionId} className="presence-panel__viewer">
                <span
                  className="presence-panel__dot"
                  style={{ backgroundColor: v.color }}
                />
                <span className="presence-panel__viewer-name">
                  {v.browser}
                </span>
                <span className="presence-panel__viewer-duration">
                  {formatDuration(v.connectedAt)}
                </span>
                {v.connectionId === selfConnectionId && (
                  <span className="presence-panel__badge">YOU</span>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* ── A2A Agents ── */}
      <section className="presence-panel__section presence-panel__section--agents">
        <h3 className="presence-panel__heading">
          A2A Agents
          <span className="presence-panel__count">{agents.length}</span>
          <span className="presence-panel__count presence-panel__count--online">
            {agents.filter(a => a.status === 'online').length} online
          </span>
        </h3>
        <ul className="presence-panel__agent-list">
          {agents.map(agent => (
            <li key={agent.id} className={`presence-panel__agent presence-panel__agent--${agent.status}`}>
              <div className="presence-panel__agent-header">
                <span
                  className="presence-panel__agent-dot"
                  style={{ backgroundColor: AGENT_STATUS_COLORS[agent.status] }}
                  title={AGENT_STATUS_LABELS[agent.status]}
                />
                <span
                  className="presence-panel__agent-name"
                  style={{ color: agent.color }}
                >
                  {agent.name}
                </span>
                <span className="presence-panel__agent-version">v{agent.version}</span>
                <span className={`presence-panel__agent-status presence-panel__agent-status--${agent.status}`}>
                  {AGENT_STATUS_LABELS[agent.status]}
                </span>
              </div>
              <div className="presence-panel__agent-meta">
                <span className="presence-panel__agent-desc">{agent.description}</span>
                {agent.latencyMs !== null && (
                  <span className="presence-panel__agent-latency">{agent.latencyMs}ms</span>
                )}
                {agent.port > 0 && (
                  <span className="presence-panel__agent-port">:{agent.port}</span>
                )}
              </div>
              <div className="presence-panel__agent-skills">
                {agent.skills.slice(0, 4).map(skill => (
                  <span key={skill.id} className="presence-panel__agent-skill" title={skill.name}>
                    {skill.name}
                  </span>
                ))}
                {agent.skills.length > 4 && (
                  <span className="presence-panel__agent-skill presence-panel__agent-skill--more">
                    +{agent.skills.length - 4}
                  </span>
                )}
              </div>
              {agent.lastSeen && (
                <span className="presence-panel__agent-lastseen">
                  Last seen: {timeAgo(agent.lastSeen)}
                </span>
              )}
            </li>
          ))}
        </ul>
      </section>

      {/* ── Discord Presence ── */}
      <section className="presence-panel__section presence-panel__section--discord">
        <h3 className="presence-panel__heading">
          Discord
          {discordOnline === true && <span className="presence-panel__discord-status presence-panel__discord-status--online">online</span>}
          {discordOnline === false && <span className="presence-panel__discord-status presence-panel__discord-status--offline">offline</span>}
          {discordOnline === null && <span className="presence-panel__discord-status">...</span>}
        </h3>

        {discordOnline === false && (
          <div className="presence-panel__discord-retry">
            <span>Discord: offline</span>
            <button className="presence-panel__btn presence-panel__btn--small" onClick={fetchDiscord}>Retry</button>
          </div>
        )}

        {discordOnline === true && (
          <>
            {/* Online members */}
            {discordMembers.length > 0 && (
              <ul className="presence-panel__discord-members">
                {discordMembers.map(m => (
                  <li key={m.id} className="presence-panel__discord-member">
                    {m.avatarUrl ? (
                      <img className="presence-panel__avatar" src={m.avatarUrl} alt={m.username} />
                    ) : (
                      <span className="presence-panel__avatar presence-panel__avatar--initials">{initials(m.username)}</span>
                    )}
                    <span className="presence-panel__discord-username">{m.username}</span>
                    <span
                      className="presence-panel__discord-dot"
                      style={{ backgroundColor: statusDotColor[m.status] || statusDotColor.offline }}
                      title={m.status}
                    />
                  </li>
                ))}
              </ul>
            )}

            {/* Recent messages */}
            {discordMessages.length > 0 && (
              <div className="presence-panel__discord-messages">
                <h4 className="presence-panel__sub-heading">Recent Messages</h4>
                {discordMessages.map(msg => (
                  <div key={msg.id} className="presence-panel__discord-msg">
                    {msg.avatarUrl ? (
                      <img className="presence-panel__avatar presence-panel__avatar--small" src={msg.avatarUrl} alt={msg.username} />
                    ) : (
                      <span className="presence-panel__avatar presence-panel__avatar--small presence-panel__avatar--initials">{initials(msg.username)}</span>
                    )}
                    <div className="presence-panel__discord-msg-body">
                      <span className="presence-panel__discord-msg-author">{msg.username}</span>
                      <span className="presence-panel__discord-msg-time">{timeAgo(msg.timestamp)}</span>
                      <p className="presence-panel__discord-msg-content">{truncate(msg.content, 100)}</p>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {/* Send message */}
            <div className="presence-panel__discord-send">
              <input
                className="presence-panel__discord-input"
                type="text"
                placeholder="Send to Discord..."
                value={discordInput}
                onChange={e => setDiscordInput(e.target.value)}
                onKeyDown={handleDiscordKeyDown}
                disabled={sendingMessage}
              />
              <button
                className="presence-panel__btn"
                onClick={handleSendDiscord}
                disabled={sendingMessage || !discordInput.trim()}
              >
                Send
              </button>
            </div>
          </>
        )}
      </section>

      {/* ── Session Activity Timeline ── */}
      <section className="presence-panel__section">
        <h3 className="presence-panel__heading">
          Session Activity
          <span className="presence-panel__count">{events.length}</span>
        </h3>
        {events.length === 0 ? (
          <p className="presence-panel__empty">No activity yet</p>
        ) : (
          <ul className="presence-panel__timeline">
            {events.map(evt => (
              <li key={evt.id} className="presence-panel__event">
                <span className="presence-panel__event-time">{evt.time}</span>
                <span className="presence-panel__event-dash">&mdash;</span>
                <span className="presence-panel__event-desc">{evt.description}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* ── Connection Log (persistent, survives reload) ── */}
      <section className="presence-panel__section presence-panel__section--connlog">
        <h3 className="presence-panel__heading">
          Connection Log
          <span className="presence-panel__count">{connEntries.length}</span>
          {connEntries.length > 0 && (
            <button
              className="presence-panel__btn presence-panel__btn--small presence-panel__btn--clear"
              onClick={() => connLog.clear()}
              title="Clear connection log"
            >
              Clear
            </button>
          )}
        </h3>
        {connEntries.length === 0 ? (
          <p className="presence-panel__empty">No connection events recorded</p>
        ) : (
          <ul className="presence-panel__connlog-list">
            {connEntries.map(entry => (
              <li key={entry.id} className={`presence-panel__connlog-entry presence-panel__connlog-entry--${entry.event}`}>
                <span
                  className="presence-panel__connlog-icon"
                  title={entry.event}
                >
                  {entry.event === 'connect' ? '\u25B2' : '\u25BC'}
                </span>
                <span
                  className="presence-panel__dot presence-panel__dot--small"
                  style={{ backgroundColor: entry.color }}
                />
                <span className="presence-panel__connlog-browser">
                  {entry.browser}
                  {entry.isSelf && <span className="presence-panel__badge presence-panel__badge--small">YOU</span>}
                </span>
                <span className="presence-panel__connlog-time">
                  {formatConnLogTime(entry.timestamp)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
};
