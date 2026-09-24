import { describe, expect, it } from 'vitest';
import { buildSpec, defaultArgsText, issuesByStep, nextStepId, type ValidationReport } from './pipelineSpec';

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
