import { describe, expect, it } from 'vitest';
import { buildSpec, bundleEdges, defaultArgsText, groupOf, groupedPositions, issuesByStep, nextStepId, specToGraph, typeErrors, type ValidationReport } from './pipelineSpec';

describe('buildSpec', () => {
  it('turns edges into depends_on and parses arguments (shape from docs/pipelines/dag-execution.md)', () => {
    const { spec, argErrors } = buildSpec(
      [
        { id: 'a', tool: 'ix_stats', argsText: '{"data":[1,2,3]}' },
        { id: 'b', tool: 'ix_cache', argsText: '{"operation":"set","key":"k","value":"$a.mean"}' },
      ],
      [{ source: 'a', target: 'b' }],
    );
    expect(argErrors).toEqual({});
    expect(spec).toEqual({
      steps: [
        { id: 'a', tool: 'ix_stats', arguments: { data: [1, 2, 3] } },
        { id: 'b', tool: 'ix_cache', arguments: { operation: 'set', key: 'k', value: '$a.mean' }, depends_on: ['a'] },
      ],
    });
  });

  it('reports bad arguments per step without dropping the step', () => {
    const { spec, argErrors } = buildSpec(
      [
        { id: 'a', tool: 'ix_stats', argsText: '{"data":' },
        { id: 'b', tool: 'ix_stats', argsText: '[1]' },
      ],
      [],
    );
    expect(Object.keys(argErrors).sort()).toEqual(['a', 'b']);
    expect(spec.steps.map((s) => s.arguments)).toEqual([{}, {}]);
  });

  it('flags required inputs left at the null template (the validator would pass them)', () => {
    const { spec, argErrors } = buildSpec(
      [
        { id: 'a', tool: 'ix_stats', argsText: '{"data":null}' },
        { id: 'b', tool: 'ix_stats', argsText: '{"data":[1]}' },
      ],
      [],
      { ix_stats: ['data'] },
    );
    expect(argErrors).toEqual({ a: 'required input(s) still null: data' });
    expect(spec.steps[0].arguments).toEqual({ data: null });
  });

  it('deduplicates parallel edges', () => {
    const { spec } = buildSpec(
      [{ id: 'a', tool: 't', argsText: '' }, { id: 'b', tool: 't', argsText: '' }],
      [{ source: 'a', target: 'b' }, { source: 'a', target: 'b' }],
    );
    expect(spec.steps[1].depends_on).toEqual(['a']);
  });
});

describe('helpers', () => {
  it('defaultArgsText lists the required inputs', () => {
    expect(JSON.parse(defaultArgsText({ name: 'ix_stats', description: '', required_inputs: ['data'] }))).toEqual({ data: null });
  });

  it('nextStepId fills the first gap', () => {
    expect(nextStepId([{ id: 's1', tool: 't', argsText: '' }, { id: 's3', tool: 't', argsText: '' }])).toBe('s2');
  });

  it('issuesByStep groups the real ix_pipeline_validate report shape', () => {
    // Captured from ix-mcp (ix 1e328a6) for a missing input + unknown tool.
    const report: ValidationReport = {
      valid: false,
      errors: [
        { code: 'missing_required_input', index: 0, message: "step 'a': tool 'ix_stats' requires input 'data'", step: 'a' },
        { code: 'unknown_tool', index: 1, message: "step 'b': unknown tool 'ix_nope'", step: 'b' },
        { code: 'missing_steps', message: 'no steps' },
      ],
      warnings: [],
      execution_order: null,
    };
    const byStep = issuesByStep(report);
    expect(byStep.get('a')?.errors).toHaveLength(1);
    expect(byStep.get('b')?.errors[0]).toContain('unknown tool');
    expect(byStep.get('')?.errors).toEqual(['no steps']);
  });
});

