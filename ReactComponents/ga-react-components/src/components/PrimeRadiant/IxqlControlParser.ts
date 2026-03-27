// src/components/PrimeRadiant/IxqlControlParser.ts
// Minimal IXql subset parser for Prime Radiant visualization control
// Grammar: SELECT nodes|edges WHERE <predicate> SET <assignments> | RESET

export interface IxqlPredicate {
  field: string;       // dotted path: "health.staleness", "type", "name"
  operator: '>' | '<' | '=' | '>=' | '<=' | '!=' | '~';
  value: string | number;
}

export interface IxqlAssignment {
  property: string;    // glow, pulse, size, color, visible, opacity, speed
  value: string | number | boolean;
}

export interface IxqlCommand {
  type: 'select' | 'reset';
  target?: 'nodes' | 'edges';
  predicates?: IxqlPredicate[];
  assignments?: IxqlAssignment[];
}

export interface IxqlParseResult {
  ok: boolean;
  command?: IxqlCommand;
  error?: string;
}

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

export function parseIxqlCommand(input: string): IxqlParseResult {
  const trimmed = input.trim();
  if (!trimmed) return { ok: false, error: 'Empty command' };

  // RESET command
  if (trimmed.toUpperCase() === 'RESET') {
    return { ok: true, command: { type: 'reset' } };
  }

  const tokens = tokenize(trimmed);
  let pos = 0;

  const peek = () => tokens[pos]?.toUpperCase();
  const next = () => tokens[pos++];
  const expect = (val: string) => {
    const t = next();
    if (t?.toUpperCase() !== val) throw new Error(`Expected '${val}', got '${t ?? 'end of input'}'`);
    return t;
  };

  try {
    // SELECT
    expect('SELECT');

    // Target: nodes | edges
    const targetToken = next()?.toLowerCase();
    if (targetToken !== 'nodes' && targetToken !== 'edges') {
      throw new Error(`Expected 'nodes' or 'edges', got '${targetToken}'`);
    }
    const target = targetToken as 'nodes' | 'edges';

    // WHERE (optional)
    const predicates: IxqlPredicate[] = [];
    if (peek() === 'WHERE') {
      next(); // consume WHERE
      // Parse predicates (field op value), joined by AND
      do {
        const field = next();
        if (!field) throw new Error('Expected field name after WHERE');

        const opToken = next();
        if (!opToken || !OPERATORS.includes(opToken as typeof OPERATORS[number])) {
          throw new Error(`Expected operator (>, <, =, >=, <=, !=, ~), got '${opToken}'`);
        }

        const valToken = next();
        if (valToken === undefined) throw new Error('Expected value after operator');

        const numVal = Number(valToken);
        const value = isNaN(numVal) ? valToken : numVal;

        predicates.push({ field, operator: opToken as IxqlPredicate['operator'], value });
      } while (peek() === 'AND' && next());
    }

    // SET (optional)
    const assignments: IxqlAssignment[] = [];
    if (peek() === 'SET') {
      next(); // consume SET
      do {
        if (peek() === ',') next(); // consume comma separator

        const prop = next()?.toLowerCase();
        if (!prop) throw new Error('Expected property name after SET');
        if (!VISUAL_PROPS.has(prop)) {
          throw new Error(`Unknown visual property '${prop}'. Valid: ${[...VISUAL_PROPS].join(', ')}`);
        }

        expect('=');

        const valToken = next();
        if (valToken === undefined) throw new Error(`Expected value for '${prop}'`);

        let value: string | number | boolean = valToken;
        if (valToken === 'true') value = true;
        else if (valToken === 'false') value = false;
        else {
          const numVal = Number(valToken);
          if (!isNaN(numVal)) value = numVal;
        }

        assignments.push({ property: prop, value });
      } while (peek() === ',' || (pos < tokens.length && peek() !== undefined));
    }

    return {
      ok: true,
      command: { type: 'select', target, predicates, assignments },
    };
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
