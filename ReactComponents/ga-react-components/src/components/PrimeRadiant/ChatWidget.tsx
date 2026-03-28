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
  markdown?: boolean;  // render as markdown
  raw?: string;        // raw source to toggle
  suggestions?: string[];  // quick-reply suggestions
}

export interface ChatWidgetProps {
  selectedNode?: GovernanceNode | null;
  onNavigateToNode?: (nodeId: string) => void;
  onNavigateToPlanet?: (planetName: string) => void;
  onLoadArcGIS?: (layer: string) => void;
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
// Claude API proxy + fallback mock backend
// ---------------------------------------------------------------------------

// Claude proxy URL — set via env var or defaults to Cloudflare Worker
const CLAUDE_PROXY_URL = typeof import.meta !== 'undefined'
  ? (import.meta as { env?: Record<string, string> }).env?.VITE_CLAUDE_PROXY_URL ?? ''
  : '';

// User-provided API key (stored in localStorage for persistence)
function getUserApiKey(): string | null {
  try { return localStorage.getItem('ga-anthropic-key'); } catch { return null; }
}

async function askClaudeStreaming(
  messages: { role: string; content: string }[],
  onChunk: (text: string) => void,
  signal?: AbortSignal,
  locale?: string,
): Promise<string> {
  const proxyUrl = CLAUDE_PROXY_URL;
  const userKey = getUserApiKey();

  if (!proxyUrl && !userKey) {
    throw new Error('no-api'); // triggers fallback to mock
  }

  const url = proxyUrl || 'https://api.anthropic.com/v1/messages';
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (userKey && !proxyUrl) {
    headers['Authorization'] = `Bearer ${userKey}`;
  }

  // System prompt — auto-detect language from user input, or honor explicit locale
  const langName = locale && locale !== 'auto' ? SUPPORTED_LANGS.find(l => l.code === locale)?.name : null;
  const langInstruction = langName
    ? `You MUST respond entirely in ${langName}. Never switch to another language.`
    : 'Detect the language of the user\'s message and respond in that same language. Match the user\'s language exactly.';
  const system = `You are Demerzel, a governance AI from the Prime Radiant. ${langInstruction}`;

  const response = await fetch(url, {
    method: 'POST',
    headers,
    body: JSON.stringify({ system, messages, stream: true }),
    signal,
  });

  if (!response.ok) {
    throw new Error(`API error: ${response.status}`);
  }

  const reader = response.body?.getReader();
  if (!reader) throw new Error('No response body');

  const decoder = new TextDecoder();
  let fullText = '';
  let buffer = '';

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });
    const lines = buffer.split('\n');
    buffer = lines.pop() ?? '';

    for (const line of lines) {
      if (!line.startsWith('data: ')) continue;
      const data = line.slice(6);
      if (data === '[DONE]') continue;

      try {
        const parsed = JSON.parse(data);
        // Handle Anthropic SSE format
        if (parsed.type === 'content_block_delta' && parsed.delta?.text) {
          fullText += parsed.delta.text;
          onChunk(parsed.delta.text);
        }
      } catch {
        // Skip unparseable chunks
      }
    }
  }

  return fullText;
}

// Mock fallback responses
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
  action?: { type: 'navigate-planet'; planet: string } | { type: 'navigate-node'; query: string } | { type: 'arcgis'; layer: string };
  markdown?: boolean;
  raw?: string;
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

// ---------------------------------------------------------------------------
// Contextual suggestions — Demerzel proactively offers next steps
// ---------------------------------------------------------------------------
function getSuggestions(response: DemerzelResponse, question: string, context?: GovernanceNode | null): string[] {
  const q = question.toLowerCase();
  const suggestions: string[] = [];

  // After planet navigation → offer related actions
  if (response.action?.type === 'navigate-planet') {
    const planet = response.action.planet;
    if (planet === 'earth') {
      suggestions.push('Show ArcGIS borders', 'Take a screenshot', 'Go to Moon');
    } else {
      suggestions.push(`Tell me about ${planet}`, 'Go to Earth', 'Take a screenshot');
    }
    return suggestions;
  }

  // After ArcGIS layer load → offer other layers or navigation
  if (response.action?.type === 'arcgis') {
    suggestions.push('Show satellite imagery', 'Show topo map', 'List map layers');
    return suggestions;
  }

  // After artifact listing → offer to view one
  if (response.text.includes('artifacts:')) {
    if (q.includes('skill')) suggestions.push('Show skill demerzel-audit', 'Show skill seldon-research');
    else if (q.includes('persona')) suggestions.push('Show persona demerzel', 'Show persona seldon');
    else if (q.includes('polic')) suggestions.push('Show policy alignment', 'Show policy rollback');
    else if (q.includes('constitution')) suggestions.push('Show constitution default', 'Show constitution asimov');
    return suggestions;
  }

  // After viewing an artifact → offer related actions
  if (response.raw) {
    suggestions.push('List policies', 'List personas', 'What is tetravalent logic?');
    return suggestions;
  }

  // Context-aware: viewing a governance node
  if (context) {
    suggestions.push(`List ${context.type}s`, 'Show constitution', 'What is the Zeroth Law?');
    return suggestions;
  }

  // Default suggestions when no specific context
  if (q.includes('governance') || q.includes('constitution') || q.includes('zeroth')) {
    suggestions.push('List personas', 'List policies', 'Go to Jupiter');
  } else if (q.includes('planet') || q.includes('solar') || q.includes('space')) {
    suggestions.push('Go to Earth', 'Go to Saturn', 'Go to Mars');
  } else {
    // General fallback suggestions
    suggestions.push('Go to Earth', 'List skills', 'What is the Zeroth Law?');
  }

  return suggestions;
}

