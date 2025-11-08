import type {
  ApiResponse,
  KeyNotes,
  ScaleDegree,
  ChordInContext,
  ChordProgressionDefinition,
} from '../types/music';
import type { KeyChordFilters } from '../store/atoms';

const buildUrl = (baseUrl: string, path: string) => {
  const trimmedBase = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
  const normalizedPath = path.startsWith('/') ? path : `/${path}`;
  return `${trimmedBase}${normalizedPath}`;
};

const isApiResponse = <T>(value: unknown): value is ApiResponse<T> => {
  if (!value || typeof value !== 'object') {
    return false;
  }

  return 'success' in value && 'data' in value;
};

const parseJson = async <T>(response: Response): Promise<T> => {
  if (!response.ok) {
    const errorBody = await response.text();
    throw new Error(
      `API request failed: ${response.status} ${response.statusText}${
        errorBody ? ` - ${errorBody}` : ''
      }`,
    );
  }

  const json = await response.json();
  if (isApiResponse<T>(json)) {
    if (!json.success) {
      throw new Error(json.error ?? 'Request failed.');
    }

    if (json.data == null) {
      throw new Error('API response did not include data.');
    }

    return json.data;
  }

  return json as T;
};

export const fetchKeyNotes = async (
  baseUrl: string,
  keyName: string,
  signal?: AbortSignal,
) => {
  const url = buildUrl(baseUrl, `/api/music-theory/keys/${encodeURIComponent(keyName)}/notes`);
  return parseJson<KeyNotes>(await fetch(url, { signal }));
};

export const fetchScaleDegrees = async (baseUrl: string, signal?: AbortSignal) => {
  const url = buildUrl(baseUrl, '/api/music-theory/scale-degrees');
  return parseJson<ScaleDegree[]>(await fetch(url, { signal }));
};

export interface FetchChordsForKeyParams {
  baseUrl: string;
  keyName: string;
  filters: KeyChordFilters;
  signal?: AbortSignal;
}

export const fetchChordsForKey = async ({
  baseUrl,
  keyName,
  filters,
  signal,
}: FetchChordsForKeyParams) => {
  const searchParams = new URLSearchParams({
    onlyNaturallyOccurring: String(filters.onlyNaturallyOccurring),
    includeBorrowedChords: String(filters.includeBorrowedChords),
    includeSecondaryDominants: String(filters.includeSecondaryDominants),
    includeSecondaryTwoFive: String(filters.includeSecondaryTwoFive),
    minCommonality: filters.minCommonality.toString(),
    limit: filters.limit.toString(),
  });

  const url = buildUrl(
    baseUrl,
    `/api/contextual-chords/keys/${encodeURIComponent(keyName)}?${searchParams.toString()}`,
  );

  return parseJson<ChordInContext[]>(await fetch(url, { signal }));
};

export const fetchChordProgressionsForKey = async (
  baseUrl: string,
  keyName: string,
  signal?: AbortSignal,
) => {
  const url = buildUrl(baseUrl, `/api/chordprogressions/key/${encodeURIComponent(keyName)}`);
  return parseJson<ChordProgressionDefinition[]>(await fetch(url, { signal }));
};
