// src/components/PrimeRadiant/SeldonFaculty.ts
// Seldon University Faculty — model-to-department assignments for multi-LLM orchestration.
// Each faculty member maps to a specific LLM provider and academic department.

import { useState, useCallback, useEffect } from 'react';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface FacultyMember {
  providerId: string;
  name: string;
  title: string;
  department: string;
  specialty: string;
  color: string;
  model: string;
  icon: string;
}

export type FacultyStatus = 'online' | 'offline' | 'unknown';

export interface FacultyWithStatus extends FacultyMember {
  status: FacultyStatus;
}

// ---------------------------------------------------------------------------
// Faculty roster — hardcoded assignments
// ---------------------------------------------------------------------------

const FACULTY_ROSTER: FacultyMember[] = [
  {
    providerId: 'anthropic',
    name: 'Claude',
    title: 'Dean of Theory',
    department: 'Music Theory',
    specialty: 'Harmonic analysis, voice leading, set theory',
    color: '#CC8844',
    model: 'claude-opus-4-6',
    icon: 'A',
  },
  {
    providerId: 'mistral',
    name: 'Mistral Agent',
    title: 'Professor of Composition',
    department: 'Composition',
    specialty: 'Chord progressions, reharmonization, modal interchange',
    color: '#FF6F00',
    model: 'mistral-large-latest',
    icon: 'M',
  },
  {
    providerId: 'openai',
    name: 'GPT-4o',
    title: 'Professor of Music History',
    department: 'Music History',
    specialty: 'Historical context, genre evolution, composer analysis',
    color: '#10A37F',
    model: 'gpt-4o',
    icon: 'O',
  },
  {
    providerId: 'gemini',
    name: 'Gemini',
    title: 'Professor of Analysis',
    department: 'Analysis',
    specialty: 'Form analysis, pattern recognition, visual score reading',
    color: '#4285F4',
    model: 'gemini-2.5-pro',
    icon: 'G',
  },
  {
    providerId: 'ollama',
    name: 'Ollama (Local)',
    title: 'Instructor of Performance',
    department: 'Performance',
    specialty: 'Fretboard geometry, technique, practice routines',
    color: '#FFFFFF',
    model: 'llama3',
    icon: '\u{1F999}',
  },
];

// ---------------------------------------------------------------------------
// Accessors
// ---------------------------------------------------------------------------

export function getFaculty(): FacultyMember[] {
  return [...FACULTY_ROSTER];
}

export function getFacultyForDepartment(dept: string): FacultyMember | undefined {
  return FACULTY_ROSTER.find(
    (m) => m.department.toLowerCase() === dept.toLowerCase(),
  );
}

export function getFacultyByProvider(providerId: string): FacultyMember | undefined {
  return FACULTY_ROSTER.find(
    (m) => m.providerId.toLowerCase() === providerId.toLowerCase(),
  );
}

// ---------------------------------------------------------------------------
// Provider health check — reuses LLMStatus patterns
// ---------------------------------------------------------------------------

async function checkProviderOnline(providerId: string): Promise<FacultyStatus> {
  switch (providerId) {
    case 'ollama':
      try {
        const res = await fetch('/proxy/ollama/api/tags', {
          signal: AbortSignal.timeout(3000),
        });
        return res.ok ? 'online' : 'offline';
      } catch {
        return 'offline';
      }

    case 'anthropic':
      // Anthropic is always at least configured in this app
      return 'online';

    case 'openai':
    case 'gemini':
    case 'mistral': {
      const envKey = `VITE_${providerId.toUpperCase()}_CONFIGURED`;
      try {
        const val = (import.meta as { env?: Record<string, string> }).env?.[envKey];
        return val && val !== '' && val !== '0' && val !== 'false'
          ? 'online'
          : 'unknown';
      } catch {
        return 'unknown';
      }
    }

    default:
      return 'unknown';
  }
}

// ---------------------------------------------------------------------------
// Ask a faculty member a question — routes to correct provider
// ---------------------------------------------------------------------------

export async function askFacultyMember(
  member: FacultyMember,
  question: string,
): Promise<string> {
  const systemPrompt = [
    `You are ${member.name}, ${member.title} at Seldon University.`,
    `Department: ${member.department}.`,
    `Your specialty: ${member.specialty}.`,
    `Answer the following question from a student, drawing on your expertise.`,
  ].join(' ');

  // Route to provider-specific endpoint
  switch (member.providerId) {
    case 'anthropic': {
      // Use the chatbot proxy endpoint
      try {
        const res = await fetch('/api/chat', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            message: question,
            systemPrompt,
            model: member.model,
          }),
          signal: AbortSignal.timeout(30000),
        });
        if (res.ok) {
          const data: { reply?: string; response?: string } = await res.json();
          return data.reply ?? data.response ?? 'No response received.';
        }
        return `[${member.name}] Service returned ${res.status}`;
      } catch (err) {
        return `[${member.name}] Error: ${err instanceof Error ? err.message : 'unknown error'}`;
      }
    }

    case 'ollama': {
      try {
        const res = await fetch('/proxy/ollama/api/chat', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            model: member.model,
            messages: [
              { role: 'system', content: systemPrompt },
              { role: 'user', content: question },
            ],
            stream: false,
          }),
          signal: AbortSignal.timeout(60000),
        });
        if (res.ok) {
          const data: { message?: { content?: string } } = await res.json();
          return data.message?.content ?? 'No response received.';
        }
        return `[${member.name}] Service returned ${res.status}`;
      } catch (err) {
        return `[${member.name}] Error: ${err instanceof Error ? err.message : 'unknown error'}`;
      }
    }

    default:
      // For providers without direct proxy, return a placeholder
      return `[${member.name}] Provider "${member.providerId}" is not yet wired for direct queries. Configure the backend proxy to enable this faculty member.`;
  }
}

// ---------------------------------------------------------------------------
// React hook — useSeldonFaculty
// ---------------------------------------------------------------------------

export interface SeldonFacultyState {
  faculty: FacultyWithStatus[];
  loading: boolean;
  refresh: () => void;
  ask: (member: FacultyMember, question: string) => Promise<string>;
}

export function useSeldonFaculty(): SeldonFacultyState {
  const [faculty, setFaculty] = useState<FacultyWithStatus[]>(
    FACULTY_ROSTER.map((m) => ({ ...m, status: 'unknown' as FacultyStatus })),
  );
  const [loading, setLoading] = useState(false);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const results = await Promise.all(
        FACULTY_ROSTER.map(async (m) => {
          const status = await checkProviderOnline(m.providerId);
          return { ...m, status };
        }),
      );
      setFaculty(results);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
    const interval = setInterval(() => { refresh(); }, 60000);
    return () => clearInterval(interval);
  }, [refresh]);

  const ask = useCallback(
    (member: FacultyMember, question: string) => askFacultyMember(member, question),
    [],
  );

  return { faculty, loading, refresh, ask };
}
