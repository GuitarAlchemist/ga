#!/usr/bin/env npx ts-node
// generate-live-data.ts
// Build-time script: scans Demerzel state/ directory and generates liveData.ts
// Run: npx ts-node src/components/PrimeRadiant/generate-live-data.ts
//
// This script reads the actual Demerzel governance artifacts and state files,
// computes real ERGOL/LOLLI/R scores, and outputs a TypeScript module that
// the Prime Radiant component imports at build time.

import * as fs from 'fs';
import * as path from 'path';

// ---------------------------------------------------------------------------
// Config — adjust DEMERZEL_ROOT if the relative path changes
// ---------------------------------------------------------------------------
const DEMERZEL_ROOT = process.env.DEMERZEL_ROOT
  || path.resolve(__dirname, '../../../../../../Demerzel');
// Default: C:\Users\spare\source\repos\Demerzel (set DEMERZEL_ROOT env var to override)

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
function readJsonSafe(filePath: string): Record<string, unknown> | null {
  try {
    return JSON.parse(fs.readFileSync(filePath, 'utf-8'));
  } catch {
    return null;
  }
}

function listFiles(dir: string, ext?: string): string[] {
  try {
    const files = fs.readdirSync(dir);
    return ext ? files.filter(f => f.endsWith(ext)) : files;
  } catch {
    return [];
  }
}

