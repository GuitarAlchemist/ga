// src/components/PrimeRadiant/IxqlWidgetSpec.ts
// WidgetSpec types and compiler — transforms IXQL AST → renderable widget descriptors.
// Phase 1: PanelSpec (AG-Grid). Future: VizSpec (D3), FormSpec (MUI).

import type { CreateGridPanelCommand, CreateVizCommand, CreateFormCommand, ProjectionField, PipeStep, IxqlPredicate, VizKind, FormFieldDef } from './IxqlControlParser';

// ---------------------------------------------------------------------------
// Whitelisted pure functions for PROJECT expressions
// ---------------------------------------------------------------------------

type PureFn = (value: unknown) => unknown;

const PURE_FUNCTIONS: Record<string, PureFn> = {
  DAYS_SINCE: (val: unknown) => {
    if (!val) return null;
    const d = new Date(val as string);
    if (isNaN(d.getTime())) return null;
    return Math.floor((Date.now() - d.getTime()) / 86_400_000);
  },
  FORMAT_PERCENT: (val: unknown) => {
    const n = Number(val);
    return isNaN(n) ? null : `${(n * 100).toFixed(1)}%`;
  },
  COALESCE: (val: unknown) => val ?? '—',
  UPPERCASE: (val: unknown) => (val != null ? String(val).toUpperCase() : null),
};

// ---------------------------------------------------------------------------
// WidgetSpec types
// ---------------------------------------------------------------------------

export interface DataBindingSpec {
  source: string;            // registered data source path
  wherePredicates: IxqlPredicate[];
  dependsOn: string[];       // signal names this widget subscribes to
}

export interface ProjectionSpec {
  fields: CompiledProjectionField[];
}

export interface CompiledProjectionField {
  name: string;              // output column name / alias
  sourcePath: string;        // dot-path in source data
  transform: PureFn | null;  // whitelisted pure function
}

export interface ResponsiveLayoutSpec {
  xs?: number;
  sm?: number;
  md?: number;
  lg?: number;
  xl?: number;
}

export interface PipelineSpec {
  steps: PipeStep[];
}

export interface PanelSpec {
  type: 'panel';
  id: string;
  kind: 'grid';
  template: string | null;
  binding: DataBindingSpec;
  projection: ProjectionSpec | null;
  pipeline: PipelineSpec | null;   // PIPE transforms between fetch and projection
  layout: ResponsiveLayoutSpec;
  governedBy: number[];
  publish: { signal: string; as: string } | null;
  subscribe: string[];
  refresh: number | null;
  live: boolean;
}

export interface VizSpec {
  type: 'viz';
  id: string;
  kind: VizKind;
  binding: DataBindingSpec;
  pipeline: PipelineSpec | null;
  nodeField: string | null;
  edgeSource: string | null;
  edgeFrom: string | null;
  edgeTo: string | null;
  colorField: string | null;
  sizeField: string | null;
  labelField: string | null;
  layout: ResponsiveLayoutSpec;
  governedBy: number[];
  publish: { signal: string; as: string } | null;
  subscribe: string[];
  refresh: number | null;
}

export interface FormSpec {
  type: 'form';
  id: string;
  fields: FormFieldDef[];
  constraints: { field: string; condition: string }[];
  submitCommand: string | null;
  onSuccess: string[];
  hexavalent: boolean;
  governedBy: number[];
  publish: { signal: string; as: string } | null;
  subscribe: string[];
}

export type WidgetSpec = PanelSpec | VizSpec | FormSpec;

// ---------------------------------------------------------------------------
// Compiler: CreateGridPanelCommand → PanelSpec
// ---------------------------------------------------------------------------

function compileProjectionField(field: ProjectionField): CompiledProjectionField {
  const { name, expression } = field;

  // Check for function call: FUNC_NAME(arg)
  const parenOpen = expression.indexOf('(');
  const parenClose = expression.lastIndexOf(')');
  if (parenOpen > 0 && parenClose === expression.length - 1) {
    const fnName = expression.substring(0, parenOpen);
    const argPath = expression.substring(parenOpen + 1, parenClose);
    const fn = PURE_FUNCTIONS[fnName];
    if (!fn) {
      throw new Error(`Unknown function '${fnName}' in PROJECT. Allowed: ${Object.keys(PURE_FUNCTIONS).join(', ')}`);
    }
    return { name, sourcePath: argPath, transform: fn };
  }

  // Plain field reference
  return { name, sourcePath: expression, transform: null };
}

