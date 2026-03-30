// src/components/PrimeRadiant/IxqlControlParser.ts
// IXQL parser for Prime Radiant visualization control and declarative UI
// Active grammar: SELECT | RESET | CREATE PANEL | BIND HEALTH | ON...THEN

export interface IxqlPredicate {
  field: string;       // dotted path: "health.staleness", "type", "name"
  operator: '>' | '<' | '=' | '>=' | '<=' | '!=' | '~';
  value: string | number;
}

export interface IxqlAssignment {
  property: string;    // glow, pulse, size, color, visible, opacity, speed
  value: string | number | boolean;
}

// ── Command variant interfaces ──

export interface SelectCommand {
  type: 'select';
  target: 'nodes' | 'edges';
  predicates: IxqlPredicate[];
  assignments: IxqlAssignment[];
}

export interface ResetCommand {
  type: 'reset';
}

export interface CreatePanelCommand {
  type: 'create-panel';
  id: string;
  source: string;
  wherePredicates: IxqlPredicate[];
  layout: 'list-detail' | 'dashboard' | 'status' | 'custom';
  icon: string;
  showFields: string[];
  filter: { field: string; mode: 'chips' | 'dropdown' | 'search' } | null;
}

// ── Phase 6: Grid panel — CREATE PANEL "id" KIND grid ──

export type ProjectionField = {
  name: string;          // output alias (or raw field name)
  expression: string;    // raw field path or function call like DAYS_SINCE(updated_at)
};

export type GridPanelKind = 'grid';

// ── PIPE transform types ──

export type AggregateFunction = 'COUNT' | 'SUM' | 'AVG' | 'MIN' | 'MAX';

export interface AggregateSpec {
  fn: AggregateFunction;
  field: string | null;  // null for COUNT (counts rows)
  alias: string;         // output field name: count, sum_effort, etc.
}

export type PipeStep =
  | { type: 'filter'; predicates: IxqlPredicate[] }
  | { type: 'sort'; field: string; direction: 'ASC' | 'DESC' }
  | { type: 'limit'; count: number }
  | { type: 'skip'; count: number }
  | { type: 'distinct'; field: string | null }
  | { type: 'flatten'; field: string }
  | { type: 'group'; byField: string; aggregates: AggregateSpec[] };

export interface CreateGridPanelCommand {
  type: 'create-grid-panel';
  id: string;
  kind: GridPanelKind;
  template: string | null;
  source: string;
  wherePredicates: IxqlPredicate[];
  project: ProjectionField[];
  pipe: PipeStep[];                  // PIPE FILTER/SORT/LIMIT/SKIP/DISTINCT/FLATTEN/GROUP BY
  refresh: number | null;
  live: boolean;
  layout: { breakpoint: string; cols: number }[];
  governedBy: number[];
  publish: { signal: string; as: string } | null;
  subscribe: string[];
}

export interface BindHealthCommand {
  type: 'bind-health';
  targetKind: 'panel' | 'node';
  targetId: string;       // panel id or node selector field
  targetSelector: IxqlPredicate[]; // for node targeting (WHERE predicates)
  source: string;
  conditions: { predicate: IxqlPredicate; status: string }[];
  fallback: string;
}

// IXQL Phase 5: Reactive trigger — executes action when source data changes
export interface OnChangedCommand {
  type: 'on-changed';
  source: string;              // "health.llm", "/api/...", "panel://backlog"
  wherePredicates: IxqlPredicate[];
  action: IxqlCommand;         // the THEN action (recursive — can be any command)
}

// ── Epistemic Constitution (Articles E-0 to E-9) ──

export type EpistemicTarget = 'beliefs' | 'strategies' | 'tensor' | 'learners' | 'journal' | 'incompetence';
export type TensorConfig = 'T_T' | 'T_F' | 'T_U' | 'T_C' | 'F_T' | 'F_F' | 'F_U' | 'F_C' | 'U_T' | 'U_F' | 'U_U' | 'U_C' | 'C_T' | 'C_F' | 'C_U' | 'C_C';

export interface ShowEpistemicCommand {
  type: 'show-epistemic';
  target: EpistemicTarget;
  predicates: IxqlPredicate[];
  orderBy: string | null;
  limit: number | null;
  visualize: boolean;         // Apply visual overrides to matching graph nodes
}

