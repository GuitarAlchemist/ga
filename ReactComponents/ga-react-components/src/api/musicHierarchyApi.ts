import { MusicHierarchyItem, MusicHierarchyLevel, MusicHierarchyLevelInfo } from '../types/musicHierarchy';

// Default to relative `/graphql` so the Vite proxy (or production same-origin
// routing) handles host resolution. The legacy `https://localhost:7001` default
// pointed at a port GaApi has not used since the move to :5232; broke the
// Music Hierarchy Navigator on 2026-05-16.
const GRAPHQL_ENDPOINT = import.meta.env.VITE_GA_GRAPHQL_URL ?? '/graphql';

// HotChocolate emits enum values in SCREAMING_SNAKE_CASE by default
// (`SET_CLASS`), but the UI was written against the PascalCase C# names
// (`SetClass`). Translate at the API boundary so the rest of the page
// keeps working without a 600-line rename.
const UI_TO_WIRE_LEVEL: Record<MusicHierarchyLevel, string> = {
  SetClass:     'SET_CLASS',
  ForteNumber:  'FORTE_NUMBER',
  PrimeForm:    'PRIME_FORM',
  Chord:        'CHORD',
  ChordVoicing: 'CHORD_VOICING',
  Scale:        'SCALE',
};

const WIRE_TO_UI_LEVEL: Record<string, MusicHierarchyLevel> = Object.fromEntries(
  Object.entries(UI_TO_WIRE_LEVEL).map(([ui, wire]) => [wire, ui as MusicHierarchyLevel]),
);

function toUiLevel(wire: string): MusicHierarchyLevel {
  return WIRE_TO_UI_LEVEL[wire] ?? (wire as MusicHierarchyLevel);
}

interface GraphQLResponse<T> {
  data?: T;
  errors?: { message: string }[];
}

async function postGraphQL<T>(query: string, variables?: Record<string, unknown>): Promise<T> {
  const response = await fetch(GRAPHQL_ENDPOINT, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ query, variables }),
  });

  if (!response.ok) {
    throw new Error(`GraphQL request failed with status ${response.status}`);
  }

  const payload = (await response.json()) as GraphQLResponse<T>;
  if (payload.errors?.length) {
    throw new Error(payload.errors.map(err => err.message).join('\n'));
  }

  if (!payload.data) {
    throw new Error('GraphQL response did not include data');
  }

  return payload.data;
}

export async function fetchHierarchyLevels(): Promise<MusicHierarchyLevelInfo[]> {
  const query = /* GraphQL */ `
    query MusicHierarchyLevels {
      musicHierarchyLevels {
        level
        displayName
        description
        totalItems
        primaryMetric
        highlights
      }
    }
  `;

  const data = await postGraphQL<{ musicHierarchyLevels: Array<MusicHierarchyLevelInfo & { level: string }> }>(query);
  return data.musicHierarchyLevels.map((l) => ({ ...l, level: toUiLevel(l.level) }));
}

export interface HierarchyItemsVariables extends Record<string, unknown> {
  level: MusicHierarchyLevel;
  parentId?: string;
  take?: number;
  search?: string;
}

// Wire shape returned by GraphQL — `metadata` is a list of KeyValuePair
// objects, not a scalar map. We flatten it to Record<string, string> at the
// API boundary so the UI keeps the original lookup-by-key contract
// (`item.metadata['Cardinality']`).
interface MusicHierarchyItemWire {
  id: string;
  name: string;
  level: string;
  category: string;
  description?: string | null;
  tags: string[];
  metadata: Array<{ key: string; value: string }>;
}

// The page reads metadata under the PascalCase names it was originally
// designed against. The GraphQL schema now emits camelCase keys
// (HotChocolate default), and a few keys were renamed entirely (e.g.
// `icv` is the historical "IntervalVector"). Alias both forms so the
// existing column renderers keep working without a UI rewrite.
const METADATA_ALIASES: Record<string, string> = {
  cardinality:       'Cardinality',
  icv:               'IntervalVector',
  primeFormId:       'PrimeFormId',
  parentSetClassId:  'ParentSetClassId',
  setClassId:        'SetClassId',
  noteCount:         'NoteCount',
  extension:         'Extension',
  frets:             'Frets',
  strings:           'Strings',
  root:              'Root',
  isModal:           'IsModal',
};

function fromWire(item: MusicHierarchyItemWire): MusicHierarchyItem {
  const meta: Record<string, string> = {};
  for (const { key, value } of item.metadata ?? []) {
    meta[key] = value;
    const alias = METADATA_ALIASES[key];
    if (alias && !(alias in meta)) {
      meta[alias] = value;
    }
  }
  return { ...item, level: toUiLevel(item.level), metadata: meta };
}

export async function fetchHierarchyItems(variables: HierarchyItemsVariables): Promise<MusicHierarchyItem[]> {
  const query = /* GraphQL */ `
    query MusicHierarchyItems($level: MusicHierarchyLevel!, $parentId: String, $take: Int, $search: String) {
      musicHierarchyItems(level: $level, parentId: $parentId, take: $take, search: $search) {
        id
        name
        level
        category
        description
        tags
        metadata {
          key
          value
        }
      }
    }
  `;

  const wireVars = { ...variables, level: UI_TO_WIRE_LEVEL[variables.level] ?? variables.level };
  const data = await postGraphQL<{ musicHierarchyItems: MusicHierarchyItemWire[] }>(query, wireVars);
  return data.musicHierarchyItems.map(fromWire);
}

export const fetchAllHierarchyItems = fetchHierarchyItems;
