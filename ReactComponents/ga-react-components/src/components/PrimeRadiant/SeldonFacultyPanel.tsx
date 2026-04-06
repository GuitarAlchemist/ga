// src/components/PrimeRadiant/SeldonFacultyPanel.tsx
// Seldon University Faculty panel — shows faculty cards with status and mini-chat.
// Registered in PanelRegistry as 'faculty' in the 'knowledge' group.

import React, { useState, useCallback, useRef, useEffect } from 'react';
import type { FacultyMember, FacultyWithStatus } from './SeldonFaculty';
import { useSeldonFaculty } from './SeldonFaculty';

// ---------------------------------------------------------------------------
// Status rendering
// ---------------------------------------------------------------------------

const STATUS_DOT: Record<string, { color: string; label: string }> = {
  online:  { color: '#33CC66', label: 'Online' },
  offline: { color: '#FF4444', label: 'Offline' },
  unknown: { color: '#888888', label: 'Unknown' },
};

// ---------------------------------------------------------------------------
// Mini-chat for a single faculty member
// ---------------------------------------------------------------------------

interface MiniChatProps {
  member: FacultyMember;
  onClose: () => void;
  ask: (member: FacultyMember, question: string) => Promise<string>;
}

interface ChatMessage {
  role: 'user' | 'faculty';
  text: string;
}

const MiniChat: React.FC<MiniChatProps> = ({ member, onClose, ask }) => {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [messages]);

  const handleSend = useCallback(async () => {
    const question = input.trim();
    if (!question || sending) return;

    setInput('');
    setMessages((prev) => [...prev, { role: 'user', text: question }]);
    setSending(true);

    try {
      const reply = await ask(member, question);
      setMessages((prev) => [...prev, { role: 'faculty', text: reply }]);
    } catch (err) {
      setMessages((prev) => [
        ...prev,
        { role: 'faculty', text: `Error: ${err instanceof Error ? err.message : 'unknown'}` },
      ]);
    } finally {
      setSending(false);
    }
  }, [input, sending, member, ask]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        handleSend();
      }
    },
    [handleSend],
  );

  return (
    <div className="faculty-panel__chat">
      <div className="faculty-panel__chat-header">
        <span className="faculty-panel__chat-title">
          <span style={{ color: member.color }}>{member.icon}</span>
          {' '}Ask {member.name}
        </span>
        <button className="faculty-panel__chat-close" onClick={onClose}>
          &times;
        </button>
      </div>

      <div className="faculty-panel__chat-messages" ref={scrollRef}>
        {messages.length === 0 && (
          <div className="faculty-panel__chat-hint">
            Ask {member.name} about {member.specialty.toLowerCase()}...
          </div>
        )}
        {messages.map((msg, i) => (
          <div
            key={i}
            className={`faculty-panel__chat-msg faculty-panel__chat-msg--${msg.role}`}
          >
            <span className="faculty-panel__chat-msg-role">
              {msg.role === 'user' ? 'You' : member.name}
            </span>
            <span className="faculty-panel__chat-msg-text">{msg.text}</span>
          </div>
        ))}
        {sending && (
          <div className="faculty-panel__chat-msg faculty-panel__chat-msg--faculty">
            <span className="faculty-panel__chat-msg-role">{member.name}</span>
            <span className="faculty-panel__chat-msg-text faculty-panel__chat-typing">
              Thinking...
            </span>
          </div>
        )}
      </div>

      <div className="faculty-panel__chat-input-row">
        <input
          className="faculty-panel__chat-input"
          type="text"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={`Ask about ${member.department.toLowerCase()}...`}
          disabled={sending}
        />
        <button
          className="faculty-panel__chat-send"
          onClick={handleSend}
          disabled={sending || !input.trim()}
        >
          {sending ? '\u23F3' : '\u2192'}
        </button>
      </div>
    </div>
  );
};

// ---------------------------------------------------------------------------
// Faculty card
// ---------------------------------------------------------------------------

interface FacultyCardProps {
  member: FacultyWithStatus;
  onSelect: (member: FacultyWithStatus) => void;
}

const FacultyCard: React.FC<FacultyCardProps> = ({ member, onSelect }) => {
  const dot = STATUS_DOT[member.status] ?? STATUS_DOT.unknown;

  return (
    <div
      className="faculty-panel__card"
      onClick={() => onSelect(member)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => { if (e.key === 'Enter') onSelect(member); }}
    >
      <div className="faculty-panel__card-icon" style={{ color: member.color }}>
        {member.icon}
      </div>
      <div className="faculty-panel__card-body">
        <div className="faculty-panel__card-name">{member.name}</div>
        <div className="faculty-panel__card-title">{member.title}</div>
        <div className="faculty-panel__card-dept">{member.department}</div>
        <div className="faculty-panel__card-specialty">{member.specialty}</div>
      </div>
      <div className="faculty-panel__card-status" title={dot.label}>
        <span
          className="faculty-panel__card-dot"
          style={{ backgroundColor: dot.color }}
        />
        <span className="faculty-panel__card-status-label">{dot.label}</span>
      </div>
    </div>
  );
};

// ---------------------------------------------------------------------------
// Main panel
// ---------------------------------------------------------------------------

export const SeldonFacultyPanel: React.FC = () => {
  const { faculty, loading, refresh, ask } = useSeldonFaculty();
  const [chatTarget, setChatTarget] = useState<FacultyWithStatus | null>(null);

  const handleSelect = useCallback((member: FacultyWithStatus) => {
    setChatTarget(member);
  }, []);

  const handleCloseChat = useCallback(() => {
    setChatTarget(null);
  }, []);

  return (
    <div className="faculty-panel">
      <div className="faculty-panel__header">
        <h3 className="faculty-panel__heading">Seldon Faculty<span className="prime-radiant__demo-badge">Demo</span></h3>
        <button
          className="faculty-panel__refresh"
          onClick={refresh}
          disabled={loading}
          title="Refresh status"
        >
          {loading ? '\u23F3' : '\u21BB'}
        </button>
      </div>

      {chatTarget ? (
        <MiniChat
          member={chatTarget}
          onClose={handleCloseChat}
          ask={ask}
        />
      ) : (
        <div className="faculty-panel__grid">
          {faculty.map((m) => (
            <FacultyCard key={m.providerId} member={m} onSelect={handleSelect} />
          ))}
        </div>
      )}
    </div>
  );
};