export interface MethylateCommand {
  type: 'methylate';
  strategyId: string;
  reason: string | null;
}

export interface DemethylateCommand {
  type: 'demethylate';
  strategyId: string;
}

export interface AmnesiaCommand {
  type: 'amnesia';
  beliefId: string;
  scheduleDays: number;       // Days until deletion
}

export interface BroadcastCommand {
  type: 'broadcast';
  target: 'beliefs' | 'tensor';
  predicates: IxqlPredicate[];
}

// GRAMMAR-RESERVED — not yet dispatched; awaiting 3-5 recurrence proof
export interface DropCommand {
  type: 'drop';
  targetKind: 'panel';
  id: string;
}

// GRAMMAR-RESERVED — not yet dispatched; awaiting 3-5 recurrence proof
export interface CreateNodeCommand {
  type: 'create-node';
  id: string;
  nodeType: string;
  parent: string | null;
}

// GRAMMAR-RESERVED — not yet dispatched; awaiting 3-5 recurrence proof
export interface LinkCommand {
  type: 'link';
  from: string;
  to: string;
  edgeType: string | null;
}

// GRAMMAR-RESERVED — not yet dispatched; awaiting 3-5 recurrence proof
export interface GroupCommand {
  type: 'group';
  predicates: IxqlPredicate[];
  byField: string;
}

// GRAMMAR-RESERVED — not yet dispatched; awaiting 3-5 recurrence proof
export interface SaveCommand {
  type: 'save';
  targetKind: 'panel' | 'graph';
  id: string | null;
}

// ── Discriminated union ──

export type IxqlCommand =
  | SelectCommand
  | ResetCommand
  | CreatePanelCommand
  | CreateGridPanelCommand
  | BindHealthCommand
  | OnChangedCommand
  | ShowEpistemicCommand
  | MethylateCommand
  | DemethylateCommand
  | AmnesiaCommand
  | BroadcastCommand
  | DropCommand
  | CreateNodeCommand
  | LinkCommand
  | GroupCommand
  | SaveCommand;

// ── Parse result as discriminated union ──

export type IxqlParseResult =
  | { ok: true; command: IxqlCommand }
  | { ok: false; error: string };

const VISUAL_PROPS = new Set(['glow', 'pulse', 'size', 'color', 'visible', 'opacity', 'speed']);
const OPERATORS = ['>=', '<=', '!=', '>', '<', '=', '~'] as const;

