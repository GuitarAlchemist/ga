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
  onNavigateToNode?: (nodeId: string) => void;
  onNavigateToPlanet?: (planetName: string) => void;
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

// Navigation command result
interface DemerzelResponse {
  text: string;
  action?: { type: 'navigate-planet'; planet: string } | { type: 'navigate-node'; query: string };
}

const PLANET_NAMES = ['sun', 'mercury', 'venus', 'earth', 'mars', 'jupiter', 'saturn', 'uranus', 'neptune', 'moon'];
const PLANET_FACTS: Record<string, string> = {
  sun: 'Our star — a G-type main-sequence star, 4.6 billion years old. The corona reaches millions of degrees.',
  mercury: 'Closest to the Sun. No atmosphere, extreme temperature swings. Cratered like our Moon.',
  venus: 'Earth\'s toxic twin. Thick sulfuric acid clouds, 470°C surface. Rotates backwards.',
  earth: 'Home. The only known planet with life. 71% ocean, one moon, perfect distance from the Sun.',
  mars: 'The red planet. Thin CO₂ atmosphere, polar ice caps, Olympus Mons — tallest volcano in the solar system.',
  jupiter: 'Gas giant king. The Great Red Spot is a storm larger than Earth. 95 known moons.',
  saturn: 'The ringed wonder. Rings are 99.9% ice. Less dense than water — it would float.',
  uranus: 'The sideways planet. Tilted 98°, rolls around the Sun. Pale cyan methane atmosphere.',
  neptune: 'The windiest planet. Supersonic winds at 2,100 km/h. Deep blue from methane.',
  moon: 'Earth\'s companion. Tidally locked — same face always toward us. 384,400 km away.',
};

async function askDemerzel(question: string, context?: GovernanceNode | null): Promise<DemerzelResponse> {
  await new Promise((r) => setTimeout(r, 300 + Math.random() * 400));
  const q = question.toLowerCase();

  // Navigation commands — "find X", "go to X", "show X", "zoom to X"
  const navMatch = q.match(/(?:find|go to|show|zoom to|navigate to|fly to|take me to)\s+(.+)/);
  if (navMatch) {
    const target = navMatch[1].trim();
    const planet = PLANET_NAMES.find((p) => target.includes(p));
    if (planet) {
      return {
        text: `Navigating to ${planet.charAt(0).toUpperCase() + planet.slice(1)}. ${PLANET_FACTS[planet] ?? ''}`,
        action: { type: 'navigate-planet', planet },
      };
    }
    // Try as governance node
    return {
      text: `Searching for "${target}" in the governance graph...`,
      action: { type: 'navigate-node', query: target },
    };
  }

  // Planet info — just asking about a planet
  for (const name of PLANET_NAMES) {
    if (q.includes(name)) {
      return { text: PLANET_FACTS[name] ?? `${name} is part of our solar system.` };
    }
  }

  // Context-aware
  if (context) {
    const typeInfo = GOVERNANCE_RESPONSES[context.type] ?? '';
    return { text: `You are viewing "${context.name}" (${context.type}). ${context.description}${typeInfo ? ' ' + typeInfo : ''}` };
  }

  // Governance keyword matching
  for (const [key, response] of Object.entries(GOVERNANCE_RESPONSES)) {
    if (q.includes(key)) return { text: response };
  }

  return { text: 'I am Demerzel. Ask me about governance (policies, constitutions, personas, logic) or say "find Earth" to navigate the solar system. Try "go to Jupiter" or "show Mars".' };
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
export const ChatWidget: React.FC<ChatWidgetProps> = ({ selectedNode, onNavigateToNode, onNavigateToPlanet }) => {
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
  const panelRef = useRef<HTMLDivElement>(null);

  // Close on click outside
  useEffect(() => {
    if (!isOpen) return;
    const handler = (e: MouseEvent) => {
      if (panelRef.current && !panelRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [isOpen]);

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
    utterance.rate = 0.9;
    utterance.pitch = 0.85;
    // Pick a good voice — prefer Google UK English or similar natural voice
    const voices = speechSynthesis.getVoices();
    const preferred = voices.find((v) => v.name.includes('Google UK English Female'))
      ?? voices.find((v) => v.name.includes('Google') && v.lang.startsWith('en'))
      ?? voices.find((v) => v.name.includes('Samantha'))
      ?? voices.find((v) => v.lang.startsWith('en') && v.localService === false)
      ?? voices[0];
    if (preferred) utterance.voice = preferred;
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
        content: response.text,
        timestamp: Date.now(),
      };
      setMessages((prev) => [...prev, botMsg]);
      speakText(response.text);

      // Execute navigation action if present
      if (response.action) {
        if (response.action.type === 'navigate-planet' && onNavigateToPlanet) {
          onNavigateToPlanet(response.action.planet);
        } else if (response.action.type === 'navigate-node' && onNavigateToNode) {
          onNavigateToNode(response.action.query);
        }
      }
    } finally {
      setIsLoading(false);
    }
  }, [isLoading, selectedNode, speakText, onNavigateToNode, onNavigateToPlanet]);

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
      <div ref={panelRef} className={`chat-widget__panel ${isOpen ? 'chat-widget__panel--open' : ''}`}>
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
