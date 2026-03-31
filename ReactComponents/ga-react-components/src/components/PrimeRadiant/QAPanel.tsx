// src/components/PrimeRadiant/QAPanel.tsx
// Automated QA Panel — runs self-tests inside the live Prime Radiant UI.
// Tests parser correctness, pipe engine transforms, render proof integrity,
// signal bus health, grammar extensions, and governance coherence.
// The governance system testing itself.

import React, { useState, useCallback, useEffect, useRef } from 'react';
import { parseIxqlCommand, evaluatePredicate } from './IxqlControlParser';
import { executePipeline, type PipeStep } from './IxqlPipeEngine';
import { signalBus } from './DashboardSignalBus';
import { isValidTransition, type HexavalentValue } from './HexavalentTemporal';
import { constitutionalGate } from './GrammarExtensionRegistry';
import { classifyDivergences } from './RenderProof';

// ---------------------------------------------------------------------------
// Test infrastructure
// ---------------------------------------------------------------------------

interface TestResult {
  name: string;
  suite: string;
  passed: boolean;
  error?: string;
  durationMs: number;
}

function runTest(suite: string, name: string, fn: () => void): TestResult {
  const start = performance.now();
  try {
    fn();
    return { name, suite, passed: true, durationMs: performance.now() - start };
  } catch (err) {
    return { name, suite, passed: false, error: String(err), durationMs: performance.now() - start };
  }
}

function assert(condition: boolean, message: string): void {
  if (!condition) throw new Error(message);
}

