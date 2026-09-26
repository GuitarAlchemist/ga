// Pure model for the IX pipeline editor: editor graph <-> the
// `ix_pipeline_run` / `ix_pipeline_validate` spec ({ steps: [...] }).

/** One entry of `ix_node_catalog`. Only the fields the editor reads. */
export interface CatalogNode {
  name: string;
  description: string;
  required_inputs: string[];
  input_schema?: { properties?: Record<string, SchemaProperty> };
  approval?: { tier?: string; effect?: string };
}

/** The part of a JSON Schema property the editor checks. */
export interface SchemaProperty {
  type?: string | string[];
  description?: string;
  items?: { type?: string | string[] };
}

/** A step as edited: arguments stay raw JSON text until the spec is built. */
export interface EditorStep {
  id: string;
  tool: string;
  argsText: string;
}

/** Edge source -> target means "target depends_on source". */
export interface EditorEdge {
  source: string;
  target: string;
}

export interface PipelineSpec {
  steps: { id: string; tool: string; arguments: Record<string, unknown>; depends_on?: string[] }[];
}

export interface ValidationIssue {
  code: string;
  message: string;
  step?: string;
  index?: number;
}

export interface ValidationReport {
  valid: boolean;
  errors: ValidationIssue[];
  warnings: ValidationIssue[];
  execution_order: string[] | null;
}

/** Arguments template: every required input, set to null for the user to fill. */
export function defaultArgsText(node: CatalogNode): string {
  const args: Record<string, null> = {};
  for (const key of node.required_inputs) args[key] = null;
  return JSON.stringify(args, null, 2);
}

/** First free id of the form s1, s2, ... */
export function nextStepId(steps: EditorStep[]): string {
  const taken = new Set(steps.map((s) => s.id));
  let n = 1;
  while (taken.has(`s${n}`)) n++;
  return `s${n}`;
}

/**
 * Builds the spec. Steps whose arguments are not a JSON object are reported
 * in `argErrors` (keyed by step id) and sent with empty arguments, so the
 * rest of the graph still validates. A required input still at the template
 * value `null` is also an arg error: ix_pipeline_validate only checks that
 * the key is present, so it would pass validation and fail at run time.
 */
export function buildSpec(
  steps: EditorStep[],
  edges: EditorEdge[],
  requiredByTool: Record<string, string[]> = {},
  propertiesByTool: Record<string, Record<string, SchemaProperty> | undefined> = {},
): { spec: PipelineSpec; argErrors: Record<string, string> } {
  const argErrors: Record<string, string> = {};
  const specSteps = steps.map((step) => {
    let args: Record<string, unknown> = {};
    try {
      const parsed: unknown = step.argsText.trim() ? JSON.parse(step.argsText) : {};
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
        args = parsed as Record<string, unknown>;
        const unfilled = (requiredByTool[step.tool] ?? []).filter((k) => args[k] === null);
        const mistyped = typeErrors(args, propertiesByTool[step.tool] ?? {});
        if (unfilled.length > 0) argErrors[step.id] = `required input(s) still null: ${unfilled.join(', ')}`;
        else if (mistyped.length > 0) argErrors[step.id] = mistyped.join('; ');
      } else {
        argErrors[step.id] = 'arguments must be a JSON object';
      }
    } catch (e) {
      argErrors[step.id] = `invalid JSON: ${e instanceof Error ? e.message : String(e)}`;
    }
    const deps = [...new Set(edges.filter((e) => e.target === step.id).map((e) => e.source))];
    return deps.length > 0
      ? { id: step.id, tool: step.tool, arguments: args, depends_on: deps }
      : { id: step.id, tool: step.tool, arguments: args };
  });
  return { spec: { steps: specSteps }, argErrors };
}

function matchesType(value: unknown, type: string | string[] | undefined): boolean {
  if (type === undefined) return true;
  const types = Array.isArray(type) ? type : [type];
  return types.some((t) => {
    switch (t) {
      case 'number': return typeof value === 'number';
      case 'integer': return typeof value === 'number' && Number.isInteger(value);
      case 'string': return typeof value === 'string';
      case 'boolean': return typeof value === 'boolean';
      case 'array': return Array.isArray(value);
      case 'object': return value !== null && typeof value === 'object' && !Array.isArray(value);
      case 'null': return value === null;
      default: return true; // a type the editor does not model: leave it to IX
    }
  });
}

/** A `$step.field` reference is resolved at run time, so its type is unknown here. */
const isReference = (v: unknown) => typeof v === 'string' && v.startsWith('$');

/**
 * Checks each supplied argument, and each item of an array argument, against
 * the catalog's declared type. ix_pipeline_validate does not check argument
 * types, so without this `{"data": ["x"]}` for a list of numbers reads as
 * valid and fails only at run time. Nulls are the required-input check's job.
 */
export function typeErrors(args: Record<string, unknown>, properties: Record<string, SchemaProperty>): string[] {
  const out: string[] = [];
  for (const [key, value] of Object.entries(args)) {
    const prop = properties[key];
    if (!prop || value === null || isReference(value)) continue;
    if (!matchesType(value, prop.type)) {
      out.push(`${key}: expected ${[prop.type].flat().join(' | ')}`);
    } else if (Array.isArray(value) && prop.items?.type !== undefined) {
      const bad = value.findIndex((item) => !isReference(item) && !matchesType(item, prop.items?.type));
      if (bad >= 0) out.push(`${key}[${bad}]: expected ${[prop.items.type].flat().join(' | ')}`);
    }
  }
  return out;
}

/** Groups validation errors and warnings by step id; step-less ones go under ''. */
export function issuesByStep(report: ValidationReport | null): Map<string, { errors: string[]; warnings: string[] }> {
  const out = new Map<string, { errors: string[]; warnings: string[] }>();
  if (!report) return out;
  const slot = (id: string) => {
    let s = out.get(id);
    if (!s) { s = { errors: [], warnings: [] }; out.set(id, s); }
    return s;
  };
  for (const e of report.errors ?? []) slot(e.step ?? '').errors.push(e.message);
  for (const w of report.warnings ?? []) slot(w.step ?? '').warnings.push(w.message);
  return out;
}