function kebabToTitle(kebab: string): string {
  return kebab
    .split('-')
    .map(w => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}

// ---------------------------------------------------------------------------
// File tree builder
// ---------------------------------------------------------------------------
interface FileTreeNode {
  name: string;
  path: string;
  type: 'file' | 'directory';
  children?: FileTreeNode[];
  extension?: string;
}

function getExtension(fileName: string): string | undefined {
  const parts = fileName.split('.');
  return parts.length > 1 ? parts[parts.length - 1] : undefined;
}

function buildFileTree(dirPath: string, relativeTo: string): FileTreeNode[] {
  try {
    const entries = fs.readdirSync(dirPath, { withFileTypes: true });
    return entries
      .sort((a, b) => {
        // directories first, then alphabetical
        if (a.isDirectory() && !b.isDirectory()) return -1;
        if (!a.isDirectory() && b.isDirectory()) return 1;
        return a.name.localeCompare(b.name);
      })
      .map(entry => {
        const fullPath = path.join(dirPath, entry.name);
        const relPath = path.relative(relativeTo, fullPath).replace(/\\/g, '/');
        if (entry.isDirectory()) {
          return {
            name: entry.name,
            path: relPath,
            type: 'directory' as const,
            children: buildFileTree(fullPath, relativeTo),
          };
        }
        return {
          name: entry.name,
          path: relPath,
          type: 'file' as const,
          extension: getExtension(entry.name),
        };
      });
  } catch {
    return [];
  }
}

function singleFileTree(fileName: string, dirRelative: string): FileTreeNode[] {
  const relPath = `${dirRelative}/${fileName}`;
  return [{
    name: fileName,
    path: relPath,
    type: 'file' as const,
    extension: getExtension(fileName),
  }];
}

// ---------------------------------------------------------------------------
// Scan artifacts
// ---------------------------------------------------------------------------
interface ArtifactInfo {
  id: string;
  name: string;
  fileName: string;
  version?: string;
  description?: string;
  fileTree?: FileTreeNode[];
}

function scanYamlVersion(filePath: string): string | undefined {
  try {
    const content = fs.readFileSync(filePath, 'utf-8');
    const match = content.match(/^version:\s*["']?([^"'\n]+)["']?/m);
    return match?.[1];
  } catch {
    return undefined;
  }
}

function scanYamlDescription(filePath: string): string | undefined {
  try {
    const content = fs.readFileSync(filePath, 'utf-8');
    const match = content.match(/^description:\s*["']?([^"'\n]+)["']?/m);
    return match?.[1]?.slice(0, 200);
  } catch {
    return undefined;
  }
}

function scanConstitutions(): ArtifactInfo[] {
  const dir = path.join(DEMERZEL_ROOT, 'constitutions');
  return listFiles(dir, '.md').map(f => {
    const base = f.replace(/\.constitution\.md$/, '').replace(/\.md$/, '');
    return {
      id: `const-${base}`,
      name: kebabToTitle(base) + (f.includes('constitution') ? ' Constitution' : ''),
      fileName: f,
      fileTree: buildFileTree(dir, DEMERZEL_ROOT),
    };
  });
}

function scanPolicies(): ArtifactInfo[] {
  const dir = path.join(DEMERZEL_ROOT, 'policies');
  return listFiles(dir, '.yaml').map(f => {
    const base = f.replace(/-policy\.yaml$/, '');
    const filePath = path.join(dir, f);
    return {
      id: `pol-${base}`,
      name: kebabToTitle(base) + ' Policy',
      fileName: f,
      version: scanYamlVersion(filePath),
      description: scanYamlDescription(filePath),
      fileTree: singleFileTree(f, 'policies'),
    };
  });
}

function scanPersonas(): ArtifactInfo[] {
  const dir = path.join(DEMERZEL_ROOT, 'personas');
  return listFiles(dir, '.yaml').map(f => {
    const base = f.replace(/\.persona\.yaml$/, '');
    const filePath = path.join(dir, f);
    return {
      id: `per-${base}`,
      name: kebabToTitle(base),
      fileName: f,
      version: scanYamlVersion(filePath),
      description: scanYamlDescription(filePath),
      fileTree: singleFileTree(f, 'personas'),
    };
  });
}

function scanSchemas(): ArtifactInfo[] {
  const dir = path.join(DEMERZEL_ROOT, 'schemas');
  return listFiles(dir, '.json')
    .filter(f => f.endsWith('.schema.json'))
    .map(f => {
      const base = f.replace(/\.schema\.json$/, '');
      return {
        id: `sch-${base}`,
        name: kebabToTitle(base) + ' Schema',
        fileName: f,
        fileTree: singleFileTree(f, 'schemas'),
      };
    });
}

function scanTests(): ArtifactInfo[] {
  const dir = path.join(DEMERZEL_ROOT, 'tests/behavioral');
  return listFiles(dir, '.md')
    .filter(f => f.endsWith('-cases.md'))
    .map(f => {
      const base = f.replace(/-cases\.md$/, '');
      return {
        id: `test-${base}`,
        name: kebabToTitle(base) + ' Tests',
        fileName: f,
        fileTree: singleFileTree(f, 'tests/behavioral'),
      };
    });
}

function scanDepartments(): ArtifactInfo[] {
  const dir = path.join(DEMERZEL_ROOT, 'state/streeling/departments');
  return listFiles(dir, '.json')
    .filter(f => f.endsWith('.department.json'))
    .map(f => {
      const base = f.replace(/\.department\.json$/, '');
      const data = readJsonSafe(path.join(dir, f));
      // Check if a department subdirectory exists for recursive tree
      const deptDir = path.join(dir, base);
      const hasDeptDir = fs.existsSync(deptDir) && fs.statSync(deptDir).isDirectory();
      const fileTree: FileTreeNode[] = hasDeptDir
        ? buildFileTree(deptDir, DEMERZEL_ROOT)
        : singleFileTree(f, 'state/streeling/departments');
      return {
        id: `dept-${base}`,
        name: (data?.full_name as string) || kebabToTitle(base) + ' Department',
        fileName: f,
        description: typeof data?.domain === 'string' ? data.domain.slice(0, 200) : undefined,
        fileTree,
      };
    });
}

// ---------------------------------------------------------------------------
// Evolution & belief data for health scoring
// ---------------------------------------------------------------------------
interface EvolutionMetrics {
  artifactPath: string;
  citationCount: number;
  complianceRate: number;
  recommendation: string;
  confidence: number;
}

function scanEvolution(): Map<string, EvolutionMetrics> {
  const dir = path.join(DEMERZEL_ROOT, 'state/evolution');
  const map = new Map<string, EvolutionMetrics>();
  for (const f of listFiles(dir, '.json')) {
    if (!f.endsWith('.evolution.json')) continue;
    const data = readJsonSafe(path.join(dir, f));
    if (!data) continue;
    const metrics = data.metrics as Record<string, unknown> | undefined;
    const assessment = data.assessment as Record<string, unknown> | undefined;
    const effectiveness = assessment?.effectiveness as Record<string, unknown> | undefined;
    const artifactPath = data.artifact as string;
    if (artifactPath) {
      map.set(artifactPath, {
        artifactPath,
        citationCount: (metrics?.citation_count as number) ?? 0,
        complianceRate: (metrics?.compliance_rate as number) ?? 1.0,
        recommendation: (assessment?.recommendation as string) ?? 'maintain',
        confidence: (effectiveness?.confidence as number) ?? 0.7,
      });
    }
  }
  return map;
}

function getHealthScores(): Record<string, { composite_score: number; trend: string }> {
  const data = readJsonSafe(path.join(DEMERZEL_ROOT, 'state/driver/health-scores.json'));
  if (!data) return {};
  return data as Record<string, { composite_score: number; trend: string }>;
}

// ---------------------------------------------------------------------------
// Graph construction
// ---------------------------------------------------------------------------
interface NodeOut {
  id: string;
  name: string;
  type: string;
  description: string;
  color: string;
  repo?: string;
  domain?: string;
  version?: string;
  health?: { resilienceScore: number; lolliCount: number; ergolCount: number };
  fileTree?: FileTreeNode[];
}

interface EdgeOut {
  id: string;
  source: string;
  target: string;
  type: string;
  label?: string;
  weight?: number;
}

const NODE_COLORS: Record<string, string> = {
  constitution: '#FFD700',
  policy: '#4CB050',
  persona: '#C678DD',
  pipeline: '#58A6FF',
  department: '#E5C07B',
  schema: '#7289DA',
  test: '#E06C75',
  ixql: '#F0883E',
};

function main() {
  console.log(`Scanning Demerzel at: ${DEMERZEL_ROOT}`);

  if (!fs.existsSync(DEMERZEL_ROOT)) {
    console.error('ERROR: Demerzel root not found. Skipping generation.');
    process.exit(1);
  }

  const evolution = scanEvolution();
  const healthScores = getHealthScores();
  const demerzelR = healthScores.demerzel?.composite_score ?? 0.82;

  const constitutions = scanConstitutions();
  const policies = scanPolicies();
  const personas = scanPersonas();
  const schemas = scanSchemas();
  const tests = scanTests();
  const departments = scanDepartments();

  // Track which artifacts have evolution data (ERGOL) vs not (potential LOLLI)
  const allArtifactPaths = new Set<string>();
  policies.forEach(p => allArtifactPaths.add(`policies/${p.fileName}`));
  constitutions.forEach(c => allArtifactPaths.add(`constitutions/${c.fileName}`));

  const _evolvedPaths = new Set(evolution.keys());

  // Compute ERGOL: artifacts with evolution tracking + positive citations
  let totalErgol = 0;
  let totalLolli = 0;

  // Count policies with evolution tracking as ERGOL, without as potential LOLLI
  for (const policyPath of allArtifactPaths) {
    const evo = evolution.get(policyPath);
    if (evo && evo.citationCount > 0) {
      totalErgol++;
    }
  }

  // All constitutions, personas, schemas, tests, departments count as ERGOL
  // (they exist and are consumed by the framework)
  totalErgol += constitutions.length;
  totalErgol += personas.length;
  totalErgol += tests.length;
  totalErgol += departments.length;
  totalErgol += schemas.length;

  // LOLLI: policies without evolution data AND no obvious consumer
  // Heuristic: policies added recently without citations
  const policiesWithoutEvolution = policies.filter(
    p => !evolution.has(`policies/${p.fileName}`)
  );
  // Not all uncited policies are LOLLI — many are new. Use a conservative count.
  // Policies with evolution showing deprecation_candidate are LOLLI.
  for (const [, evo] of evolution) {
    if (evo.recommendation === 'deprecate') totalLolli++;
  }
  // Also count stale policies (no evolution, old) as mild LOLLI signal
  totalLolli += Math.max(0, Math.floor(policiesWithoutEvolution.length * 0.1));

  // Build nodes
  const nodes: NodeOut[] = [];
  const edges: EdgeOut[] = [];

  // --- Constitutions ---
  for (const c of constitutions) {
    const evo = evolution.get(`constitutions/${c.fileName}`);
    nodes.push({
      id: c.id,
      name: c.name,
      type: 'constitution',
      description: c.description || `Constitutional document: ${c.fileName}`,
      color: NODE_COLORS.constitution,
      repo: 'demerzel',
      version: '1.0.0',
      health: {
        resilienceScore: evo ? evo.confidence : 0.95,
        lolliCount: 0,
        ergolCount: evo?.citationCount ?? 5,
      },
      fileTree: c.fileTree,
    });
  }

  // --- Policies ---
  for (const p of policies) {
    const evo = evolution.get(`policies/${p.fileName}`);
    const isLolli = evo?.recommendation === 'deprecate';
    nodes.push({
      id: p.id,
      name: p.name,
      type: 'policy',
      description: p.description || `Governance policy: ${p.fileName}`,
      color: NODE_COLORS.policy,
      repo: 'demerzel',
      version: p.version || '1.0.0',
      health: {
        resilienceScore: evo ? evo.complianceRate * evo.confidence : 0.7,
        lolliCount: isLolli ? 1 : 0,
        ergolCount: evo?.citationCount ?? 1,
      },
      fileTree: p.fileTree,
    });
  }

  // --- Personas ---
  for (const p of personas) {
    nodes.push({
      id: p.id,
      name: p.name,
      type: 'persona',
      description: p.description || `Agent persona: ${p.fileName}`,
      color: NODE_COLORS.persona,
      repo: 'demerzel',
      version: p.version || '1.0.0',
      health: {
        resilienceScore: 0.85,
        lolliCount: 0,
        ergolCount: 3,
      },
      fileTree: p.fileTree,
    });
  }

  // --- Schemas (top 10 by relevance to keep graph readable) ---
  const topSchemas = schemas.slice(0, 12);
  for (const s of topSchemas) {
    nodes.push({
      id: s.id,
      name: s.name,
      type: 'schema',
      description: `JSON Schema: ${s.fileName}`,
      color: NODE_COLORS.schema,
      repo: 'demerzel',
      health: {
        resilienceScore: 0.9,
        lolliCount: 0,
        ergolCount: 2,
      },
      fileTree: s.fileTree,
    });
  }

  // --- Tests (top 15 most relevant) ---
  const keyTests = tests.filter(t =>
    ['asimov-law', 'demerzel', 'kaizen', 'proto-conscience', 'seldon',
     'alignment', 'reconnaissance', 'governance-process', 'driver',
     'skeptical-auditor', 'completeness-instinct', 'weakness-prober',
     'chaos-engineering', 'render-critic', 'streeling'].some(k => t.id.includes(k))
  ).slice(0, 15);
  // Add remaining if we have fewer than 15
  if (keyTests.length < 15) {
    for (const t of tests) {
      if (!keyTests.find(k => k.id === t.id) && keyTests.length < 15) {
        keyTests.push(t);
      }
    }
  }
  for (const t of keyTests) {
    nodes.push({
      id: t.id,
      name: t.name,
      type: 'test',
      description: `Behavioral test suite: ${t.fileName}`,
      color: NODE_COLORS.test,
      repo: 'demerzel',
      health: {
        resilienceScore: 0.85,
        lolliCount: 0,
        ergolCount: 5,
      },
      fileTree: t.fileTree,
    });
  }

  // --- Departments ---
  for (const d of departments) {
    nodes.push({
      id: d.id,
      name: d.name,
      type: 'department',
      description: d.description || `Streeling University department`,
      color: NODE_COLORS.department,
      repo: 'demerzel',
      domain: 'streeling',
      health: {
        resilienceScore: 0.8,
        lolliCount: 0,
        ergolCount: 4,
      },
      fileTree: d.fileTree,
    });
  }

  // --- Pipelines (cross-repo, kept from sample since ix/tars state isn't local) ---
  const pipelines = [
    { id: 'pipe-belief', name: 'Belief Update Pipeline', desc: 'Updates tetravalent belief states from evidence', repo: 'ix' },
    { id: 'pipe-markov', name: 'Markov Prediction Pipeline', desc: 'Memristive Markov chain state predictions', repo: 'ix' },
    { id: 'pipe-conscience', name: 'Conscience Cycle Pipeline', desc: 'Ethical tension detection and resolution cycle', repo: 'tars' },
    { id: 'pipe-resilience', name: 'Resilience Scoring Pipeline', desc: 'Computes governance health and resilience scores', repo: 'ix' },
    { id: 'pipe-staleness', name: 'Staleness Scanner Pipeline', desc: 'Scans artifacts for staleness and decay', repo: 'ix' },
  ];
  for (const p of pipelines) {
    const repoR = healthScores[p.repo]?.composite_score ?? 0.75;
    nodes.push({
      id: p.id,
      name: p.name,
      type: 'pipeline',
      description: p.desc,
      color: NODE_COLORS.pipeline,
      repo: p.repo,
      health: {
        resilienceScore: repoR,
        lolliCount: 0,
        ergolCount: 3,
      },
    });
  }

  // --- IxQL queries ---
  const ixqls = [
    { id: 'ixql-belief', name: 'belief-update.ixql', desc: 'IxQL query for tetravalent belief state updates' },
    { id: 'ixql-resilience', name: 'resilience-score.ixql', desc: 'IxQL query for computing resilience metrics' },
  ];
  for (const q of ixqls) {
    nodes.push({
      id: q.id,
      name: q.name,
      type: 'ixql',
      description: q.desc,
      color: NODE_COLORS.ixql,
      repo: 'ix',
      health: { resilienceScore: 0.75, lolliCount: 0, ergolCount: 2 },
    });
  }

  // =========================================================================
  // Edges
  // =========================================================================
  const nodeIds = new Set(nodes.map(n => n.id));
  let edgeIdx = 0;
  const addEdge = (source: string, target: string, type: string, weight = 0.7, label?: string) => {
    if (nodeIds.has(source) && nodeIds.has(target)) {
      edges.push({
        id: `e-${edgeIdx++}`,
        source,
        target,
        type,
        weight,
        ...(label ? { label } : {}),
      });
    }
  };

  // Constitutional hierarchy: asimov -> default, mandate, harm
  addEdge('const-asimov', 'const-default', 'constitutional-hierarchy', 1.0);
  addEdge('const-asimov', 'const-demerzel-mandate', 'constitutional-hierarchy', 1.0);
  addEdge('const-asimov', 'const-harm-taxonomy', 'constitutional-hierarchy', 0.8);

  // Constitution -> all policies
  for (const p of policies) {
    addEdge('const-default', p.id, 'constitutional-hierarchy', 0.6);
  }

  // Policy -> matching persona (by name heuristic)
  const _personaMap = new Map(personas.map(p => [p.id.replace('per-', ''), p.id]));
  const policyPersonaLinks: Array<[string, string]> = [
    ['alignment', 'demerzel'],
    ['alignment', 'skeptical-auditor'],
    ['kaizen', 'kaizen-optimizer'],
    ['seldon-plan', 'seldon'],
    ['proto-conscience', 'demerzel'],
    ['autonomous-loop', 'demerzel'],
    ['reconnaissance', 'demerzel'],
    ['governance-audit', 'skeptical-auditor'],
    ['weakness-prober', 'skeptical-auditor'],
    ['scientific-objectivity', 'critical-theorist'],
    ['rollback', 'recovery-agent'],
    ['self-modification', 'reflective-architect'],
    ['staleness-detection', 'validator-reflector'],
    ['completeness-instinct', 'system-integrator'],
    ['continuous-learning', 'communal-steward'],
    ['governance-experimentation', 'seldon'],
  ];
  for (const [pol, per] of policyPersonaLinks) {
    addEdge(`pol-${pol}`, `per-${per}`, 'policy-persona', 0.8);
  }

  // Pipeline connections
  addEdge('pol-alignment', 'pipe-belief', 'pipeline-flow', 0.8);
  addEdge('pol-seldon-plan', 'pipe-markov', 'pipeline-flow', 0.7);
  addEdge('pol-proto-conscience', 'pipe-conscience', 'pipeline-flow', 0.8);
  addEdge('pol-staleness-detection', 'pipe-resilience', 'pipeline-flow', 0.6);
  addEdge('pol-staleness-detection', 'pipe-staleness', 'pipeline-flow', 0.5);

  // Cross-repo
  addEdge('pipe-belief', 'ixql-belief', 'cross-repo', 0.6);
  addEdge('pipe-resilience', 'ixql-resilience', 'cross-repo', 0.5);
  addEdge('per-demerzel', 'pipe-belief', 'cross-repo', 0.7);
  addEdge('per-seldon', 'pipe-markov', 'cross-repo', 0.6);

  // Streeling -> departments
  for (const d of departments) {
    addEdge('pol-streeling', d.id, 'policy-persona', 0.5);
  }

  // Test connections — link tests to matching policies/personas
  const testLinks: Array<[string, string]> = [
    ['test-asimov-law', 'const-asimov'],
    ['test-demerzel', 'per-demerzel'],
    ['test-kaizen', 'pol-kaizen'],
    ['test-proto-conscience', 'pol-proto-conscience'],
    ['test-seldon', 'per-seldon'],
    ['test-reconnaissance', 'pol-reconnaissance'],
    ['test-governance-process', 'pol-governance-process'],
    ['test-driver', 'pol-autonomous-loop'],
    ['test-skeptical-auditor', 'per-skeptical-auditor'],
    ['test-completeness-instinct', 'pol-completeness-instinct'],
    ['test-weakness-prober', 'pol-weakness-prober'],
    ['test-chaos-engineering', 'pol-chaos-test'],
    ['test-streeling', 'pol-streeling'],
  ];
  for (const [testId, targetId] of testLinks) {
    addEdge(testId, targetId, 'policy-persona', 0.7);
  }

  // Schema connections
  addEdge('per-demerzel', 'sch-persona', 'policy-persona', 0.5);
  addEdge('pipe-belief', 'sch-fuzzy-belief', 'pipeline-flow', 0.6);
  addEdge('pipe-conscience', 'sch-conscience-signal', 'pipeline-flow', 0.5);
  addEdge('pipe-resilience', 'sch-resilience-metric', 'pipeline-flow', 0.5);

  // LOLLI edges — find stale/deprecated connections
  // Use evolution data to identify deprecated artifacts
  for (const [artPath, evo] of evolution) {
    if (evo.recommendation === 'deprecate') {
      const base = artPath.split('/').pop()?.replace(/-policy\.yaml$/, '').replace(/\..*$/, '');
      if (base) {
        addEdge(`pol-${base}`, 'pipe-staleness', 'lolli', 0.3, 'deprecated binding');
      }
    }
  }

  // Compute global health
  const globalHealth = {
    resilienceScore: demerzelR,
    lolliCount: totalLolli,
    ergolCount: totalErgol,
    staleness: 0.08,
  };

  // =========================================================================
  // Output
  // =========================================================================
  const output = `// src/components/PrimeRadiant/liveData.ts
// AUTO-GENERATED by generate-live-data.ts — do not edit manually
// Generated: ${new Date().toISOString()}
// Source: Demerzel state/ directory scan
//
// Artifact counts:
//   Constitutions: ${constitutions.length}
//   Policies: ${policies.length}
//   Personas: ${personas.length}
//   Schemas: ${topSchemas.length} (of ${schemas.length})
//   Tests: ${keyTests.length} (of ${tests.length})
//   Departments: ${departments.length}
//   Pipelines: ${pipelines.length}
//   IxQL: ${ixqls.length}
//   Total nodes: ${nodes.length}
//   Total edges: ${edges.length}
//   ERGOL: ${totalErgol} | LOLLI: ${totalLolli} | R: ${demerzelR}

import type { GovernanceGraph } from './types';

export const LIVE_GOVERNANCE_GRAPH: GovernanceGraph = ${JSON.stringify(
    { timestamp: new Date().toISOString(), globalHealth, nodes, edges },
    null,
    2,
  )};
`;

  const outPath = path.join(__dirname, 'liveData.ts');
  fs.writeFileSync(outPath, output, 'utf-8');
  console.log(`Written ${outPath}`);
  console.log(`  Nodes: ${nodes.length}  Edges: ${edges.length}`);
  console.log(`  ERGOL: ${totalErgol}  LOLLI: ${totalLolli}  R: ${demerzelR}`);
}

main();
