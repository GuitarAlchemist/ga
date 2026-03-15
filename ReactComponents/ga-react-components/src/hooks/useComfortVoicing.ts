import { useState, useCallback } from 'react';

export interface VoicingWithAnalysis {
  chordName: string;
  frets: number[];
  difficulty: string;
  difficultyScore: number;
  handPosition: string;
  stringSet: string;
  cagedShape: string | null;
  semanticTags: string[];
  isBarreChord: boolean;
  barreInfo: string | null;
}

export interface ComfortRankedVoicing {
  voicing: VoicingWithAnalysis;
  stretch: number;
}

interface UseComfortVoicingResult {
  rankedVoicings: ComfortRankedVoicing[];
  easiestVoicing: ComfortRankedVoicing | null;
  isLoading: boolean;
  error: string | null;
  fetchComfortVoicings: (chordName: string, excludeBarre?: boolean) => Promise<void>;
}

export function useComfortVoicing(baseUrl = ''): UseComfortVoicingResult {
  const [rankedVoicings, setRankedVoicings] = useState<ComfortRankedVoicing[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchComfortVoicings = useCallback(async (chordName: string, excludeBarre = true) => {
    setIsLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams({ excludeBarre: String(excludeBarre) });
      const url = `${baseUrl}/api/contextual-chords/voicings/${encodeURIComponent(chordName)}/comfort?${params}`;
      const res = await fetch(url);
      if (!res.ok) {
        const msg = await res.text();
        setError(msg || `HTTP ${res.status}`);
        return;
      }
      const data: ComfortRankedVoicing[] = await res.json();
      setRankedVoicings(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setIsLoading(false);
    }
  }, [baseUrl]);

  return {
    rankedVoicings,
    easiestVoicing: rankedVoicings[0] ?? null,
    isLoading,
    error,
    fetchComfortVoicings,
  };
}
