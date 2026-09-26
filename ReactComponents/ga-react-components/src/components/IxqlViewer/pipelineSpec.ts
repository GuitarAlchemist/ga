// Pure model for the IX pipeline editor: editor graph <-> the
// `ix_pipeline_run` / `ix_pipeline_validate` spec ({ steps: [...] }).

/** One entry of `ix_node_catalog`. Only the fields the editor reads. */
export interface CatalogNode {
  name: string;
  description: string;
  required_inputs: string[];
  input_schema?: { properties?: Record<string, { type?: string; description?: string }> };
  approval?: { tier?: string; effect?: string };
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
): { spec: PipelineSpec; argErrors: Record<string, string> } {
  const argErrors: Record<string, string> = {};
  const specSteps = steps.map((step) => {
    let args: Record<string, unknown> = {};
    try {
      const parsed: unknown = step.argsText.trim() ? JSON.parse(step.argsText) : {};
      if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
        args = parsed as Record<string, unknown>;
        const unfilled = (requiredByTool[step.tool] ?? []).filter((k) => args[k] === null);
        if (unfilled.length > 0) argErrors[step.id] = `required input(s) still null: ${unfilled.join(', ')}`;
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
