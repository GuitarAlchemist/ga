// src/components/PrimeRadiant/IxqlControlParser.ts
// IXQL parser for Prime Radiant visualization control and declarative UI
// Active grammar: SELECT | RESET | CREATE PANEL | BIND HEALTH

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

export interface BindHealthCommand {
  type: 'bind-health';
  targetKind: 'panel' | 'node';
  targetId: string;       // panel id or node selector field
  targetSelector: IxqlPredicate[]; // for node targeting (WHERE predicates)
  source: string;
  conditions: { predicate: IxqlPredicate; status: string }[];
  fallback: string;
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
  | BindHealthCommand
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

    // Comma
    if (input[i] === ',') { tokens.push(','); i++; continue; }

    // Word or number
    let word = '';
    while (i < input.length && !/[\s,>=<!~'"']/.test(input[i])) {
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
          return { ok: true, command: parseCreatePanel(ctx) };
        }
        throw new Error(`Expected 'PANEL' after CREATE, got '${peekRaw(ctx) ?? 'end of input'}'`);
      }

      case 'BIND': {
        next(ctx);
        return { ok: true, command: parseBindHealth(ctx) };
      }

      default:
        return { ok: false, error: `Unknown command '${peekRaw(ctx) ?? 'end of input'}'. Expected SELECT, RESET, CREATE, or BIND` };
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
      try {
        return new RegExp(String(expected), 'i').test(String(actual));
      } catch {
        return false;
      }
    }
    default: return false;
  }
}