describe('argument types', () => {
  // ix_stats as ix_node_catalog declares it.
  const stats = { data: { type: 'array', items: { type: 'number' } } };
  const build = (argsText: string) =>
    buildSpec([{ id: 's1', tool: 'ix_stats', argsText }], [], { ix_stats: ['data'] }, { ix_stats: stats }).argErrors;

  it('accepts a list of numbers', () => {
    expect(build('{"data":[1,2,3]}')).toEqual({});
  });

  it('flags an item of the wrong type, which ix_pipeline_validate lets through', () => {
    expect(build('{"data":["not-a-number"]}').s1).toBe('data[0]: expected number');
  });

  it('flags a value of the wrong type', () => {
    expect(build('{"data":"1,2,3"}').s1).toBe('data: expected array');
  });

  it('leaves $step.field references to run time', () => {
    expect(build('{"data":"$s0.values"}')).toEqual({});
    expect(typeErrors({ data: [1, '$s0.mean'] }, stats)).toEqual([]);
  });

  it('reports the null placeholder as unfilled, not as mistyped', () => {
    expect(build('{"data":null}').s1).toContain('still null');
  });

  it('accepts types it does not model and union types', () => {
    expect(typeErrors({ a: 1, b: 'x' }, { a: { type: ['number', 'null'] }, b: { type: 'custom' } })).toEqual([]);
    expect(typeErrors({ n: 1.5 }, { n: { type: 'integer' } })).toEqual(['n: expected integer']);
  });
});

describe('specToGraph', () => {
  it('round-trips through buildSpec', () => {
    const spec = {
      steps: [
        { id: 'a', tool: 'ix_stats', arguments: { data: [1, 2] } },
        { id: 'b', tool: 'ix_stats', arguments: { data: ['$a.mean'] }, depends_on: ['a'] },
      ],
    };
    const { steps, edges } = specToGraph(spec);
    expect(edges).toEqual([{ source: 'a', target: 'b' }]);
    expect(buildSpec(steps, edges).spec).toEqual(spec);
  });

  it('refuses shapes it cannot represent', () => {
    expect(() => specToGraph({})).toThrow('"steps" array');
    expect(() => specToGraph({ steps: [{ id: 'a', tool: 'x' }, { id: 'a', tool: 'y' }] })).toThrow('duplicate');
    expect(() => specToGraph({ steps: [{ id: 'a', tool: 'x', depends_on: ['nope'] }] })).toThrow('unknown step "nope"');
    expect(() => specToGraph({ steps: [{ id: 'a' }] })).toThrow('"tool"');
  });
});

describe('sections', () => {
  const st = (id: string, tool = 'ix_stats') => ({ id, tool, argsText: '{}' });

  it('groups by id prefix, else by tool', () => {
    expect(groupOf(st('delta_I_IV'))).toBe('delta');
    expect(groupOf(st('s1', 'ix_pca'))).toBe('ix_pca');
  });

  it('bundles edges between sections and drops edges inside one', () => {
    const steps = [st('a_1'), st('a_2'), st('b_1'), st('b_2')];
    const edges = [
      { source: 'a_1', target: 'b_1' }, { source: 'a_2', target: 'b_1' },
      { source: 'a_1', target: 'b_2' }, { source: 'b_1', target: 'b_2' },
    ];
    expect(bundleEdges(steps, edges)).toEqual([{ source: 'a', target: 'b', count: 3 }]);
  });

  it('wraps a row of sections wider than maxRowWidth', () => {
    const steps = [st('a_1'), st('b_1'), st('c_1')];
    const pos = groupedPositions(steps, [], { dx: 100, gapX: 0, maxRowWidth: 200 });
    expect(pos.a_1.y).toBe(pos.b_1.y);
    expect(pos.c_1.y).toBeGreaterThan(pos.a_1.y);
    expect(pos.c_1.x).toBe(0);
  });

  it('keeps a section together and below the section it reads from', () => {
    const steps = [st('a_1'), st('a_2'), st('a_3'), st('b_1')];
    const pos = groupedPositions(steps, [{ source: 'a_1', target: 'b_1' }], { cols: 2 });
    expect(pos.b_1.y).toBeGreaterThan(Math.max(pos.a_1.y, pos.a_2.y, pos.a_3.y));
    expect(new Set(Object.values(pos).map((p) => `${p.x},${p.y}`)).size).toBe(4);
  });
});