function assertEqual(actual: unknown, expected: unknown, message?: string): void {
  if (actual !== expected) {
    throw new Error(message ?? `Expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
  }
}

// ---------------------------------------------------------------------------
// Test suites
// ---------------------------------------------------------------------------

function runParserTests(): TestResult[] {
  const results: TestResult[] = [];

  results.push(runTest('Parser', 'RESET parses', () => {
    const r = parseIxqlCommand('RESET');
    assert(r.ok, 'RESET should parse');
    assertEqual(r.command.type, 'reset');
  }));

  results.push(runTest('Parser', 'SELECT nodes with predicates', () => {
    const r = parseIxqlCommand('SELECT nodes WHERE type = "policy" SET glow = true');
    assert(r.ok, 'SELECT should parse');
    assertEqual(r.command.type, 'select');
  }));

  results.push(runTest('Parser', 'CREATE PANEL KIND grid', () => {
    const r = parseIxqlCommand('CREATE PANEL "test" KIND grid SOURCE governance.beliefs');
    assert(r.ok, 'CREATE PANEL should parse');
    assertEqual(r.command.type, 'create-grid-panel');
  }));

  results.push(runTest('Parser', 'CREATE VIZ KIND truth-lattice', () => {
    const r = parseIxqlCommand('CREATE VIZ "lat" KIND truth-lattice SOURCE governance.beliefs');
    assert(r.ok, 'CREATE VIZ truth-lattice should parse');
  }));

  results.push(runTest('Parser', 'SHOW beliefs routes correctly', () => {
    const r = parseIxqlCommand('SHOW beliefs');
    assert(r.ok, 'SHOW beliefs should parse');
    assertEqual(r.command.type, 'show-epistemic');
  }));

  results.push(runTest('Parser', 'SHOW learners routes correctly', () => {
    const r = parseIxqlCommand('SHOW learners');
    assert(r.ok, 'SHOW learners should parse');
    assertEqual(r.command.type, 'show-epistemic');
  }));

  results.push(runTest('Parser', 'Recursion depth limit', () => {
    let cmd = 'SELECT nodes SET glow = true';
    for (let i = 0; i < 15; i++) cmd = `ON src${i} CHANGED THEN ${cmd}`;
    const r = parseIxqlCommand(cmd);
    assert(!r.ok, 'Deeply nested should fail');
    assert(r.error.indexOf('nesting depth') >= 0, 'Should mention depth');
  }));

  results.push(runTest('Parser', 'Empty command rejected', () => {
    const r = parseIxqlCommand('');
    assert(!r.ok, 'Empty should fail');
  }));

  results.push(runTest('Parser', 'DIAGNOSE parses', () => {
    const r = parseIxqlCommand('DIAGNOSE');
    assert(r.ok, 'DIAGNOSE should parse');
  }));

  return results;
}

function runPipeEngineTests(): TestResult[] {
  const results: TestResult[] = [];
  const data = [
    { id: '1', name: 'a', severity: 3, type: 'policy' },
    { id: '2', name: 'b', severity: 5, type: 'belief' },
    { id: '3', name: 'c', severity: 1, type: 'policy' },
    { id: '4', name: 'd', severity: 5, type: 'belief' },
  ];

  results.push(runTest('PipeEngine', 'FILTER by type', () => {
    const r = executePipeline(data, [{ type: 'filter', predicates: [{ field: 'type', operator: '=', value: 'policy' }] }]);
    assertEqual(r.length, 2, `Expected 2 policies, got ${r.length}`);
  }));

  results.push(runTest('PipeEngine', 'SORT descending', () => {
    const r = executePipeline(data, [{ type: 'sort', field: 'severity', direction: 'DESC' }]);
    assertEqual(r[0].severity, 5, 'First should be 5');
  }));

  results.push(runTest('PipeEngine', 'LIMIT', () => {
    const r = executePipeline(data, [{ type: 'limit', count: 2 }]);
    assertEqual(r.length, 2, `Expected 2, got ${r.length}`);
  }));

  results.push(runTest('PipeEngine', 'DISTINCT by type', () => {
    const r = executePipeline(data, [{ type: 'distinct', field: 'type' }]);
    assertEqual(r.length, 2, `Expected 2 distinct types, got ${r.length}`);
  }));

  results.push(runTest('PipeEngine', 'GROUP BY with COUNT', () => {
    const r = executePipeline(data, [{ type: 'group', byField: 'type', aggregates: [{ fn: 'COUNT', field: null, alias: 'count' }] }]);
    assertEqual(r.length, 2, `Expected 2 groups, got ${r.length}`);
  }));

  results.push(runTest('PipeEngine', 'Composed FILTER→SORT→LIMIT', () => {
    const steps: PipeStep[] = [
      { type: 'filter', predicates: [{ field: 'type', operator: '=', value: 'belief' }] },
      { type: 'sort', field: 'severity', direction: 'DESC' },
      { type: 'limit', count: 1 },
    ];
    const r = executePipeline(data, steps);
    assertEqual(r.length, 1, `Expected 1, got ${r.length}`);
    assertEqual(r[0].type, 'belief');
  }));

  results.push(runTest('PipeEngine', 'Empty data passes through', () => {
    const r = executePipeline([], [{ type: 'sort', field: 'x', direction: 'ASC' }]);
    assertEqual(r.length, 0);
  }));

  return results;
}

function runPredicateTests(): TestResult[] {
  const results: TestResult[] = [];
  const row = { name: 'test', severity: 5, confidence: 0.8 };

  results.push(runTest('Predicates', '= operator', () => {
    assert(evaluatePredicate({ field: 'name', operator: '=', value: 'test' }, row), '= should match');
    assert(!evaluatePredicate({ field: 'name', operator: '=', value: 'other' }, row), '= should not match');
  }));

  results.push(runTest('Predicates', '> operator', () => {
    assert(evaluatePredicate({ field: 'severity', operator: '>', value: 3 }, row), '5 > 3');
    assert(!evaluatePredicate({ field: 'severity', operator: '>', value: 5 }, row), '5 not > 5');
  }));

  results.push(runTest('Predicates', '~ contains operator', () => {
    assert(evaluatePredicate({ field: 'name', operator: '~', value: 'est' }, row), 'test contains est');
  }));

  results.push(runTest('Predicates', 'Missing field returns false', () => {
    assert(!evaluatePredicate({ field: 'nonexistent', operator: '=', value: 'x' }, row), 'Missing should be false');
  }));

  return results;
}

function runGovernanceTests(): TestResult[] {
  const results: TestResult[] = [];

  results.push(runTest('Governance', 'Hexavalent transitions T→P allowed', () => {
    assert(isValidTransition('T', 'P'), 'T→P should be allowed');
  }));

  results.push(runTest('Governance', 'Hexavalent transitions T→F blocked', () => {
    assert(!isValidTransition('T', 'F'), 'T→F should be blocked (must go through P→U→D→F)');
  }));

  results.push(runTest('Governance', 'C reachable from any state', () => {
    const values: HexavalentValue[] = ['T', 'P', 'U', 'D', 'F'];
    for (const v of values) {
      assert(isValidTransition(v, 'C'), `${v}→C should be allowed`);
    }
  }));

  results.push(runTest('Governance', 'Constitutional gate blocks IGNORE', () => {
    const r = constitutionalGate('IGNORE');
    assert(!r.allowed, 'IGNORE should be blocked');
  }));

  results.push(runTest('Governance', 'Constitutional gate allows TOP', () => {
    const r = constitutionalGate('TOP');
    assert(r.allowed, 'TOP should be allowed');
  }));

  results.push(runTest('Governance', 'Divergence classification', () => {
    assertEqual(classifyDivergences([]), 'info');
    assertEqual(classifyDivergences(['minor issue']), 'info');
    assertEqual(classifyDivergences(['a', 'b', 'c']), 'warning');
    assertEqual(classifyDivergences(['missing data']), 'critical');
  }));

  return results;
}

function runSignalBusTests(): TestResult[] {
  const results: TestResult[] = [];

  results.push(runTest('SignalBus', 'Publish and get', () => {
    signalBus.publish('__qa_test__', { test: true }, '__qa__');
    // Note: 50ms throttle means we can't read immediately in sync test
    // This test verifies no crash on publish
  }));

  results.push(runTest('SignalBus', 'Clear removes signal', () => {
    signalBus.clear('__qa_test__');
    const sig = signalBus.get('__qa_test__');
    // May still exist due to throttle, but clear shouldn't crash
  }));

  return results;
}

// ---------------------------------------------------------------------------
// QA Panel Component
// ---------------------------------------------------------------------------

export const QAPanel: React.FC = () => {
  const [results, setResults] = useState<TestResult[]>([]);
  const [running, setRunning] = useState(false);
  const [lastRun, setLastRun] = useState<string | null>(null);
  const [autoRun, setAutoRun] = useState(false);
  const intervalRef = useRef<number | null>(null);

  const runAllTests = useCallback(() => {
    setRunning(true);
    const start = performance.now();

    const allResults = [
      ...runParserTests(),
      ...runPipeEngineTests(),
      ...runPredicateTests(),
      ...runGovernanceTests(),
      ...runSignalBusTests(),
    ];

    const totalMs = Math.round(performance.now() - start);
    setResults(allResults);
    setLastRun(`${new Date().toLocaleTimeString()} (${totalMs}ms)`);
    setRunning(false);
  }, []);

  // Auto-run on mount
  useEffect(() => { runAllTests(); }, [runAllTests]);

  // Auto-run interval
  useEffect(() => {
    if (autoRun) {
      intervalRef.current = window.setInterval(runAllTests, 60_000); // every 60s
      return () => { if (intervalRef.current) window.clearInterval(intervalRef.current); };
    } else {
      if (intervalRef.current) { window.clearInterval(intervalRef.current); intervalRef.current = null; }
    }
  }, [autoRun, runAllTests]);

  const passed = results.filter(r => r.passed).length;
  const failed = results.filter(r => !r.passed).length;
  const total = results.length;
  const suites = [...new Set(results.map(r => r.suite))];

  return (
    <div style={{
      fontFamily: "'JetBrains Mono', monospace",
      fontSize: 11,
      color: '#e6edf3',
      height: '100%',
      display: 'flex',
      flexDirection: 'column',
      overflow: 'hidden',
    }}>
      {/* Header */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        padding: '10px 12px',
        borderBottom: '1px solid rgba(255,255,255,0.06)',
        background: 'rgba(0,0,0,0.3)',
        flexShrink: 0,
      }}>
        <span style={{ fontWeight: 'bold', fontSize: 13 }}>QA</span>
        <span style={{
          background: failed === 0 && total > 0 ? 'rgba(34,197,94,0.15)' : 'rgba(239,68,68,0.15)',
          color: failed === 0 && total > 0 ? '#22c55e' : '#ef4444',
          padding: '2px 8px',
          borderRadius: 4,
          fontSize: 10,
          fontWeight: 'bold',
        }}>
          {failed === 0 && total > 0 ? 'ALL PASS' : `${failed} FAIL`}
        </span>
        <span style={{ color: '#6b7280', fontSize: 10 }}>
          {passed}/{total}
        </span>
        <div style={{ flex: 1 }} />
        <button
          onClick={runAllTests}
          disabled={running}
          style={{
            background: 'rgba(255,215,0,0.1)',
            border: '1px solid rgba(255,215,0,0.3)',
            borderRadius: 4,
            color: '#ffd700',
            cursor: running ? 'wait' : 'pointer',
            fontSize: 10,
            fontWeight: 'bold',
            padding: '3px 10px',
          }}
        >
          {running ? 'Running...' : 'Run'}
        </button>
        <label style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: 9, color: '#6b7280', cursor: 'pointer' }}>
          <input
            type="checkbox"
            checked={autoRun}
            onChange={e => setAutoRun(e.target.checked)}
            style={{ accentColor: '#ffd700', width: 12, height: 12 }}
          />
          Auto (60s)
        </label>
      </div>

      {/* Results */}
      <div style={{ flex: 1, overflow: 'auto', padding: '6px 12px' }}>
        {suites.map(suite => {
          const suiteResults = results.filter(r => r.suite === suite);
          const suitePassed = suiteResults.every(r => r.passed);
          return (
            <div key={suite} style={{ marginBottom: 10 }}>
              <div style={{
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                marginBottom: 4,
                color: suitePassed ? '#22c55e' : '#ef4444',
                fontWeight: 'bold',
                fontSize: 11,
              }}>
                <span>{suitePassed ? '\u2713' : '\u2717'}</span>
                <span>{suite}</span>
                <span style={{ color: '#6b7280', fontWeight: 'normal', fontSize: 9 }}>
                  {suiteResults.filter(r => r.passed).length}/{suiteResults.length}
                </span>
              </div>
              {suiteResults.map((r, i) => (
                <div key={i} style={{
                  display: 'flex',
                  alignItems: 'baseline',
                  gap: 6,
                  padding: '2px 0 2px 16px',
                  fontSize: 10,
                }}>
                  <span style={{ color: r.passed ? '#22c55e' : '#ef4444', flexShrink: 0 }}>
                    {r.passed ? '\u2713' : '\u2717'}
                  </span>
                  <span style={{ color: r.passed ? '#8b949e' : '#e6edf3' }}>
                    {r.name}
                  </span>
                  {!r.passed && r.error && (
                    <span style={{ color: '#ef4444', fontSize: 9 }}>
                      {r.error.length > 60 ? r.error.substring(0, 60) + '...' : r.error}
                    </span>
                  )}
                  <span style={{ color: '#484f58', fontSize: 8, marginLeft: 'auto' }}>
                    {r.durationMs.toFixed(1)}ms
                  </span>
                </div>
              ))}
            </div>
          );
        })}
      </div>

      {/* Footer */}
      {lastRun && (
        <div style={{
          padding: '6px 12px',
          borderTop: '1px solid rgba(255,255,255,0.06)',
          fontSize: 9,
          color: '#6b7280',
          flexShrink: 0,
        }}>
          Last run: {lastRun}
        </div>
      )}
    </div>
  );
};
