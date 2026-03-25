// src/components/PrimeRadiant/ChatWidget.tsx
// Floating Demerzel governance chatbot with voice input/output

import React, { useCallback, useEffect, useRef, useState } from 'react';
import type { GovernanceNode } from './types';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------
interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: number;
}

export interface ChatWidgetProps {
  selectedNode?: GovernanceNode | null;
}

// ---------------------------------------------------------------------------
// Web Speech API type declarations
// ---------------------------------------------------------------------------
declare global {
  interface Window {
    SpeechRecognition: typeof SpeechRecognition;
    webkitSpeechRecognition: typeof SpeechRecognition;
  }
}

// ---------------------------------------------------------------------------
// Mock backend — TODO: Connect to real Demerzel backend
// ---------------------------------------------------------------------------
const GOVERNANCE_RESPONSES: Record<string, string> = {
  policy: 'Our governance framework encompasses 39 policies covering alignment, rollback, self-modification, kaizen, reconnaissance, and more. Each policy includes versioning with explicit rationale.',
  constitution: 'The constitution hierarchy flows from the Asimov root (Articles 0-5) through the Demerzel mandate to the operational ethics (Articles 1-11). The Zeroth Law always takes precedence.',
  persona: 'There are 14 persona archetypes defined in YAML, each specifying role, capabilities, constraints, voice characteristics, and goal-directedness level.',
  zeroth: 'The Zeroth Law states: protect humanity and the ecosystem above all else. It overrides every other directive, including individual human instructions.',
  health: 'Governance health is measured through resilience scores, ERGOL (live executed bindings), and LOLLI (dead references). Confidence thresholds range from autonomous action (>=0.9) to escalation (<0.3).',
  logic: 'Demerzel uses tetravalent logic: True (verified), False (refuted), Unknown (insufficient evidence, triggers investigation), and Contradictory (conflicting evidence, triggers escalation).',
  test: 'The framework includes 80 behavioral test suites with 135+ test cases, ensuring all governance artifacts behave as specified.',
  schema: 'There are 37 JSON schemas validating persona definitions, belief states, reconnaissance data, conscience records, contracts, and context engine artifacts.',
};

