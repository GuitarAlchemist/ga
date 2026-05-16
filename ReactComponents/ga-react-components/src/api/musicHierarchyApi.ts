import { MusicHierarchyItem, MusicHierarchyLevel, MusicHierarchyLevelInfo } from '../types/musicHierarchy';

// Default to relative `/graphql` so the Vite proxy (or production same-origin
// routing) handles host resolution. The legacy `https://localhost:7001` default
// pointed at a port GaApi has not used since the move to :5232; broke the
// Music Hierarchy Navigator on 2026-05-16.
const GRAPHQL_ENDPOINT = import.meta.env.VITE_GA_GRAPHQL_URL ?? '/graphql';

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

  const data = await postGraphQL<{ musicHierarchyLevels: MusicHierarchyLevelInfo[] }>(query);
  return data.musicHierarchyLevels;
}

export interface HierarchyItemsVariables extends Record<string, unknown> {
  level: MusicHierarchyLevel;
  parentId?: string;
  take?: number;
  search?: string;
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
        metadata
      }
    }
  `;

  const data = await postGraphQL<{ musicHierarchyItems: MusicHierarchyItem[] }>(query, variables);
  return data.musicHierarchyItems;
}

export const fetchAllHierarchyItems = fetchHierarchyItems;