function tokenize(input: string): string[] {
  const tokens: string[] = [];
  let i = 0;
  while (i < input.length) {
    // Skip whitespace
    if (/\s/.test(input[i])) { i++; continue; }

    // Quoted string
    if (input[i] === "'" || input[i] === '"') {
      const quote = input[i];
      let str = '';
      i++; // skip opening quote
      while (i < input.length && input[i] !== quote) {
        str += input[i];
        i++;
      }
      i++; // skip closing quote
      tokens.push(str);
      continue;
    }

    // Operators (multi-char first)
    if (i + 1 < input.length) {
      const two = input.substring(i, i + 2);
      if (['>=', '<=', '!='].includes(two)) {
        tokens.push(two);
        i += 2;
        continue;
      }
    }
    if ('>=<~'.includes(input[i])) {
      tokens.push(input[i]);
      i++;
      continue;
    }

    // Structural characters
    if (input[i] === ',') { tokens.push(','); i++; continue; }
    if (input[i] === '{') { tokens.push('{'); i++; continue; }
    if (input[i] === '}') { tokens.push('}'); i++; continue; }
    if (input[i] === ':') { tokens.push(':'); i++; continue; }

    // Word or number (includes parenthesized args like DAYS_SINCE(updated_at))
    let word = '';
    while (i < input.length && !/[\s,>=<!~'"{}:]/.test(input[i])) {
      word += input[i];
      i++;
    }
    if (word) tokens.push(word);
  }
  return tokens;
}

// ── Shared parsing helpers ──

type ParserContext = {
  tokens: string[];
  pos: number;
};

function peek(ctx: ParserContext): string | undefined {
  return ctx.tokens[ctx.pos]?.toUpperCase();
}

function peekRaw(ctx: ParserContext): string | undefined {
  return ctx.tokens[ctx.pos];
}

function next(ctx: ParserContext): string | undefined {
  return ctx.tokens[ctx.pos++];
}

function nextRaw(ctx: ParserContext): string {
  const t = ctx.tokens[ctx.pos++];
  if (t === undefined) throw new Error('Unexpected end of input');
  return t;
}

function expect(ctx: ParserContext, val: string): string {
  const t = next(ctx);
  if (t?.toUpperCase() !== val) throw new Error(`Expected '${val}', got '${t ?? 'end of input'}'`);
  return t;
}

function parsePredicates(ctx: ParserContext): IxqlPredicate[] {
  const predicates: IxqlPredicate[] = [];
  do {
    const field = nextRaw(ctx);

    const opToken = nextRaw(ctx);
    if (!OPERATORS.includes(opToken as typeof OPERATORS[number])) {
      throw new Error(`Expected operator (>, <, =, >=, <=, !=, ~), got '${opToken}'`);
    }

    const valToken = nextRaw(ctx);
    const numVal = Number(valToken);
    const value = isNaN(numVal) ? valToken : numVal;

    predicates.push({ field, operator: opToken as IxqlPredicate['operator'], value });
  } while (peek(ctx) === 'AND' && next(ctx));
  return predicates;
}

function parseValue(token: string): string | number | boolean {
  if (token === 'true') return true;
  if (token === 'false') return false;
  const numVal = Number(token);
  if (!isNaN(numVal)) return numVal;
  return token;
}

// ── SELECT command parser ──

function parseSelect(ctx: ParserContext): SelectCommand {
  const targetToken = next(ctx)?.toLowerCase();
  if (targetToken !== 'nodes' && targetToken !== 'edges') {
    throw new Error(`Expected 'nodes' or 'edges', got '${targetToken}'`);
  }
  const target = targetToken as 'nodes' | 'edges';

  // WHERE (optional)
  const predicates: IxqlPredicate[] = [];
  if (peek(ctx) === 'WHERE') {
    next(ctx);
    predicates.push(...parsePredicates(ctx));
  }

  // SET (optional)
  const assignments: IxqlAssignment[] = [];
  if (peek(ctx) === 'SET') {
    next(ctx);
    do {
      if (peek(ctx) === ',') next(ctx);

      const prop = next(ctx)?.toLowerCase();
      if (!prop) throw new Error('Expected property name after SET');
      if (!VISUAL_PROPS.has(prop)) {
        throw new Error(`Unknown visual property '${prop}'. Valid: ${[...VISUAL_PROPS].join(', ')}`);
      }

      expect(ctx, '=');
      const valToken = nextRaw(ctx);
      assignments.push({ property: prop, value: parseValue(valToken) });
    } while (peek(ctx) === ',' || (ctx.pos < ctx.tokens.length && peek(ctx) !== undefined));
  }

  return { type: 'select', target, predicates, assignments };
}

// ── CREATE PANEL parser ──

function parseCreatePanel(ctx: ParserContext): CreatePanelCommand {
  const id = nextRaw(ctx);

  // FROM clause (required)
  expect(ctx, 'FROM');
  const source = nextRaw(ctx);

  // WHERE clause (optional, after FROM)
  const wherePredicates: IxqlPredicate[] = [];
  if (peek(ctx) === 'WHERE') {
    next(ctx);
    wherePredicates.push(...parsePredicates(ctx));
  }

  // LAYOUT clause (required)
  expect(ctx, 'LAYOUT');
  const layoutToken = nextRaw(ctx).toLowerCase();
  const validLayouts = ['list-detail', 'dashboard', 'status', 'custom'];
  if (!validLayouts.includes(layoutToken)) {
    throw new Error(`Expected layout type (${validLayouts.join(', ')}), got '${layoutToken}'`);
  }
  const layout = layoutToken as CreatePanelCommand['layout'];

  // ICON clause (optional)
  let icon = '';
  if (peek(ctx) === 'ICON') {
    next(ctx);
    icon = nextRaw(ctx);
  }

  // SHOW clause (optional)
  const showFields: string[] = [];
  if (peek(ctx) === 'SHOW') {
    next(ctx);
    showFields.push(nextRaw(ctx));
    while (peek(ctx) === ',') {
      next(ctx); // consume comma
      showFields.push(nextRaw(ctx));
    }
  }

  // FILTER clause (optional)
  let filter: CreatePanelCommand['filter'] = null;
  if (peek(ctx) === 'FILTER') {
    next(ctx);
    const field = nextRaw(ctx);
    expect(ctx, 'AS');
    const modeToken = nextRaw(ctx).toLowerCase();
    const validModes = ['chips', 'dropdown', 'search'];
    if (!validModes.includes(modeToken)) {
      throw new Error(`Expected filter mode (${validModes.join(', ')}), got '${modeToken}'`);
    }
    filter = { field, mode: modeToken as 'chips' | 'dropdown' | 'search' };
  }

  return { type: 'create-panel', id, source, wherePredicates, layout, icon, showFields, filter };
}

// ── CREATE PANEL ... KIND grid parser ──

function parseDuration(token: string): number {
  const lower = token.toLowerCase();
  // Find where digits end and unit begins
  let numEnd = 0;
  while (numEnd < lower.length && lower[numEnd] >= '0' && lower[numEnd] <= '9') numEnd++;
  if (numEnd === 0) throw new Error(`Invalid duration '${token}'. Expected e.g. 30s, 5m, 1000ms`);
  const num = parseInt(lower.substring(0, numEnd), 10);
  const unit = lower.substring(numEnd) || 'ms';
  switch (unit) {
    case 'ms': return num;
    case 's': return num * 1000;
    case 'm': return num * 60_000;
    case 'h': return num * 3_600_000;
    default: throw new Error(`Invalid duration unit '${unit}'. Expected ms, s, m, or h`);
  }
}

function parseLayoutBreakpoints(ctx: ParserContext): { breakpoint: string; cols: number }[] {
  const layouts: { breakpoint: string; cols: number }[] = [];
  // Tokenizer splits md:6 into md, :, 6 — parse as three tokens
  const BP_NAMES = new Set(['XS', 'SM', 'MD', 'LG', 'XL']);
  while (ctx.pos < ctx.tokens.length) {
    const raw = peek(ctx);
    if (!raw || !BP_NAMES.has(raw)) break;
    const bp = nextRaw(ctx).toLowerCase();
    expect(ctx, ':');
    const cols = parseInt(nextRaw(ctx), 10);
    if (isNaN(cols)) throw new Error(`Expected column number after ${bp}:`);
    layouts.push({ breakpoint: bp, cols });
  }
  return layouts;
}

function parseProjectClause(ctx: ParserContext): ProjectionField[] {
  const fields: ProjectionField[] = [];
  // Expect opening {
  expect(ctx, '{');
  while (ctx.pos < ctx.tokens.length) {
    if (peekRaw(ctx) === '}') { next(ctx); break; }
    if (peek(ctx) === ',') { next(ctx); continue; }

    const firstToken = nextRaw(ctx);

    // Check for alias: "ageDays: DAYS_SINCE(updated_at)" pattern
    if (peekRaw(ctx) === ':') {
      next(ctx); // consume ':'
      // Collect everything until comma or closing brace
      let expr = '';
      while (ctx.pos < ctx.tokens.length && peekRaw(ctx) !== ',' && peekRaw(ctx) !== '}') {
        expr += (expr ? ' ' : '') + nextRaw(ctx);
      }
      fields.push({ name: firstToken, expression: expr });
    } else {
      // No alias — field name is both name and expression
      fields.push({ name: firstToken, expression: firstToken });
    }
  }
  return fields;
}

// ── PIPE step parser ──

const PIPE_STEP_KEYWORDS = new Set(['FILTER', 'SORT', 'LIMIT', 'SKIP', 'DISTINCT', 'FLATTEN', 'GROUP']);
const AGGREGATE_FUNCTIONS = new Set(['COUNT', 'SUM', 'AVG', 'MIN', 'MAX']);

// Clause keywords that end a PIPE step (so we know when to stop consuming tokens)
const CLAUSE_KEYWORDS = new Set(['PIPE', 'PROJECT', 'REFRESH', 'LIVE', 'LAYOUT', 'GOVERNED', 'PUBLISH', 'SUBSCRIBE', 'TEMPLATE', 'SOURCE', 'FROM']);

function parseAggregate(token: string): AggregateSpec {
  const upper = token.toUpperCase();
  // COUNT has no field argument
  if (upper === 'COUNT') {
    return { fn: 'COUNT', field: null, alias: 'count' };
  }
  // SUM(field), AVG(field), MIN(field), MAX(field)
  const parenOpen = token.indexOf('(');
  const parenClose = token.lastIndexOf(')');
  if (parenOpen > 0 && parenClose === token.length - 1) {
    const fnName = token.substring(0, parenOpen).toUpperCase();
    if (!AGGREGATE_FUNCTIONS.has(fnName)) {
      throw new Error(`Unknown aggregate function '${fnName}'. Supported: COUNT, SUM, AVG, MIN, MAX`);
    }
    const field = token.substring(parenOpen + 1, parenClose);
    return { fn: fnName as AggregateFunction, field, alias: fnName.toLowerCase() + '_' + field };
  }
  throw new Error(`Invalid aggregate '${token}'. Expected COUNT, SUM(field), AVG(field), MIN(field), or MAX(field)`);
}

function parsePipeStep(ctx: ParserContext): PipeStep {
  const stepKw = peek(ctx);
  if (!stepKw || !PIPE_STEP_KEYWORDS.has(stepKw)) {
    throw new Error(`Expected PIPE step (${[...PIPE_STEP_KEYWORDS].join(', ')}), got '${peekRaw(ctx) ?? 'end of input'}'`);
  }
  next(ctx);

  switch (stepKw) {
    case 'FILTER':
      return { type: 'filter', predicates: parsePredicates(ctx) };

    case 'SORT': {
      const field = nextRaw(ctx);
      let direction: 'ASC' | 'DESC' = 'ASC';
      const dirToken = peek(ctx);
      if (dirToken === 'ASC' || dirToken === 'DESC') {
        direction = dirToken;
        next(ctx);
      }
      return { type: 'sort', field, direction };
    }

    case 'LIMIT': {
      const count = parseInt(nextRaw(ctx), 10);
      if (isNaN(count) || count < 0) throw new Error('PIPE LIMIT requires a non-negative integer');
      return { type: 'limit', count };
    }

    case 'SKIP': {
      const count = parseInt(nextRaw(ctx), 10);
      if (isNaN(count) || count < 0) throw new Error('PIPE SKIP requires a non-negative integer');
      return { type: 'skip', count };
    }

    case 'DISTINCT': {
      // Optional field — if next token is not a clause keyword, it's a field
      let field: string | null = null;
      const nextKw = peek(ctx);
      if (nextKw && !CLAUSE_KEYWORDS.has(nextKw) && !PIPE_STEP_KEYWORDS.has(nextKw)) {
        field = nextRaw(ctx);
      }
      return { type: 'distinct', field };
    }

    case 'FLATTEN': {
      const field = nextRaw(ctx);
      return { type: 'flatten', field };
    }

    case 'GROUP': {
      if (peek(ctx) === 'BY') next(ctx);
      const byField = nextRaw(ctx);
      const aggregates: AggregateSpec[] = [];
      // Parse aggregates until we hit a clause keyword or end
      while (ctx.pos < ctx.tokens.length) {
        const nxt = peek(ctx);
        if (!nxt || CLAUSE_KEYWORDS.has(nxt)) break;
        aggregates.push(parseAggregate(nextRaw(ctx)));
      }
      if (aggregates.length === 0) {
        // Default to COUNT if no aggregates specified
        aggregates.push({ fn: 'COUNT', field: null, alias: 'count' });
      }
      return { type: 'group', byField, aggregates };
    }

    default:
      throw new Error(`Unknown PIPE step '${stepKw}'`);
  }
}

function parseCreateGridPanel(ctx: ParserContext, id: string): CreateGridPanelCommand {
  const kindToken = nextRaw(ctx).toLowerCase();
  if (kindToken !== 'grid') {
    throw new Error(`Unknown panel KIND '${kindToken}'. Supported: grid`);
  }

  let template: string | null = null;
  let source = '';
  const wherePredicates: IxqlPredicate[] = [];
  let project: ProjectionField[] = [];
  const pipe: PipeStep[] = [];
  let refresh: number | null = null;
  let live = false;
  const layout: { breakpoint: string; cols: number }[] = [];
  const governedBy: number[] = [];
  let publish: { signal: string; as: string } | null = null;
  const subscribe: string[] = [];

  // Parse clauses in any order
  while (ctx.pos < ctx.tokens.length) {
    const kw = peek(ctx);
    switch (kw) {
      case 'TEMPLATE':
        next(ctx);
        template = nextRaw(ctx);
        break;

      case 'SOURCE':
      case 'FROM':
        next(ctx);
        source = nextRaw(ctx);
        // Optional WHERE after SOURCE
        if (peek(ctx) === 'WHERE') {
          next(ctx);
          wherePredicates.push(...parsePredicates(ctx));
        }
        break;

      case 'PROJECT':
        next(ctx);
        project = parseProjectClause(ctx);
        break;

      case 'REFRESH':
        next(ctx);
        refresh = parseDuration(nextRaw(ctx));
        break;

      case 'LIVE':
        next(ctx);
        live = nextRaw(ctx).toLowerCase() === 'true';
        break;

      case 'PIPE':
        next(ctx);
        pipe.push(parsePipeStep(ctx));
        break;

      case 'LAYOUT':
        next(ctx);
        layout.push(...parseLayoutBreakpoints(ctx));
        break;

      case 'GOVERNED': {
        next(ctx);
        if (peek(ctx) === 'BY') next(ctx);
        // Parse article=7 or article=7,3
        const articleClause = nextRaw(ctx);
        const eqIdx = articleClause.indexOf('=');
        if (eqIdx !== -1 && articleClause.substring(0, eqIdx).toLowerCase() === 'article') {
          const nums = articleClause.substring(eqIdx + 1).split(',');
          for (const n of nums) {
            const parsed = parseInt(n.trim(), 10);
            if (!isNaN(parsed)) governedBy.push(parsed);
          }
        } else {
          // Just a number
          const num = parseInt(articleClause, 10);
          if (!isNaN(num)) governedBy.push(num);
        }
        break;
      }

      case 'PUBLISH':
        next(ctx);
        {
          const signal = nextRaw(ctx);
          let as = signal;
          if (peek(ctx) === 'AS') {
            next(ctx);
            as = nextRaw(ctx);
          }
          publish = { signal, as };
        }
        break;

      case 'SUBSCRIBE':
        next(ctx);
        subscribe.push(nextRaw(ctx));
        while (peek(ctx) === ',') {
          next(ctx);
          subscribe.push(nextRaw(ctx));
        }
        break;

      default:
        // Unknown clause — stop parsing
        throw new Error(`Unexpected clause '${peekRaw(ctx)}' in CREATE PANEL KIND grid`);
    }
  }

  if (!source) throw new Error('CREATE PANEL KIND grid requires a SOURCE clause');

  return {
    type: 'create-grid-panel',
    id,
    kind: 'grid',
    template,
    source,
    wherePredicates,
    project,
    pipe,
    refresh,
    live,
    layout,
    governedBy,
    publish,
    subscribe,
  };
}

// ── BIND HEALTH parser ──

function parseBindHealth(ctx: ParserContext): BindHealthCommand {
  let targetKind: 'panel' | 'node';
  let targetId = '';
  const targetSelector: IxqlPredicate[] = [];

  const kindToken = peek(ctx);
  if (kindToken === 'PANEL') {
    next(ctx);
    targetKind = 'panel';
    targetId = nextRaw(ctx);
  } else if (kindToken === 'NODE') {
    next(ctx);
    targetKind = 'node';
    if (peek(ctx) === 'WHERE') {
      next(ctx);
      targetSelector.push(...parsePredicates(ctx));
    }
  } else {
    throw new Error(`Expected 'PANEL' or 'NODE' after BIND, got '${peekRaw(ctx) ?? 'end of input'}'`);
  }

  expect(ctx, 'HEALTH');
  expect(ctx, 'FROM');
  const source = nextRaw(ctx);

  // WHEN conditions
  const conditions: { predicate: IxqlPredicate; status: string }[] = [];
  while (peek(ctx) === 'WHEN') {
    next(ctx);
    const preds = parsePredicates(ctx);
    if (preds.length !== 1) {
      throw new Error('BIND HEALTH WHEN clause expects exactly one predicate');
    }
    expect(ctx, 'SET');
    const status = nextRaw(ctx);
    conditions.push({ predicate: preds[0], status });
  }

  // ELSE SET fallback
  let fallback = 'ok';
  if (peek(ctx) === 'ELSE') {
    next(ctx);
    expect(ctx, 'SET');
    fallback = nextRaw(ctx);
  }

  return { type: 'bind-health', targetKind, targetId, targetSelector, source, conditions, fallback };
}

// ── ON...THEN parser ──

function parseOnChanged(ctx: ParserContext): OnChangedCommand {
  const source = nextRaw(ctx);
  expect(ctx, 'CHANGED');

  // Optional WHERE predicates
  const wherePredicates: IxqlPredicate[] = [];
  if (peek(ctx) === 'WHERE') {
    next(ctx);
    wherePredicates.push(...parsePredicates(ctx));
  }

  expect(ctx, 'THEN');

  // Collect remaining tokens and re-parse as a sub-command
  const remaining = ctx.tokens.slice(ctx.pos).join(' ');
  const subResult = parseIxqlCommand(remaining);
  if (!subResult.ok) {
    throw new Error(`Invalid THEN clause: ${subResult.error}`);
  }
  // Consume all remaining tokens
  ctx.pos = ctx.tokens.length;

  return { type: 'on-changed', source, wherePredicates, action: subResult.command };
}

// ── Main entry point ──

// ── Epistemic command parsers (Articles E-0 to E-9) ──

const EPISTEMIC_TARGETS = new Set<EpistemicTarget>(['beliefs', 'strategies', 'tensor', 'learners', 'journal', 'incompetence']);

function parseShowEpistemic(ctx: ParserContext): ShowEpistemicCommand {
  const targetRaw = next(ctx);
  const target = targetRaw.toLowerCase() as EpistemicTarget;
  if (!EPISTEMIC_TARGETS.has(target)) {
    throw new Error(`Expected BELIEFS, STRATEGIES, TENSOR, LEARNERS, JOURNAL, or INCOMPETENCE after SHOW, got '${targetRaw}'`);
  }

  let predicates: IxqlPredicate[] = [];
  let orderBy: string | null = null;
  let limit: number | null = null;
  let visualize = false;

  while (ctx.pos < ctx.tokens.length) {
    const kw = peek(ctx);
    if (kw === 'WHERE') {
      next(ctx);
      predicates = parsePredicates(ctx);
    } else if (kw === 'ORDER') {
      next(ctx);
      if (peek(ctx) === 'BY') next(ctx);
      orderBy = nextRaw(ctx);
    } else if (kw === 'LIMIT') {
      next(ctx);
      limit = parseInt(nextRaw(ctx), 10);
      if (isNaN(limit)) throw new Error('LIMIT requires a number');
    } else if (kw === 'VISUALIZE') {
      next(ctx);
      visualize = true;
    } else {
      break;
    }
  }

  return { type: 'show-epistemic', target, predicates, orderBy, limit, visualize };
}

function parseMethylate(ctx: ParserContext): MethylateCommand {
  const strategyId = nextRaw(ctx);
  let reason: string | null = null;
  if (ctx.pos < ctx.tokens.length && peek(ctx) === 'REASON') {
    next(ctx);
    reason = ctx.tokens.slice(ctx.pos).join(' ');
    ctx.pos = ctx.tokens.length;
  }
  return { type: 'methylate', strategyId, reason };
}

function parseDemethylate(ctx: ParserContext): DemethylateCommand {
  const strategyId = nextRaw(ctx);
  return { type: 'demethylate', strategyId };
}

function parseAmnesia(ctx: ParserContext): AmnesiaCommand {
  const beliefId = nextRaw(ctx);
  let scheduleDays = 7; // default 7 days
  if (ctx.pos < ctx.tokens.length && peek(ctx) === 'IN') {
    next(ctx);
    scheduleDays = parseInt(nextRaw(ctx), 10);
    if (isNaN(scheduleDays)) throw new Error('AMNESIA IN requires a number of days');
    if (peek(ctx) === 'DAYS') next(ctx); // consume optional DAYS keyword
  }
  return { type: 'amnesia', beliefId, scheduleDays };
}

function parseBroadcast(ctx: ParserContext): BroadcastCommand {
  const targetRaw = next(ctx);
  const target = targetRaw.toLowerCase() as 'beliefs' | 'tensor';
  if (target !== 'beliefs' && target !== 'tensor') {
    throw new Error(`Expected BELIEFS or TENSOR after BROADCAST, got '${targetRaw}'`);
  }
  let predicates: IxqlPredicate[] = [];
  if (ctx.pos < ctx.tokens.length && peek(ctx) === 'WHERE') {
    next(ctx);
    predicates = parsePredicates(ctx);
  }
  return { type: 'broadcast', target, predicates };
}

// ── Main entry point ──

export function parseIxqlCommand(input: string): IxqlParseResult {
  const trimmed = input.trim();
  if (!trimmed) return { ok: false, error: 'Empty command' };

  // RESET command (fast path)
  if (trimmed.toUpperCase() === 'RESET') {
    return { ok: true, command: { type: 'reset' } };
  }

  const tokens = tokenize(trimmed);
  const ctx: ParserContext = { tokens, pos: 0 };

  try {
    const keyword = peek(ctx);

    switch (keyword) {
      case 'SELECT': {
        next(ctx);
        return { ok: true, command: parseSelect(ctx) };
      }

      case 'CREATE': {
        next(ctx);
        const subKeyword = peek(ctx);
        if (subKeyword === 'PANEL') {
          next(ctx);
          const panelId = nextRaw(ctx);
          // Branch: if next token is KIND → new grid panel grammar
          if (peek(ctx) === 'KIND') {
            next(ctx);
            return { ok: true, command: parseCreateGridPanel(ctx, panelId) };
          }
          // Legacy path: FROM/LAYOUT grammar — put panelId back by rewinding
          ctx.pos--;
          return { ok: true, command: parseCreatePanel(ctx) };
        }
        throw new Error(`Expected 'PANEL' after CREATE, got '${peekRaw(ctx) ?? 'end of input'}'`);
      }

      case 'BIND': {
        next(ctx);
        return { ok: true, command: parseBindHealth(ctx) };
      }

      case 'ON': {
        next(ctx);
        return { ok: true, command: parseOnChanged(ctx) };
      }

      case 'SHOW': {
        next(ctx);
        return { ok: true, command: parseShowEpistemic(ctx) };
      }

      case 'METHYLATE': {
        next(ctx);
        return { ok: true, command: parseMethylate(ctx) };
      }

      case 'DEMETHYLATE': {
        next(ctx);
        return { ok: true, command: parseDemethylate(ctx) };
      }

      case 'AMNESIA': {
        next(ctx);
        return { ok: true, command: parseAmnesia(ctx) };
      }

      case 'BROADCAST': {
        next(ctx);
        return { ok: true, command: parseBroadcast(ctx) };
      }

      default:
        return { ok: false, error: `Unknown command '${peekRaw(ctx) ?? 'end of input'}'. Expected SELECT, RESET, CREATE (PANEL/PANEL KIND grid), BIND, ON, SHOW, METHYLATE, DEMETHYLATE, AMNESIA, or BROADCAST` };
    }
  } catch (e) {
    return { ok: false, error: (e as Error).message };
  }
}

// ── Evaluate a predicate against a node or edge object ──
export function evaluatePredicate(
  predicate: IxqlPredicate,
  obj: Record<string, unknown>,
): boolean {
  // Resolve dotted path
  const parts = predicate.field.split('.');
  let current: unknown = obj;
  for (const part of parts) {
    if (current == null || typeof current !== 'object') return false;
    current = (current as Record<string, unknown>)[part];
  }

  const actual = current;
  const expected = predicate.value;

  switch (predicate.operator) {
    case '=':  return String(actual) === String(expected);
    case '!=': return String(actual) !== String(expected);
    case '>':  return Number(actual) > Number(expected);
    case '<':  return Number(actual) < Number(expected);
    case '>=': return Number(actual) >= Number(expected);
    case '<=': return Number(actual) <= Number(expected);
    case '~': {
      // Case-insensitive substring match (no regex — avoids ReDoS)
      return String(actual).toLowerCase().indexOf(String(expected).toLowerCase()) >= 0;
    }
    default: return false;
  }
}