// Auto-detect language from text — returns language code or null if unclear
function detectLanguage(text: string): string | null {
  const t = text.toLowerCase();
  // French
  if (/[àâéèêëïîôùûüÿçœæ]/.test(t) || /\b(bonjour|salut|merci|oui|non|je|nous|vous|les|des|est|une?|dans|pour|sur|avec|qui|que|quoi|comment|pourquoi|montre|aller|voir)\b/i.test(t))
    return 'fr';
  // Spanish
  if (/[ñ¿¡]/.test(t) || /\b(hola|gracias|sí|por favor|qué|cómo|dónde|cuándo|buenas|buenos|muestra|ir a)\b/i.test(t))
    return 'es';
  // German
  if (/[äöüß]/.test(t) || /\b(hallo|danke|bitte|ja|nein|wie|warum|zeige|gehe zu|der|die|das|ist|und|ein|eine)\b/i.test(t))
    return 'de';
  // Italian
  if (/\b(ciao|grazie|sì|per favore|come|perché|mostra|vai a|buongiorno|sono|della|degli)\b/i.test(t))
    return 'it';
  // Portuguese
  if (/\b(olá|obrigado|sim|por favor|como|onde|quando|mostra|ir para|bom dia)\b/i.test(t))
    return 'pt';
  // Dutch
  if (/\b(hallo|dank|ja|nee|hoe|waarom|toon|ga naar|goedemorgen)\b/i.test(t))
    return 'nl';
  // Hindi (Devanagari script)
  if (/[\u0900-\u097F]/.test(t))
    return 'hi';
  // Arabic script
  if (/[\u0600-\u06FF]/.test(t))
    return 'ar';
  return null; // Can't determine — default to current locale
}

// Backward compat wrapper
function detectFrench(text: string): boolean {
  return detectLanguage(text) === 'fr';
}

// ── Artifact / Skill viewer ──
const GITHUB_RAW = 'https://raw.githubusercontent.com/GuitarAlchemist/Demerzel/master';

// Known artifact paths by type
const ARTIFACT_PATHS: Record<string, { dir: string; ext: string }> = {
  skill: { dir: '.claude/skills', ext: 'SKILL.md' },
  policy: { dir: 'policies', ext: '.yaml' },
  persona: { dir: 'personas', ext: '.persona.yaml' },
  constitution: { dir: 'constitutions', ext: '.md' },
  schema: { dir: 'schemas', ext: '.json' },
};

async function fetchArtifact(type: string, name: string): Promise<string | null> {
  const info = ARTIFACT_PATHS[type];
  if (!info) return null;
  const path = type === 'skill'
    ? `${info.dir}/${name}/${info.ext}`
    : `${info.dir}/${name}${info.ext}`;
  try {
    const resp = await fetch(`${GITHUB_RAW}/${path}`);
    if (!resp.ok) return null;
    return await resp.text();
  } catch { return null; }
}

async function listArtifacts(type: string): Promise<string[]> {
  const info = ARTIFACT_PATHS[type];
  if (!info) return [];
  try {
    const resp = await fetch(`https://api.github.com/repos/GuitarAlchemist/Demerzel/contents/${info.dir}`);
    if (!resp.ok) return [];
    const items = await resp.json() as { name: string; type: string }[];
    return items
      .filter(i => type === 'skill' ? i.type === 'dir' : i.name.endsWith(info.ext))
      .map(i => type === 'skill' ? i.name : i.name.replace(info.ext, ''));
  } catch { return []; }
}

