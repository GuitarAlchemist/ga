// src/components/PrimeRadiant/ClaudeCodePanel.tsx
// Terminal-like command panel for creating skills, schedules, and running governance commands

import React, { useCallback, useEffect, useRef, useState } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
interface Command {
  id: string;
  text: string;
  timestamp: number;
  status: 'pending' | 'running' | 'completed' | 'failed';
  result?: string;
}

// ---------------------------------------------------------------------------
// SpeechRecognition type shim (Web Speech API)
// ---------------------------------------------------------------------------
interface SpeechRecognitionEvent {
  results: { [index: number]: { [index: number]: { transcript: string } }; length: number };
  resultIndex: number;
}

interface SpeechRecognitionErrorEvent {
  error: string;
}

interface SpeechRecognitionInstance {
  continuous: boolean;
  interimResults: boolean;
  lang: string;
  start(): void;
  stop(): void;
  onresult: ((event: SpeechRecognitionEvent) => void) | null;
  onerror: ((event: SpeechRecognitionErrorEvent) => void) | null;
  onend: (() => void) | null;
}

type SpeechRecognitionConstructor = new () => SpeechRecognitionInstance;

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------
const STORAGE_KEY = 'prime-radiant-claude-commands';
const MAX_HISTORY = 50;

const STATUS_CONFIG: Record<Command['status'], { color: string; icon: string; label: string }> = {
  pending: { color: '#6b7280', icon: '\u25CB', label: 'pending' },
  running: { color: '#FFB300', icon: '\u25CF', label: 'running' },
  completed: { color: '#33CC66', icon: '\u2713', label: 'completed' },
  failed: { color: '#FF4444', icon: '\u2717', label: 'failed' },
};

interface QuickAction {
  label: string;
  command: string;
  color: string;
}

