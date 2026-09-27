// Jev (TypeSafe System One) as a shadow advisor on agent proposals.
//
// Shadow means the advice is shown and recorded next to the user's own
// decision, and nothing reads it to act: Accept and Reject never depend on
// it. Only a summary leaves the machine (step ids, tool names, approval
// tier/effect, the proposal's title), never step arguments. Shared by the
// editor (building the summary) and the dev server (payload, validation).

export const JEV_MODEL = 'jev-1.13.0';
export const JEV_API_URL = 'https://api.typesafe.ai/v1/systemone';
/** Same proxy as the learn lab: provider input price, applied to all tokens. */
export const JEV_PRICE_PER_MILLION_USD = 0.042;
export const JEV_MAX_PAYLOAD_BYTES = 4_000;
const MAX_LISTED = 20;

export interface ProposalSummary {
  title: string;
  author: string;
  based_on_current: boolean;
  added: { id: string; tool: string; tier?: string; effect?: string }[];
  removed: { id: string; tool: string }[];
  changed: { id: string; fields: string[] }[];
  steps_before: number;
  steps_after: number;
}

const QUESTIONS = {
  recommendation: {
    type: 'choice',
    instructions: 'Should the user accept this proposed change to their pipeline?',
    criteria: {
      accept: 'Small and coherent with its title; adds no tool with side effects.',
      review: 'Needs a closer human look: large, based on a stale revision, or adds tools with effects.',
      reject: 'Contradicts its title or removes most of the pipeline without saying so.',
    },
  },
  risk: {
    type: 'score',
    instructions: 'How hard would this change be to undo if it were wrong?',
    criteria: ['Low and reversible', 'Moderate', 'High or hard to reverse'],
  },
  matches_title: {
    type: 'noul',
    instructions: 'Do the listed changes do what the title says?',
  },
} as const;

/** The request body; lists are capped and free text is truncated. */
export function buildAdvicePayload(s: ProposalSummary): { model: string; state: unknown; questions: typeof QUESTIONS } {
  const cap = <T,>(xs: T[]) => xs.slice(0, MAX_LISTED);
  return {
    model: JEV_MODEL,
    state: {
      proposal_title: String(s.title).slice(0, 200),
      proposal_author: String(s.author).slice(0, 100),
      based_on_current_revision: s.based_on_current === true,
      steps_before: s.steps_before,
      steps_after: s.steps_after,
      added: cap(s.added).map((a) => ({ id: a.id, tool: a.tool, tier: a.tier ?? 'unknown', effect: a.effect ?? 'unknown' })),
      removed: cap(s.removed).map((r) => ({ id: r.id, tool: r.tool })),
      changed: cap(s.changed).map((c) => ({ id: c.id, fields: c.fields })),
      truncated: s.added.length > MAX_LISTED || s.removed.length > MAX_LISTED || s.changed.length > MAX_LISTED,
    },
    questions: QUESTIONS,
  };
}

export interface JevAdvice {
  recommendation: 'accept' | 'review' | 'reject';
  confidence: number;
  risk: number;
  matches_title: number;
  usage: { input_tokens: number; output_tokens: number };
}

const unit = (v: unknown): v is number => typeof v === 'number' && Number.isFinite(v) && v >= 0 && v <= 1;
const count = (v: unknown): v is number => typeof v === 'number' && Number.isInteger(v) && v >= 0;

/** Reads the fields the editor shows; throws on anything off contract. */
export function readAdvice(raw: unknown): JevAdvice {
  const r = (raw ?? {}) as { model?: unknown; answers?: Record<string, Record<string, unknown>>; usage?: Record<string, unknown> };
  if (r.model !== JEV_MODEL) throw new Error(`model must be the pinned ${JEV_MODEL}`);
  const a = r.answers ?? {};
  const keys = Object.keys(a).sort().join(',');
  if (keys !== Object.keys(QUESTIONS).sort().join(',')) throw new Error('answers must match the question ids');
  const choice = a.recommendation.choice;
  if (a.recommendation.type !== 'choice' || !(typeof choice === 'string' && choice in QUESTIONS.recommendation.criteria)) {
    throw new Error('recommendation is not a valid choice');
  }
  if (!unit(a.recommendation.confidence)) throw new Error('recommendation.confidence must be in [0, 1]');
  const score = a.risk.score;
  if (a.risk.type !== 'score' || typeof score !== 'number' || !(score >= 0 && score <= 2)) throw new Error('risk is not a valid score');
  if (a.matches_title.type !== 'noul' || !unit(a.matches_title.noul)) throw new Error('matches_title is not a valid noul');
  const u = r.usage ?? {};
  if (!count(u.input_tokens) || !count(u.output_tokens)) throw new Error('usage must hold token counts');
  return {
    recommendation: choice as JevAdvice['recommendation'],
    confidence: a.recommendation.confidence as number,
    risk: score,
    matches_title: a.matches_title.noul as number,
    usage: { input_tokens: u.input_tokens, output_tokens: u.output_tokens },
  };
}

/** Cost proxy of a call from its reported usage. */
export const usageCostUsd = (u: { input_tokens: number; output_tokens: number }) =>
  ((u.input_tokens + u.output_tokens) / 1_000_000) * JEV_PRICE_PER_MILLION_USD;