// Simple markdown → HTML renderer (no external deps)
function renderMarkdown(md: string): string {
  return md
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    // Headers
    .replace(/^### (.+)$/gm, '<h4>$1</h4>')
    .replace(/^## (.+)$/gm, '<h3>$1</h3>')
    .replace(/^# (.+)$/gm, '<h2>$1</h2>')
    // Bold/italic
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.+?)\*/g, '<em>$1</em>')
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    // Code blocks
    .replace(/```[\w]*\n([\s\S]*?)```/g, '<pre><code>$1</code></pre>')
    // Lists
    .replace(/^- (.+)$/gm, '<li>$1</li>')
    .replace(/(<li>.*<\/li>\n?)+/g, (m) => `<ul>${m}</ul>`)
    // Horizontal rules
    .replace(/^---$/gm, '<hr/>')
    // Line breaks
    .replace(/\n\n/g, '<br/><br/>')
    .replace(/\n/g, '<br/>');
}

async function askDemerzel(question: string, context?: GovernanceNode | null, onLanguageDetected?: (lang: string) => void): Promise<DemerzelResponse> {
  const q = question.toLowerCase();
  const detectedLang = detectLanguage(q);
  if (detectedLang && onLanguageDetected) onLanguageDetected(detectedLang);
  const isFr = detectedLang === 'fr';

  // ── List artifacts: "list skills", "list policies", "show all personas" ──
  const listMatch = q.match(/\b(?:list|show all|ls|all)\s+(skills?|policies|personas?|constitutions?|schemas?)\b/);
  if (listMatch) {
    const typeRaw = listMatch[1].replace(/s$/, ''); // normalize plural
    const type = typeRaw === 'policie' ? 'policy' : typeRaw;
    const items = await listArtifacts(type);
    if (items.length === 0) return { text: `No ${type} artifacts found.` };
    const formatted = items.map(i => `• ${i}`).join('\n');
    return { text: `**${items.length} ${type} artifacts:**\n\n${formatted}\n\n_Say "show ${type} [name]" to view one._`, markdown: true };
  }

  // ── Show specific artifact: "show skill demerzel-audit", "show policy alignment" ──
  const showMatch = q.match(/\b(?:show|view|open|read|display|cat)\s+(skill|policy|persona|constitution|schema)\s+(.+)/);
  if (showMatch) {
    const type = showMatch[1];
    const name = showMatch[2].trim().replace(/['"]/g, '');
    const content = await fetchArtifact(type, name);
    if (!content) return { text: `Could not find ${type} "${name}". Try "list ${type}s" to see available ones.` };
    return { text: content, markdown: true, raw: content };
  }

  await new Promise((r) => setTimeout(r, 300 + Math.random() * 400));

  // Detect any planet mentioned in the input
  const mentionedPlanet = PLANET_NAMES.find((p) => q.includes(p));

  // Navigation intent — broad natural language matching
  const isNavIntent = /\b(go|fly|zoom|navigate|take me|show me|let'?s go|bring me|move|closer|approach|visit|look at|see)\b/.test(q)
    || /\b(aller|montre|voir|cherche|trouve|va vers|approche|visite|regarde)\b/.test(q);

  // Zoom / close-up intent
  const isZoomIntent = /\b(zoom|close[- ]?up|closer|magnif|enlarge|approach|agrand|rapproch)\b/i.test(q);

  // Screenshot intent
  const isScreenshotIntent = /\b(picture|photo|screenshot|capture|snap|image|screen\s?shot)\b/i.test(q);

  if (isScreenshotIntent) {
    // Take a screenshot of the canvas
    const canvas = document.querySelector('.prime-radiant canvas') as HTMLCanvasElement | null;
    if (canvas) {
      const dataUrl = canvas.toDataURL('image/png');
      const link = document.createElement('a');
      link.download = `prime-radiant-${mentionedPlanet || 'screenshot'}-${Date.now()}.png`;
      link.href = dataUrl;
      link.click();
      return {
        text: isFr
          ? `Capture enregistrée ! ${mentionedPlanet ? PLANET_FACTS[mentionedPlanet] ?? '' : ''}`
          : `Screenshot saved! ${mentionedPlanet ? PLANET_FACTS[mentionedPlanet] ?? '' : ''}`,
      };
    }
    return { text: isFr ? 'Impossible de capturer — aucun canvas trouvé.' : 'Could not capture — no canvas found.' };
  }

  // ArcGIS / map overlay intent
  const arcgisMatch = q.match(/\b(?:arcgis|gis|map|satellite|topo|borders|streets|imagery|carte|frontières)\b/i);
  if (arcgisMatch && (q.includes('earth') || q.includes('terre') || mentionedPlanet === 'earth')) {
    // Detect which layer
    let layer = 'borders';
    if (/\b(satellite|imagery|imagerie)\b/i.test(q)) layer = 'imagery';
    else if (/\b(street|rue|road|route)\b/i.test(q)) layer = 'streets';
    else if (/\b(topo|terrain|relief)\b/i.test(q)) layer = 'topo';
    else if (/\b(dark|sombre|noir)\b/i.test(q)) layer = 'darkgray';
    else if (/\b(border|frontier|frontière|country|pays)\b/i.test(q)) layer = 'borders';

    return {
      text: isFr
        ? `Chargement de la couche ArcGIS "${layer}" sur la Terre... Les tuiles vont apparaître sur le globe.`
        : `Loading ArcGIS "${layer}" layer on Earth... Tiles will appear on the globe.`,
      action: { type: 'arcgis', layer },
    };
  }

  // List available map layers
  if (/\b(list|show).*(map|layer|gis|carte|couche)\b/i.test(q)) {
    return {
      text: isFr
        ? '**Couches ArcGIS disponibles:**\n\n• imagery — Imagerie satellite\n• streets — Plan des rues\n• topo — Carte topographique\n• borders — Frontières et noms de lieux\n• darkgray — Fond sombre\n\n_Dites "arcgis borders on earth" pour charger._'
        : '**Available ArcGIS layers:**\n\n• imagery — Satellite imagery\n• streets — Street map\n• topo — Topographic map\n• borders — Country borders & labels\n• darkgray — Dark gray base\n\n_Say "arcgis borders on earth" to load._',
      markdown: true,
    };
  }

  if (mentionedPlanet && (isNavIntent || isZoomIntent)) {
    return {
      text: isFr
        ? `Navigation vers ${mentionedPlanet.charAt(0).toUpperCase() + mentionedPlanet.slice(1)}. ${PLANET_FACTS[mentionedPlanet] ?? ''}`
        : `Navigating to ${mentionedPlanet.charAt(0).toUpperCase() + mentionedPlanet.slice(1)}. ${PLANET_FACTS[mentionedPlanet] ?? ''}`,
      action: { type: 'navigate-planet', planet: mentionedPlanet },
    };
  }

  // Explicit navigation command patterns — "find X", "go to X"
  const navMatch = q.match(/(?:find|go to|show|navigate to|fly to|take me to|montre|aller à|voir|cherche|trouve|va vers)\s+(.+)/);
  if (navMatch) {
    const target = navMatch[1].trim();
    return {
      text: isFr ? `Recherche de « ${target} » dans le graphe de gouvernance...` : `Searching for "${target}" in the governance graph...`,
      action: { type: 'navigate-node', query: target },
    };
  }

  // Planet info — just asking about a planet (no action intent)
  if (mentionedPlanet) {
    return { text: PLANET_FACTS[mentionedPlanet] ?? `${mentionedPlanet} is part of our solar system.` };
  }

  // Context-aware
  if (context) {
    const typeInfo = GOVERNANCE_RESPONSES[context.type] ?? '';
    const text = isFr
      ? `Vous consultez « ${context.name} » (${context.type}). ${context.description}${typeInfo ? ' ' + typeInfo : ''}`
      : `You are viewing "${context.name}" (${context.type}). ${context.description}${typeInfo ? ' ' + typeInfo : ''}`;
    return { text };
  }

  // Governance keyword matching
  for (const [key, response] of Object.entries(GOVERNANCE_RESPONSES)) {
    if (q.includes(key)) return { text: response };
  }

  return {
    text: isFr
      ? 'Je suis Demerzel. Posez-moi des questions sur la gouvernance (politiques, constitutions, personas, logique) ou dites « montre la Terre » pour naviguer dans le système solaire.'
      : 'I am Demerzel. Ask me about governance (policies, constitutions, personas, logic) or say "find Earth" to navigate the solar system. Try "go to Jupiter" or "show Mars".',
  };
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------
// Supported languages — full BCP-47 locale for better speech recognition accuracy
const SUPPORTED_LANGS = [
  { code: 'auto', locale: '', label: 'Auto', name: '' },
  { code: 'en', locale: 'en-US', label: 'EN', name: 'English' },
  { code: 'fr', locale: 'fr-FR', label: 'FR', name: 'Français' },
  { code: 'es', locale: 'es-ES', label: 'ES', name: 'Español' },
  { code: 'pt', locale: 'pt-BR', label: 'PT', name: 'Português' },
  { code: 'de', locale: 'de-DE', label: 'DE', name: 'Deutsch' },
  { code: 'it', locale: 'it-IT', label: 'IT', name: 'Italiano' },
  { code: 'nl', locale: 'nl-NL', label: 'NL', name: 'Nederlands' },
  { code: 'hi', locale: 'hi-IN', label: 'HI', name: 'हिन्दी' },
  { code: 'ar', locale: 'ar-SA', label: 'AR', name: 'العربية' },
] as const;

/** Get the full BCP-47 locale for the selected language code */
function getLocaleForCode(code: string): string {
  const lang = SUPPORTED_LANGS.find(l => l.code === code);
  return lang?.locale || navigator.language || 'en-US';
}

const WELCOME_MESSAGES: Record<string, string> = {
  en: 'I am Demerzel. Ask me about governance policies, constitutions, personas, or any artifact in the Prime Radiant.',
  fr: 'Je suis Demerzel. Posez-moi des questions sur les politiques de gouvernance, les constitutions, les personas ou tout artefact du Prime Radiant.',
  es: 'Soy Demerzel. Pregúntame sobre políticas de gobernanza, constituciones, personas o cualquier artefacto en el Prime Radiant.',
  pt: 'Eu sou Demerzel. Pergunte-me sobre políticas de governança, constituições, personas ou qualquer artefato no Prime Radiant.',
  de: 'Ich bin Demerzel. Fragen Sie mich zu Governance-Richtlinien, Verfassungen, Personas oder anderen Artefakten im Prime Radiant.',
  it: 'Sono Demerzel. Chiedimi delle politiche di governance, costituzioni, personas o qualsiasi artefatto nel Prime Radiant.',
  nl: 'Ik ben Demerzel. Vraag me over governance-beleid, grondwetten, persona\'s of artefacten in de Prime Radiant.',
  hi: 'मैं डेमरज़ेल हूँ। मुझसे गवर्नेंस नीतियों, संविधानों, पर्सोना या प्राइम रेडिएंट के किसी भी आर्टिफैक्ट के बारे में पूछें।',
  ar: 'أنا ديميرزيل. اسألني عن سياسات الحوكمة أو الدساتير أو الشخصيات أو أي عنصر في برايم راديانت.',
};

function detectLocale(): string {
  return 'auto';
}

export const ChatWidget: React.FC<ChatWidgetProps> = ({ selectedNode, onNavigateToNode, onNavigateToPlanet, onLoadArcGIS }) => {
  const [locale, setLocale] = useState(detectLocale);
  const [isOpen, setIsOpen] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>(() => [{
    id: 'welcome',
    role: 'assistant',
    content: WELCOME_MESSAGES[detectLocale()] ?? WELCOME_MESSAGES.en,
    timestamp: Date.now(),
    suggestions: ['Go to Earth', 'List personas', 'What is the Zeroth Law?'],
  }]);
  const [input, setInput] = useState('');
  const [rawViewIds, setRawViewIds] = useState<Set<string>>(new Set());
  const [showContext, setShowContext] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [isListening, setIsListening] = useState(false);
  const [voiceState, setVoiceState] = useState<'idle' | 'listening' | 'processing' | 'understood'>('idle');
  const [ttsEnabled, setTtsEnabled] = useState(true); // Demerzel speaks by default
  const [alwaysListen, setAlwaysListen] = useState(false); // Continuous listening mode

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const recognitionRef = useRef<SpeechRecognition | null>(null);
  const isSpeakingRef = useRef(false);
  const startListeningRef = useRef<() => void>(() => {});
  const panelRef = useRef<HTMLDivElement>(null);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const audioBlobUrlRef = useRef<string | null>(null);

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
      stopAudio();
      speechSynthesis.cancel();
    };
  }, []);

  /** Stop any currently playing audio and revoke blob URL */
  const stopAudio = useCallback(() => {
    if (audioRef.current) {
      audioRef.current.pause();
      audioRef.current = null;
    }
    if (audioBlobUrlRef.current) {
      URL.revokeObjectURL(audioBlobUrlRef.current);
      audioBlobUrlRef.current = null;
    }
  }, []);

  /** Fallback: use browser Web Speech API */
  const speakWithBrowserTts = useCallback((text: string) => {
    speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.rate = 0.9;
    utterance.pitch = 0.85;
    const voices = speechSynthesis.getVoices();

    // Detect language: use explicit locale, or sniff from text for auto mode
    let lang = locale;
    if (lang === 'auto') {
      // Simple heuristic: check for common non-ASCII or language-specific patterns
      if (/[àâéèêëïîôùûüÿçœæ]/.test(text) || /\b(je|nous|vous|les|des|est|une?|dans|pour|sur|avec)\b/i.test(text)) lang = 'fr';
      else if (/[áéíóúñ¿¡]/.test(text) || /\b(el|los|las|una?|por|para|con|que)\b/i.test(text)) lang = 'es';
      else if (/[äöüß]/.test(text) || /\b(der|die|das|ist|und|ein|eine)\b/i.test(text)) lang = 'de';
      else if (/[àèéìíòóùú]/.test(text) || /\b(il|gli|della|sono|che|per|una?)\b/i.test(text)) lang = 'it';
      else lang = 'en';
    }

    utterance.lang = getLocaleForCode(lang);
    // Pick a voice matching detected/selected language
    const findVoice = (prefix: string) =>
      voices.find((v) => v.name.includes('Google') && v.lang.startsWith(prefix))
      ?? voices.find((v) => v.lang.startsWith(prefix) && v.localService === false)
      ?? voices.find((v) => v.lang.startsWith(prefix));
    const preferred = lang === 'en'
      ? (voices.find((v) => v.name.includes('Google UK English Female'))
        ?? voices.find((v) => v.name.includes('Samantha'))
        ?? findVoice('en'))
      : findVoice(lang);
    if (preferred) utterance.voice = preferred;
    speechSynthesis.speak(utterance);
  }, [locale]);

  /** Speak text via Voxtral backend, falling back to browser TTS on failure */
  const speakText = useCallback(async (text: string) => {
    if (!ttsEnabled) return;
    stopAudio();
    speechSynthesis.cancel();

    // Pause recognition while speaking to prevent feedback
    isSpeakingRef.current = true;
    if (recognitionRef.current && isListening) {
      try { recognitionRef.current.stop(); } catch { /* ignore */ }
    }

    const onSpeechDone = () => {
      isSpeakingRef.current = false;
      // Resume always-listen after TTS finishes (use ref to avoid circular dep)
      if (alwaysListen) setTimeout(() => startListeningRef.current(), 300);
    };

    try {
      const baseUrl = typeof import.meta !== 'undefined'
        ? (import.meta as { env?: Record<string, string> }).env?.VITE_API_BASE_URL ?? 'https://localhost:7001'
        : 'https://localhost:7001';

      const response = await fetch(`${baseUrl}/api/tts`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text }),
      });

      if (!response.ok) {
        speakWithBrowserTts(text);
        // Browser TTS end detection
        speechSynthesis.addEventListener('end', onSpeechDone, { once: true });
        setTimeout(onSpeechDone, 10000); // safety fallback
        return;
      }

      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      audioBlobUrlRef.current = url;

      const audio = new Audio(url);
      audioRef.current = audio;
      audio.onended = () => {
        URL.revokeObjectURL(url);
        audioBlobUrlRef.current = null;
        audioRef.current = null;
        onSpeechDone();
      };
      await audio.play();
    } catch {
      speakWithBrowserTts(text);
      speechSynthesis.addEventListener('end', onSpeechDone, { once: true });
      setTimeout(onSpeechDone, 10000);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps -- startListening ref avoids circular dep
  }, [ttsEnabled, stopAudio, speakWithBrowserTts, alwaysListen]);

  const sendMessage = useCallback(async (text: string) => {
    const trimmed = text.trim();
    if (!trimmed || isLoading) return;

    // Stop any ongoing speech (both Voxtral audio and browser TTS)
    stopAudio();
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

    // Create placeholder assistant message for streaming
    const botMsgId = `bot-${Date.now()}`;
    const botMsg: ChatMessage = {
      id: botMsgId,
      role: 'assistant',
      content: '',
      timestamp: Date.now(),
    };
    setMessages((prev) => [...prev, botMsg]);

    try {
      // Build conversation history for Claude
      const apiMessages = messages
        .filter(m => m.role === 'user' || m.role === 'assistant')
        .slice(-10) // Last 10 messages for context
        .map(m => ({ role: m.role, content: m.content }));
      const ctxHint = selectedNode ? ` [Context: viewing "${selectedNode.name}" (${selectedNode.type})]` : '';
      apiMessages.push({ role: 'user', content: trimmed + ctxHint });

      const fullText = await askClaudeStreaming(
        apiMessages,
        (chunk) => {
          // Stream chunks into the placeholder message
          setMessages((prev) => prev.map(m =>
            m.id === botMsgId ? { ...m, content: m.content + chunk } : m,
          ));
        },
        undefined,
        locale,
      );

      // Parse navigation actions from Claude's response
      const actionMatch = fullText.match(/\{"action":"navigate[^}]+\}/);
      if (actionMatch) {
        try {
          const action = JSON.parse(actionMatch[0]);
          if (action.action === 'navigate-planet' && action.planet && onNavigateToPlanet) {
            onNavigateToPlanet(action.planet);
          } else if (action.target && onNavigateToNode) {
            onNavigateToNode(action.target);
          }
        } catch { /* ignore parse errors */ }
      }

      // Add contextual suggestions after Claude responds
      const claudeSuggestions = getSuggestions(
        { text: fullText },
        trimmed,
        selectedNode,
      );
      setMessages((prev) => prev.map(m =>
        m.id === botMsgId ? { ...m, suggestions: claudeSuggestions } : m,
      ));

      void speakText(fullText);
    } catch {
      // Try backend Ollama chatbot first, then fall back to mock patterns
      let response: DemerzelResponse | null = null;
      try {
        const baseUrl = typeof window !== 'undefined' ? window.location.origin : '';
        const chatRes = await fetch(`${baseUrl}/api/chatbot/chat`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ message: trimmed }),
          signal: AbortSignal.timeout(15000),
        });
        if (chatRes.ok) {
          const data = await chatRes.json();
          const answer = data.naturalLanguageAnswer || data.answer || '';
          if (answer) {
            response = { text: answer };
            // Auto-detect language from the response
            const lang = detectLanguage(trimmed);
            if (lang && lang !== locale) setLocale(lang);
          }
        }
      } catch { /* Backend unavailable — fall through to mock */ }

      // Final fallback: client-side mock patterns
      if (!response) {
        response = await askDemerzel(trimmed, selectedNode, (lang) => {
          if (lang !== locale) setLocale(lang);
        });
      }

      const suggestions = getSuggestions(response, trimmed, selectedNode);
      setMessages((prev) => prev.map(m =>
        m.id === botMsgId ? { ...m, content: response!.text, markdown: response!.markdown, raw: response!.raw, suggestions } : m,
      ));
      void speakText(response.text);

      if (response.action) {
        if (response.action.type === 'navigate-planet' && onNavigateToPlanet) {
          onNavigateToPlanet(response.action.planet);
        } else if (response.action.type === 'navigate-node' && onNavigateToNode) {
          onNavigateToNode(response.action.query);
        } else if (response.action.type === 'arcgis' && onLoadArcGIS) {
          onLoadArcGIS(response.action.layer);
        }
      }
    } finally {
      setIsLoading(false);
    }
  }, [isLoading, messages, selectedNode, speakText, stopAudio, onNavigateToNode, onNavigateToPlanet, onLoadArcGIS]);

  const handleKeyDown = useCallback((e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage(input);
    }
  }, [input, sendMessage]);

  const startListening = useCallback(() => {
    // Don't listen while TTS is playing — prevents feedback loop
    if (isSpeakingRef.current) {
      if (alwaysListen) setTimeout(() => startListening(), 500);
      return;
    }
    const SpeechRecognitionAPI = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognitionAPI) return;

    const recognition = new SpeechRecognitionAPI();
    recognition.continuous = false;
    recognition.interimResults = true;
    recognition.lang = getLocaleForCode(locale);

    recognition.onresult = (event: SpeechRecognitionEvent) => {
      const transcript = Array.from(event.results)
        .map((r) => r[0].transcript)
        .join('');
      setInput(transcript);

      if (event.results[event.results.length - 1].isFinal) {
        setVoiceState('processing');
        sendMessage(transcript).then(() => {
          setVoiceState('understood');
          setTimeout(() => setVoiceState('idle'), 1500);
        });
        // Always-listen restart is now handled by speakText's onSpeechDone callback
        // This prevents the mic from picking up TTS audio
        if (!alwaysListen) {
          setIsListening(false);
        }
      }
    };

    recognition.onerror = () => {
      setIsListening(false);
      if (voiceState === 'listening') setVoiceState('idle');
      // Retry in always-listen mode
      if (alwaysListen) setTimeout(() => startListening(), 1000);
    };
    recognition.onend = () => {
      // In always-listen mode, restart only if not currently speaking TTS
      if (alwaysListen && !isSpeakingRef.current) {
        setTimeout(() => startListening(), 500);
      } else if (!alwaysListen) {
        setIsListening(false);
        if (voiceState === 'listening') setVoiceState('idle');
      }
    };

    recognitionRef.current = recognition;
    recognition.start();
    setIsListening(true);
    setVoiceState('listening');
  }, [sendMessage, alwaysListen, locale]);
  startListeningRef.current = startListening;

  const toggleListening = useCallback(() => {
    if (isListening) {
      recognitionRef.current?.stop();
      setIsListening(false);
      setVoiceState('idle');
      return;
    }
    startListening();
  }, [isListening, startListening]);

  const toggleOpen = useCallback(() => setIsOpen((o) => !o), []);

  // Global hotkey: hold V to voice input (works even when collapsed)
  useEffect(() => {
    let held = false;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'v' && !e.ctrlKey && !e.metaKey && !e.altKey && !held && !(e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement)) {
        held = true;
        if (!isListening) startListening();
      }
    };
    const onKeyUp = (e: KeyboardEvent) => {
      if (e.key === 'v' && held) {
        held = false;
        // Don't stop if always-listen is on
        if (!alwaysListen && isListening) {
          recognitionRef.current?.stop();
        }
      }
    };
    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('keyup', onKeyUp);
    };
  }, [isListening, startListening, alwaysListen]);

  const triggerClasses = [
    'chat-widget__trigger',
    voiceState !== 'idle' ? `chat-widget__trigger--${voiceState}` : '',
    alwaysListen ? 'chat-widget__trigger--always-listen' : '',
  ].filter(Boolean).join(' ');

  return (
    <>
      {/* Floating trigger button */}
      <button
        className={triggerClasses}
        onClick={toggleOpen}
        title="Ask Demerzel (hold V for voice)"
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
              onClick={() => { setTtsEnabled((v) => !v); if (ttsEnabled) { stopAudio(); speechSynthesis.cancel(); } }}
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
            <select
              className="chat-widget__lang-select"
              value={locale}
              onChange={(e) => {
                const newLang = e.target.value;
                setLocale(newLang);
                // Update welcome message if it's the only message
                setMessages(prev => {
                  if (prev.length === 1 && prev[0].id === 'welcome') {
                    return [{ ...prev[0], content: WELCOME_MESSAGES[newLang] ?? WELCOME_MESSAGES.en }];
                  }
                  return prev;
                });
              }}
              title="Response language"
              aria-label="Select response language"
            >
              {SUPPORTED_LANGS.map(l => (
                <option key={l.code} value={l.code}>{l.label}</option>
              ))}
            </select>
            <button
              className={`chat-widget__context-btn ${showContext ? 'chat-widget__context-btn--active' : ''}`}
              onClick={() => setShowContext((v) => !v)}
              title="Show Demerzel's context"
              aria-label="Show context info"
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10" />
                <line x1="12" y1="16" x2="12" y2="12" />
                <line x1="12" y1="8" x2="12.01" y2="8" />
              </svg>
            </button>
            <button
              className="chat-widget__clear-btn"
              onClick={() => {
                stopAudio();
                speechSynthesis.cancel();
                setMessages([{
                  id: 'welcome',
                  role: 'assistant',
                  content: WELCOME_MESSAGES[locale] ?? WELCOME_MESSAGES.en,
                  timestamp: Date.now(),
                }]);
              }}
              title="Clear conversation"
              aria-label="Clear conversation"
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="3 6 5 6 21 6" />
                <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
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

        {/* Context popover */}
        {showContext && (
          <div className="chat-widget__context-popover">
            <div className="chat-widget__context-title">Demerzel's Context</div>
            <div className="chat-widget__context-row">
              <span className="chat-widget__context-label">Backend</span>
              <span className="chat-widget__context-value">
                {CLAUDE_PROXY_URL ? 'Claude API (streaming)' : getUserApiKey() ? 'Claude API (direct)' : 'Local fallback'}
              </span>
            </div>
            <div className="chat-widget__context-row">
              <span className="chat-widget__context-label">Language</span>
              <span className="chat-widget__context-value">
                {locale === 'auto' ? 'Auto-detect' : SUPPORTED_LANGS.find(l => l.code === locale)?.name ?? locale}
              </span>
            </div>
            <div className="chat-widget__context-row">
              <span className="chat-widget__context-label">Selected node</span>
              <span className="chat-widget__context-value">
                {selectedNode ? `${selectedNode.name} (${selectedNode.type})` : 'None'}
              </span>
            </div>
            <div className="chat-widget__context-row">
              <span className="chat-widget__context-label">Messages</span>
              <span className="chat-widget__context-value">{messages.length}</span>
            </div>
            <div className="chat-widget__context-row">
              <span className="chat-widget__context-label">Voice</span>
              <span className="chat-widget__context-value">
                {ttsEnabled ? 'On' : 'Off'}{alwaysListen ? ' / Always-listen' : ''}
              </span>
            </div>
            <div className="chat-widget__context-desc">
              Demerzel responds using governance knowledge from the Prime Radiant — constitutions,
              policies, personas, and schemas. She can navigate the solar system and browse artifacts
              from the Demerzel governance repo.
            </div>
          </div>
        )}

        {/* Messages */}
        <div className="chat-widget__messages">
          {messages.map((msg) => {
            const showRaw = rawViewIds.has(msg.id);
            const hasRaw = !!msg.raw;
            const isMarkdown = !!msg.markdown && !showRaw;
            return (
              <div key={msg.id} className={`chat-widget__msg chat-widget__msg--${msg.role}`}>
                <div className="chat-widget__msg-bubble">
                  {isMarkdown
                    ? <div className="chat-widget__md" dangerouslySetInnerHTML={{ __html: renderMarkdown(msg.content) }} />
                    : msg.content}
                  {msg.role === 'assistant' && msg.content && (
                    <div className="chat-widget__msg-actions">
                      {hasRaw && (
                        <button
                          className="chat-widget__raw-toggle"
                          onClick={() => setRawViewIds(prev => {
                            const next = new Set(prev);
                            if (next.has(msg.id)) next.delete(msg.id); else next.add(msg.id);
                            return next;
                          })}
                          title={showRaw ? 'Show rendered' : 'Show raw source'}
                        >
                          {showRaw ? '📄' : '</>'}
                        </button>
                      )}
                      <button
                        className="chat-widget__replay-btn"
                        onClick={() => void speakText(msg.content)}
                        title="Replay this message"
                        aria-label="Replay message"
                      >
                        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5" />
                          <path d="M15.54 8.46a5 5 0 0 1 0 7.07" />
                        </svg>
                      </button>
                    </div>
                  )}
                  {/* Suggestion chips — only on the last assistant message */}
                  {msg.role === 'assistant' && msg.suggestions && msg.suggestions.length > 0
                    && msg.id === messages.filter(m => m.role === 'assistant').at(-1)?.id && (
                    <div className="chat-widget__suggestions">
                      {msg.suggestions.map((suggestion) => (
                        <button
                          key={suggestion}
                          className="chat-widget__suggestion-chip"
                          onClick={() => sendMessage(suggestion)}
                          disabled={isLoading}
                        >
                          {suggestion}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            );
          })}
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
            className={`chat-widget__always-listen-btn ${alwaysListen ? 'chat-widget__always-listen-btn--active' : ''}`}
            onClick={() => {
              const next = !alwaysListen;
              setAlwaysListen(next);
              if (next && !isListening) startListening();
              if (!next && isListening) { recognitionRef.current?.stop(); setIsListening(false); }
            }}
            title={alwaysListen ? 'Disable always-listen mode' : 'Enable always-listen mode (continuous conversation)'}
            aria-label="Toggle always-listen mode"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <path d="M4 12h16" />
              <path d="M4 6h16" />
              <path d="M4 18h16" />
              {alwaysListen && <circle cx="20" cy="4" r="3" fill="#33CC66" stroke="none" />}
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