const QUICK_ACTIONS: QuickAction[] = [
  { label: 'Self-Reflect', command: '/self-reflect', color: '#c084fc' },
  { label: 'Create Skill', command: '/skill create', color: '#FFD700' },
  { label: 'Add Schedule', command: '/schedule add', color: '#4FC3F7' },
  { label: 'Run Audit', command: '/governance audit', color: '#33CC66' },
  { label: 'Deploy', command: '/deploy', color: '#FF7A45' },
];

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
function generateId(): string {
  return `cmd-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

function loadCommands(): Command[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? JSON.parse(raw) : [];
  } catch {
    return [];
  }
}

function saveCommands(commands: Command[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(commands.slice(-MAX_HISTORY)));
  } catch {
    // localStorage full or unavailable — silently ignore
  }
}

function formatTime(ts: number): string {
  const d = new Date(ts);
  return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function getSpeechRecognition(): SpeechRecognitionConstructor | null {
  const w = window as unknown as Record<string, unknown>;
  return (w.SpeechRecognition ?? w.webkitSpeechRecognition ?? null) as SpeechRecognitionConstructor | null;
}

// ---------------------------------------------------------------------------
// Simulated command execution
// ---------------------------------------------------------------------------
function simulateExecution(command: Command, update: (cmd: Command) => void): void {
  // Move to running immediately
  const running = { ...command, status: 'running' as const };
  update(running);

  const delay = 1500 + Math.random() * 2000;
  setTimeout(() => {
    const text = command.text.toLowerCase().trim();
    let result: string;
    let status: Command['status'] = 'completed';

    if (text.startsWith('/self-reflect')) {
      // Capture the Prime Radiant canvas and analyze it
      const canvas = document.querySelector('.prime-radiant__canvas-area canvas') as HTMLCanvasElement | null;
      if (canvas) {
        try {
          const { captureCanvas } = await import('./ScreenshotCapture');
          const dataUrl = await captureCanvas();
          const sizeKb = Math.round(dataUrl.length * 0.75 / 1024);
          // Store screenshot for governance pickup
          const screenshot = {
            type: 'self-reflect',
            timestamp: new Date().toISOString(),
            dataUrl,
            sizeKb,
            viewport: { width: canvas.width, height: canvas.height },
            activePanel: document.querySelector('.prime-radiant__side-panel--open') ? 'open' : 'closed',
            nodeCount: document.querySelectorAll('.prime-radiant__canvas-area canvas').length,
          };
          localStorage.setItem('prime-radiant-last-screenshot', JSON.stringify({
            timestamp: screenshot.timestamp,
            sizeKb: screenshot.sizeKb,
            viewport: screenshot.viewport,
          }));
          // Also capture DOM metrics
          const healthBar = document.querySelector('.prime-radiant__health');
          const panels = document.querySelectorAll('.prime-radiant__side-panel > *');
          const overlays = document.querySelectorAll('[class*="overlay"]');
          result = [
            `Screenshot captured: ${sizeKb}KB (${canvas.width}x${canvas.height})`,
            `DOM analysis:`,
            `  Health HUD: ${healthBar ? 'visible' : 'missing'}`,
            `  Side panels: ${panels.length} component(s) loaded`,
            `  Overlays: ${overlays.length} active`,
            `  Canvas: WebGL ${canvas.getContext('webgl2') ? '2.0' : canvas.getContext('webgl') ? '1.0' : 'unavailable'}`,
            `  FPS indicator: ${document.querySelector('.prime-radiant__gst-clock') ? 'present' : 'missing'}`,
            ``,
            `Visual check: Screenshot stored. Send to /demerzel render-critic for AI analysis.`,
          ].join('\n');
        } catch (err) {
          result = `Screenshot failed: ${err instanceof Error ? err.message : String(err)}`;
          status = 'failed';
        }
      } else {
        result = 'No canvas found. Is the 3D view loaded?';
        status = 'failed';
      }
    } else if (text.startsWith('/skill create')) {
      result = 'Skill template generated. Edit and commit to register.';
    } else if (text.startsWith('/schedule add')) {
      result = 'Schedule entry queued. Next execution cycle will pick it up.';
    } else if (text.startsWith('/governance audit')) {
      result = 'Governance audit initiated across Demerzel, ga, tars, ix. 39 policies evaluated. 0 violations.';
    } else if (text.startsWith('/deploy')) {
      result = 'Deployment pipeline triggered. Monitor in CI/CD panel.';
    } else if (text.startsWith('/help')) {
      result = 'Commands: /self-reflect, /skill create, /schedule add, /governance audit, /deploy, /seldon plan, /belief status, /help';
    } else if (text.startsWith('/seldon')) {
      result = 'Seldon Plan status: 4 active predictions, 2 pending validation.';
    } else if (text.startsWith('/belief')) {
      result = 'Belief state: 12 T, 3 U, 1 C. No staleness detected.';
    } else if (text === '') {
      result = 'Empty command. Type /help for available commands.';
      status = 'failed';
    } else {
      result = `Executed: ${command.text}`;
    }

    update({ ...command, status, result });
  }, delay);
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const ClaudeCodePanel: React.FC = () => {
  const [commands, setCommands] = useState<Command[]>(loadCommands);
  const [input, setInput] = useState('');
  const [collapsed, setCollapsed] = useState(false);
  const [listening, setListening] = useState(false);
  const logRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const recognitionRef = useRef<SpeechRecognitionInstance | null>(null);

  const speechSupported = !!getSpeechRecognition();

  // Persist commands to localStorage on change
  useEffect(() => {
    saveCommands(commands);
  }, [commands]);

  // Auto-scroll log to bottom
  useEffect(() => {
    if (logRef.current) {
      logRef.current.scrollTop = logRef.current.scrollHeight;
    }
  }, [commands]);

  const updateCommand = useCallback((updated: Command) => {
    setCommands((prev) => {
      const idx = prev.findIndex((c) => c.id === updated.id);
      if (idx === -1) return prev;
      const next = [...prev];
      next[idx] = updated;
      return next;
    });
  }, []);

  const submitCommand = useCallback((text: string) => {
    const trimmed = text.trim();
    if (!trimmed) return;

    const cmd: Command = {
      id: generateId(),
      text: trimmed,
      timestamp: Date.now(),
      status: 'pending',
    };

    setCommands((prev) => [...prev, cmd]);
    setInput('');
    simulateExecution(cmd, updateCommand);
  }, [updateCommand]);

  const handleKeyDown = useCallback((e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      submitCommand(input);
    }
  }, [input, submitCommand]);

  const handleQuickAction = useCallback((action: QuickAction) => {
    setInput(action.command + ' ');
    inputRef.current?.focus();
  }, []);

  const toggleSpeech = useCallback(() => {
    if (listening && recognitionRef.current) {
      recognitionRef.current.stop();
      setListening(false);
      return;
    }

    const SpeechRecognition = getSpeechRecognition();
    if (!SpeechRecognition) return;

    const recognition = new SpeechRecognition();
    recognition.continuous = false;
    recognition.interimResults = false;
    recognition.lang = 'en-US';

    recognition.onresult = (event: SpeechRecognitionEvent) => {
      const transcript = event.results[0][0].transcript;
      setInput((prev) => prev + transcript);
    };

    recognition.onerror = () => {
      setListening(false);
    };

    recognition.onend = () => {
      setListening(false);
    };

    recognitionRef.current = recognition;
    recognition.start();
    setListening(true);
  }, [listening]);

  const clearHistory = useCallback(() => {
    setCommands([]);
    localStorage.removeItem(STORAGE_KEY);
  }, []);

  const pendingCount = commands.filter((c) => c.status === 'pending' || c.status === 'running').length;

  return (
    <div className="prime-radiant__activity" style={{ maxHeight: '70vh', overflowY: 'auto' }}>
      {/* Header */}
      <div
        className="prime-radiant__activity-header"
        onClick={() => setCollapsed(!collapsed)}
      >
        <span className="prime-radiant__activity-title">
          Claude Code
          <span className="prime-radiant__activity-count">
            {commands.length} commands
            {pendingCount > 0 && (
              <span style={{ color: '#FFB300' }}> &middot; {pendingCount} running</span>
            )}
          </span>
        </span>
        <span className="prime-radiant__activity-toggle">
          {collapsed ? '\u25B6' : '\u25BC'}
        </span>
      </div>

      {!collapsed && (
        <div style={{ padding: '8px 0' }}>
          {/* Quick actions */}
          <div style={{
            display: 'flex',
            flexWrap: 'wrap',
            gap: 6,
            padding: '4px 12px 8px',
            borderBottom: '1px solid #21262d',
          }}>
            {QUICK_ACTIONS.map((action) => (
              <button
                key={action.label}
                onClick={() => handleQuickAction(action)}
                style={{
                  padding: '3px 10px',
                  background: `${action.color}15`,
                  border: `1px solid ${action.color}44`,
                  borderRadius: 12,
                  color: action.color,
                  fontFamily: "'JetBrains Mono', monospace",
                  fontSize: '0.65rem',
                  fontWeight: 600,
                  cursor: 'pointer',
                  transition: 'all 0.15s',
                  lineHeight: 1.6,
                  whiteSpace: 'nowrap',
                }}
                onMouseEnter={(e) => {
                  (e.target as HTMLElement).style.background = `${action.color}30`;
                }}
                onMouseLeave={(e) => {
                  (e.target as HTMLElement).style.background = `${action.color}15`;
                }}
              >
                {action.label}
              </button>
            ))}
          </div>

          {/* Command input */}
          <div style={{
            display: 'flex',
            alignItems: 'center',
            gap: 6,
            padding: '8px 12px',
            borderBottom: '1px solid #21262d',
          }}>
            {/* Prompt character */}
            <span style={{
              color: '#FFD700',
              fontFamily: "'JetBrains Mono', monospace",
              fontSize: '0.8rem',
              fontWeight: 700,
              flexShrink: 0,
              userSelect: 'none',
            }}>
              &gt;
            </span>
            <input
              ref={inputRef}
              type="text"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Type a command or say it..."
              style={{
                flex: 1,
                background: 'transparent',
                border: 'none',
                outline: 'none',
                color: '#c9d1d9',
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: '0.75rem',
                padding: '4px 0',
                caretColor: '#FFD700',
              }}
            />
            {/* Mic button */}
            {speechSupported && (
              <button
                onClick={toggleSpeech}
                title={listening ? 'Stop listening' : 'Voice input'}
                style={{
                  width: 28,
                  height: 28,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  background: listening ? 'rgba(255, 68, 68, 0.2)' : 'rgba(255, 215, 0, 0.1)',
                  border: `1px solid ${listening ? 'rgba(255, 68, 68, 0.5)' : 'rgba(255, 215, 0, 0.3)'}`,
                  borderRadius: '50%',
                  color: listening ? '#FF4444' : '#FFD700',
                  cursor: 'pointer',
                  fontSize: '0.8rem',
                  flexShrink: 0,
                  transition: 'all 0.15s',
                  animation: listening ? 'pulse 1.5s infinite' : 'none',
                }}
              >
                {/* Mic SVG icon */}
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z" />
                  <path d="M19 10v2a7 7 0 0 1-14 0v-2" />
                  <line x1="12" y1="19" x2="12" y2="23" />
                  <line x1="8" y1="23" x2="16" y2="23" />
                </svg>
              </button>
            )}
            {/* Submit button */}
            <button
              onClick={() => submitCommand(input)}
              title="Execute command"
              style={{
                width: 28,
                height: 28,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                background: 'rgba(255, 215, 0, 0.1)',
                border: '1px solid rgba(255, 215, 0, 0.3)',
                borderRadius: 4,
                color: '#FFD700',
                cursor: 'pointer',
                fontSize: '0.8rem',
                fontWeight: 700,
                flexShrink: 0,
                fontFamily: "'JetBrains Mono', monospace",
                transition: 'all 0.15s',
              }}
              onMouseEnter={(e) => {
                (e.target as HTMLElement).style.background = 'rgba(255, 215, 0, 0.25)';
              }}
              onMouseLeave={(e) => {
                (e.target as HTMLElement).style.background = 'rgba(255, 215, 0, 0.1)';
              }}
            >
              {/* Arrow right / enter icon */}
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                <line x1="5" y1="12" x2="19" y2="12" />
                <polyline points="12 5 19 12 12 19" />
              </svg>
            </button>
          </div>

          {/* Command log */}
          <div
            ref={logRef}
            style={{
              maxHeight: '35vh',
              overflowY: 'auto',
              padding: '4px 0',
            }}
          >
            {commands.length === 0 && (
              <div style={{
                padding: '16px 12px',
                color: '#6b7280',
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: '0.7rem',
                textAlign: 'center',
                fontStyle: 'italic',
              }}>
                No commands yet. Type /help to get started.
              </div>
            )}
            {commands.map((cmd) => {
              const cfg = STATUS_CONFIG[cmd.status];
              return (
                <div
                  key={cmd.id}
                  style={{
                    padding: '6px 12px',
                    borderLeft: `2px solid ${cfg.color}`,
                    marginBottom: 1,
                    transition: 'background 0.15s',
                  }}
                  onMouseEnter={(e) => {
                    (e.currentTarget as HTMLElement).style.background = 'rgba(255, 255, 255, 0.03)';
                  }}
                  onMouseLeave={(e) => {
                    (e.currentTarget as HTMLElement).style.background = 'transparent';
                  }}
                >
                  {/* Command line */}
                  <div style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 8,
                    fontFamily: "'JetBrains Mono', monospace",
                    fontSize: '0.72rem',
                  }}>
                    {/* Status icon */}
                    <span style={{
                      color: cfg.color,
                      fontWeight: 700,
                      fontSize: '0.8rem',
                      width: 14,
                      textAlign: 'center',
                      flexShrink: 0,
                    }}>
                      {cfg.icon}
                    </span>
                    {/* Command text */}
                    <span style={{
                      color: '#c9d1d9',
                      flex: 1,
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      whiteSpace: 'nowrap',
                    }}>
                      <span style={{ color: '#FFD700', userSelect: 'none' }}>&gt; </span>
                      {cmd.text}
                    </span>
                    {/* Timestamp */}
                    <span style={{
                      color: '#484f58',
                      fontSize: '0.6rem',
                      flexShrink: 0,
                      whiteSpace: 'nowrap',
                    }}>
                      {formatTime(cmd.timestamp)}
                    </span>
                  </div>
                  {/* Result */}
                  {cmd.result && (
                    <div style={{
                      marginTop: 3,
                      paddingLeft: 22,
                      color: cmd.status === 'failed' ? '#FF4444' : '#8b949e',
                      fontFamily: "'JetBrains Mono', monospace",
                      fontSize: '0.65rem',
                      lineHeight: 1.5,
                    }}>
                      {cmd.result}
                    </div>
                  )}
                  {/* Running indicator */}
                  {cmd.status === 'running' && (
                    <div style={{
                      marginTop: 4,
                      paddingLeft: 22,
                      display: 'flex',
                      alignItems: 'center',
                      gap: 6,
                    }}>
                      <div style={{
                        width: 60,
                        height: 3,
                        background: '#21262d',
                        borderRadius: 2,
                        overflow: 'hidden',
                      }}>
                        <div style={{
                          width: '40%',
                          height: '100%',
                          background: 'linear-gradient(90deg, #FFB300, #FFD700)',
                          borderRadius: 2,
                          animation: 'shimmer 1.2s infinite',
                        }} />
                      </div>
                      <span style={{
                        color: '#FFB300',
                        fontFamily: "'JetBrains Mono', monospace",
                        fontSize: '0.6rem',
                      }}>
                        executing...
                      </span>
                    </div>
                  )}
                </div>
              );
            })}
          </div>

          {/* Footer — clear history */}
          {commands.length > 0 && (
            <div style={{
              padding: '6px 12px',
              borderTop: '1px solid #21262d',
              display: 'flex',
              justifyContent: 'flex-end',
            }}>
              <button
                onClick={clearHistory}
                style={{
                  background: 'none',
                  border: 'none',
                  color: '#484f58',
                  fontFamily: "'JetBrains Mono', monospace",
                  fontSize: '0.6rem',
                  cursor: 'pointer',
                  padding: '2px 6px',
                  transition: 'color 0.15s',
                }}
                onMouseEnter={(e) => {
                  (e.target as HTMLElement).style.color = '#8b949e';
                }}
                onMouseLeave={(e) => {
                  (e.target as HTMLElement).style.color = '#484f58';
                }}
              >
                clear history
              </button>
            </div>
          )}
        </div>
      )}

      {/* Inline keyframe styles for animations */}
      <style>{`
        @keyframes pulse {
          0%, 100% { opacity: 1; }
          50% { opacity: 0.5; }
        }
        @keyframes shimmer {
          0% { transform: translateX(-100%); }
          100% { transform: translateX(250%); }
        }
      `}</style>
    </div>
  );
};