async function askDemerzel(question: string, context?: GovernanceNode | null): Promise<string> {
  // Simulate network latency
  await new Promise((r) => setTimeout(r, 400 + Math.random() * 600));

  const q = question.toLowerCase();

  // Context-aware response if user is viewing a specific node
  if (context) {
    const typeInfo = GOVERNANCE_RESPONSES[context.type] ?? '';
    return `You are viewing "${context.name}" (${context.type}). ${context.description}${typeInfo ? ' ' + typeInfo : ''}`;
  }

  // Keyword matching
  for (const [key, response] of Object.entries(GOVERNANCE_RESPONSES)) {
    if (q.includes(key)) return response;
  }

  // Default
  return 'The Demerzel governance framework ensures all AI agent actions align with the constitutional hierarchy. The Asimov constitution (Articles 0-5) provides the root authority, with the Zeroth Law — protect humanity — overriding everything. Ask me about policies, personas, constitutions, logic, health, tests, or schemas.';
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const ChatWidget: React.FC<ChatWidgetProps> = ({ selectedNode }) => {
  const [isOpen, setIsOpen] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: 'welcome',
      role: 'assistant',
      content: 'I am Demerzel. Ask me about governance policies, constitutions, personas, or any artifact in the Prime Radiant.',
      timestamp: Date.now(),
    },
  ]);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isListening, setIsListening] = useState(false);
  const [ttsEnabled, setTtsEnabled] = useState(false);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const recognitionRef = useRef<SpeechRecognition | null>(null);

  // Auto-scroll to bottom
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  // Focus input when opened
  useEffect(() => {
    if (isOpen) inputRef.current?.focus();
  }, [isOpen]);

  // Cleanup speech on unmount
  useEffect(() => {
    return () => {
      recognitionRef.current?.abort();
      speechSynthesis.cancel();
    };
  }, []);

  const speakText = useCallback((text: string) => {
    if (!ttsEnabled) return;
    speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = navigator.language;
    utterance.rate = 0.95;
    utterance.pitch = 0.9;
    speechSynthesis.speak(utterance);
  }, [ttsEnabled]);

  const sendMessage = useCallback(async (text: string) => {
    const trimmed = text.trim();
    if (!trimmed || isLoading) return;

    // Stop any ongoing speech
    speechSynthesis.cancel();

    const userMsg: ChatMessage = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: trimmed,
      timestamp: Date.now(),
    };
    setMessages((prev) => [...prev, userMsg]);
    setInput('');
    setIsLoading(true);

    try {
      const response = await askDemerzel(trimmed, selectedNode);
      const botMsg: ChatMessage = {
        id: `bot-${Date.now()}`,
        role: 'assistant',
        content: response,
        timestamp: Date.now(),
      };
      setMessages((prev) => [...prev, botMsg]);
      speakText(response);
    } finally {
      setIsLoading(false);
    }
  }, [isLoading, selectedNode, speakText]);

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage(input);
    }
  }, [input, sendMessage]);

  const toggleListening = useCallback(() => {
    if (isListening) {
      recognitionRef.current?.stop();
      setIsListening(false);
      return;
    }

    const SpeechRecognitionAPI = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognitionAPI) return;

    const recognition = new SpeechRecognitionAPI();
    recognition.continuous = false;
    recognition.interimResults = true;
    recognition.lang = navigator.language;

    recognition.onresult = (event: SpeechRecognitionEvent) => {
      const transcript = Array.from(event.results)
        .map((r) => r[0].transcript)
        .join('');
      setInput(transcript);

      // Auto-send on final result
      if (event.results[event.results.length - 1].isFinal) {
        setIsListening(false);
        sendMessage(transcript);
      }
    };

    recognition.onerror = () => setIsListening(false);
    recognition.onend = () => setIsListening(false);

    recognitionRef.current = recognition;
    recognition.start();
    setIsListening(true);
  }, [isListening, sendMessage]);

  const toggleOpen = useCallback(() => setIsOpen((o) => !o), []);

  return (
    <>
      {/* Floating trigger button */}
      <button
        className="chat-widget__trigger"
        onClick={toggleOpen}
        title="Ask Demerzel"
        aria-label="Open Demerzel chat"
      >
        <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M12 2a8 8 0 0 1 8 8c0 3.5-2 6-4 7.5V20a2 2 0 0 1-2 2h-4a2 2 0 0 1-2-2v-2.5C6 16 4 13.5 4 10a8 8 0 0 1 8-8z" />
          <circle cx="9" cy="10" r="1" fill="currentColor" />
          <circle cx="15" cy="10" r="1" fill="currentColor" />
        </svg>
      </button>

      {/* Chat panel */}
      <div className={`chat-widget__panel ${isOpen ? 'chat-widget__panel--open' : ''}`}>
        {/* Header */}
        <div className="chat-widget__header">
          <span className="chat-widget__title">Ask Demerzel</span>
          <div className="chat-widget__header-actions">
            <button
              className={`chat-widget__tts-btn ${ttsEnabled ? 'chat-widget__tts-btn--active' : ''}`}
              onClick={() => { setTtsEnabled((v) => !v); if (ttsEnabled) speechSynthesis.cancel(); }}
              title={ttsEnabled ? 'Disable voice output' : 'Enable voice output'}
              aria-label="Toggle text-to-speech"
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5" />
                {ttsEnabled && (
                  <>
                    <path d="M15.54 8.46a5 5 0 0 1 0 7.07" />
                    <path d="M19.07 4.93a10 10 0 0 1 0 14.14" />
                  </>
                )}
              </svg>
            </button>
            <button className="chat-widget__close-btn" onClick={toggleOpen} aria-label="Close chat">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </div>
        </div>

        {/* Messages */}
        <div className="chat-widget__messages">
          {messages.map((msg) => (
            <div key={msg.id} className={`chat-widget__msg chat-widget__msg--${msg.role}`}>
              <div className="chat-widget__msg-bubble">{msg.content}</div>
            </div>
          ))}
          {isLoading && (
            <div className="chat-widget__msg chat-widget__msg--assistant">
              <div className="chat-widget__msg-bubble chat-widget__msg-bubble--loading">
                <span className="chat-widget__typing-dot" />
                <span className="chat-widget__typing-dot" />
                <span className="chat-widget__typing-dot" />
              </div>
            </div>
          )}
          <div ref={messagesEndRef} />
        </div>

        {/* Input area */}
        <div className="chat-widget__input-area">
          <input
            ref={inputRef}
            className="chat-widget__input"
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Ask about governance..."
            disabled={isLoading}
          />
          <button
            className={`chat-widget__mic-btn ${isListening ? 'chat-widget__mic-btn--active' : ''}`}
            onClick={toggleListening}
            title={isListening ? 'Stop listening' : 'Voice input'}
            aria-label="Toggle voice input"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="9" y="2" width="6" height="11" rx="3" />
              <path d="M5 10a7 7 0 0 0 14 0" />
              <line x1="12" y1="19" x2="12" y2="22" />
            </svg>
          </button>
          <button
            className="chat-widget__send-btn"
            onClick={() => sendMessage(input)}
            disabled={!input.trim() || isLoading}
            aria-label="Send message"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="22" y1="2" x2="11" y2="13" />
              <polygon points="22 2 15 22 11 13 2 9 22 2" />
            </svg>
          </button>
        </div>
      </div>
    </>
  );
};