function compileLayout(
  breakpoints: { breakpoint: string; cols: number }[],
): ResponsiveLayoutSpec {
  const spec: ResponsiveLayoutSpec = {};
  for (const bp of breakpoints) {
    (spec as Record<string, number>)[bp.breakpoint] = bp.cols;
  }
  // Default: full width if nothing specified
  if (Object.keys(spec).length === 0) {
    spec.xs = 12;
  }
  return spec;
}

export function compileGridPanel(cmd: CreateGridPanelCommand): PanelSpec {
  // Auto-subscribe to __refresh__ signal so ON_SUCCESS REFRESH from forms works
  const subscribe = [...cmd.subscribe];
  if (subscribe.indexOf('__refresh__' + cmd.id) < 0) {
    subscribe.push('__refresh__' + cmd.id);
  }
  return {
    type: 'panel',
    id: cmd.id,
    kind: 'grid',
    template: cmd.template,
    binding: {
      source: cmd.source,
      wherePredicates: cmd.wherePredicates,
      dependsOn: subscribe,
    },
    projection: cmd.project.length > 0
      ? { fields: cmd.project.map(compileProjectionField) }
      : null,
    pipeline: cmd.pipe.length > 0 ? { steps: cmd.pipe } : null,
    layout: compileLayout(cmd.layout),
    governedBy: cmd.governedBy,
    publish: cmd.publish,
    subscribe,
    refresh: cmd.refresh,
    live: cmd.live,
  };
}

export function compileViz(cmd: CreateVizCommand): VizSpec {
  const subscribe = [...cmd.subscribe];
  if (subscribe.indexOf('__refresh__' + cmd.id) < 0) {
    subscribe.push('__refresh__' + cmd.id);
  }
  return {
    type: 'viz',
    id: cmd.id,
    kind: cmd.kind,
    binding: {
      source: cmd.source,
      wherePredicates: cmd.wherePredicates,
      dependsOn: subscribe,
    },
    pipeline: cmd.pipe.length > 0 ? { steps: cmd.pipe } : null,
    nodeField: cmd.nodeField,
    edgeSource: cmd.edgeSource,
    edgeFrom: cmd.edgeFrom,
    edgeTo: cmd.edgeTo,
    colorField: cmd.colorField,
    sizeField: cmd.sizeField,
    labelField: cmd.labelField,
    layout: compileLayout(cmd.layout),
    governedBy: cmd.governedBy,
    publish: cmd.publish,
    subscribe,
    refresh: cmd.refresh,
  };
}

export function compileForm(cmd: CreateFormCommand): FormSpec {
  // Auto-subscribe to __refresh__ signal so ON_SUCCESS REFRESH works
  const subscribe = [...cmd.subscribe];
  if (subscribe.indexOf('__refresh__' + cmd.id) < 0) {
    subscribe.push('__refresh__' + cmd.id);
  }
  return {
    type: 'form',
    id: cmd.id,
    fields: cmd.fields,
    constraints: cmd.constraints,
    submitCommand: cmd.submitCommand,
    onSuccess: cmd.onSuccess,
    hexavalent: cmd.hexavalent,
    governedBy: cmd.governedBy,
    publish: null, // forms don't publish by default; future: PUBLISH clause in grammar
    subscribe,
  };
}

// ---------------------------------------------------------------------------
// Runtime: apply projection to a data row
// ---------------------------------------------------------------------------

import { resolveField } from './DataFetcher';

export function applyProjection(
  row: Record<string, unknown>,
  projection: ProjectionSpec,
): Record<string, unknown> {
  const result: Record<string, unknown> = {};
  for (const field of projection.fields) {
    const raw = resolveField(row, field.sourcePath);
    result[field.name] = field.transform ? field.transform(raw) : raw;
  }
  return result;
}
