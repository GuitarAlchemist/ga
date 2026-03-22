import type { Domain, NodeStatus, RoadmapNode, StatItem } from './types';

// ---------------------------------------------------------------------------
// Color → Domain mapping helper
// ---------------------------------------------------------------------------
const COLOR_TO_DOMAIN: Record<string, Domain> = {
  '#f0883e': 'core',
  '#4cb050': 'gov',
  '#7289da': 'music',
  '#e06c75': 'sci',
  '#c678dd': 'human',
  '#56b6c2': 'infra',
  '#e5c07b': 'meta',
  // dim/horizon items use infra as fallback
  '#8b949e': 'infra',
  // blue accent — infra (GitHub Pages, Discord) or music contextually;
  // resolved per-node below via explicit domain override
  '#58a6ff': 'infra',
};

function colorToDomain(hex: string): Domain {
  return COLOR_TO_DOMAIN[hex.toLowerCase()] ?? 'core';
}

// ---------------------------------------------------------------------------
// Raw tree shape (mirrors the JS source before transformation)
// ---------------------------------------------------------------------------
interface RawNode {
  name: string;
  color: string;
  desc: string;
  sub?: string;
  url?: string;
  status?: NodeStatus;
  domain?: Domain;          // explicit override when color is ambiguous
  grammarUrl?: string;
  children?: RawNode[];
}

// ---------------------------------------------------------------------------
// The raw hierarchical data ported from gh-pages/demos/roadmap/index.html
// ---------------------------------------------------------------------------
const RAW_TREE: RawNode = {
  name: 'GuitarAlchemist', color: '#f0883e',
  desc: 'AI-native tools for music, ML, and agent governance.',
  url: 'https://github.com/GuitarAlchemist',
  children: [
    {
      name: 'Constitution', color: '#4cb050', sub: '11 Articles',
      desc: 'Asimov Laws (0-5) + operational ethics (1-11).',
      url: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/constitutions/default.constitution.md',
      children: [
        {
          name: 'Asimov Laws', color: '#4cb050', sub: 'Art 0-5',
          desc: 'Zeroth Law, Three Laws, separation, invariance.',
          children: [
            { name: 'Zeroth Law', color: '#4cb050', desc: 'Protect humanity and ecosystem.' },
            { name: 'First Law',  color: '#4cb050', desc: 'Protect individual humans.' },
            { name: 'Second Law', color: '#4cb050', desc: 'Obey human authority.' },
            { name: 'Third Law',  color: '#4cb050', desc: 'Self-preservation (lowest).' },
          ],
        },
        {
          name: 'Operational Ethics', color: '#4cb050', sub: 'Art 1-11',
          desc: 'Truthfulness, transparency, reversibility...',
          children: [
            { name: 'Truthfulness',    color: '#4cb050', desc: 'Do not fabricate.' },
            { name: 'Transparency',    color: '#4cb050', desc: 'Explain reasoning.' },
            { name: 'Reversibility',   color: '#4cb050', desc: 'Prefer reversible actions.' },
            { name: 'Proportionality', color: '#4cb050', desc: 'Match scope to request.' },
            { name: 'Escalation',      color: '#4cb050', desc: 'Escalate when uncertain.' },
            { name: 'Auditability',    color: '#4cb050', desc: 'Maintain logs and traces.' },
            { name: 'Bounded Autonomy',color: '#4cb050', desc: 'Operate within bounds.' },
          ],
        },
        {
          name: 'Fuzzy Enum/DU', color: '#4cb050', sub: 'SPEC',
          desc: 'FuzzyEnum<T>, FuzzyDU<T,P>, FuzzyBuilder CE.',
          url: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/docs/superpowers/specs/2026-03-22-fuzzy-enum-du-design.md',
        },
        {
          name: 'BS Detector', color: '#4cb050', sub: 'v2',
          desc: '10-domain grammar + decoder pipeline.',
          url: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/gov-bs-generators.ebnf',
        },
        { name: 'Staleness',   color: '#4cb050', sub: 'POLICY',  desc: 'Per-category freshness thresholds.' },
        { name: 'Blind Spots', color: '#4cb050', sub: 'GRAMMAR', desc: 'Coverage, cognitive, meta blind spots.' },
        { name: 'Alignment',   color: '#4cb050', desc: '≥0.9 proceed, ≥0.7 note, ≥0.5 confirm, <0.3 stop.' },
      ],
    },
    {
      name: 'Tetravalent', color: '#f0883e', sub: 'T/F/U/C',
      desc: 'Four-valued logic: True, False, Unknown, Contradictory.',
      url: 'https://github.com/GuitarAlchemist/Demerzel/tree/master/logic',
      children: [
        { name: 'Fuzzy Ops',   color: '#f0883e', desc: 'AND, OR, NOT, renormalize, sharpen.' },
        { name: 'Propagation', color: '#f0883e', desc: 'Multiplicative, Zadeh, Bayesian, Custom.' },
        {
          name: 'K-Theory', color: '#8b949e', sub: 'HORIZON',
          desc: 'Grothendieck bundles. Future work.',
          status: 'horizon',
        },
      ],
    },
    {
      name: 'Streeling Univ.', color: '#7289da', sub: '14 Depts',
      desc: '14 departments. Research cycles produce courses.',
      url: 'https://github.com/GuitarAlchemist/Demerzel/tree/master/state/streeling',
      grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/tree/master/grammars',
      children: [
        {
          name: 'Music', color: '#7289da',
          desc: 'Scales, chords, progressions, keys.',
          grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/music-theory.ebnf',
          children: [
            { name: 'MUS-001', color: '#7289da', desc: 'What Is a Chord — beginner.' },
          ],
        },
        {
          name: 'Guitar Studies', color: '#7289da',
          desc: 'Fretboard, CAGED, technique.',
          grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/music-guitar-technique.ebnf',
          children: [
            { name: 'GTR-001', color: '#7289da', desc: 'The Fretboard Map.' },
            { name: 'GTR-002', color: '#7289da', desc: 'CAGED Geometry — research-produced.' },
            { name: 'Satriani Grammar', color: '#7289da', sub: 'ADVANCED', desc: 'Legato, tapping, whammy.' },
          ],
        },
        {
          name: 'Audio Eng.', color: '#e06c75', sub: 'NEW',
          desc: 'Recording, mixing, mastering. Metabuild-bootstrapped.',
          status: 'new',
          grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/sci-audio-eng.ebnf',
          children: [
            { name: 'AUD-001', color: '#e06c75', desc: 'EQ/Compression Order — research-produced.' },
          ],
        },
        { name: 'Musicology',   color: '#7289da', desc: 'How Music Evolves — cultural analysis.', grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/music-musicology.ebnf' },
        { name: 'Physics',      color: '#e06c75', desc: 'Acoustics, vibration, modeling.', grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/sci-acoustics.ebnf' },
        { name: 'Mathematics',  color: '#e06c75', desc: 'Proofs, algebra, topology.', grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/sci-math-proof.ebnf' },
        { name: 'Comp. Science',color: '#e06c75', desc: 'Algorithms, ML, DSP.', grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/sci-algorithms.ebnf' },
        { name: 'Philosophy',   color: '#c678dd', desc: 'Ethics, dialectic, epistemology.', grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/human-philosophy.ebnf' },
        { name: 'Cog. Science', color: '#c678dd', desc: 'Biases, heuristics, decision making.', grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/human-cog-sci.ebnf' },
        { name: 'Futurology',   color: '#c678dd', desc: 'Signals, scenarios, horizons.', grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/human-futurology.ebnf' },
        { name: 'Psychohistory',color: '#c678dd', desc: 'Statistical prediction, crises.', grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/human-psychohistory.ebnf' },
        { name: 'Academy',      color: '#7289da', desc: 'Beginner onboarding.' },
        { name: 'World Music',  color: '#7289da', desc: '10 traditions, 6 languages.' },
        { name: 'Product Mgmt', color: '#56b6c2', desc: 'Shipping vs talking.', grammarUrl: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/grammars/gov-product-mgmt.ebnf' },
        {
          name: 'Research Cycle', color: '#7289da', sub: '2 DONE',
          desc: 'CAGED + EQ/compression validated.',
          url: 'https://github.com/GuitarAlchemist/Demerzel/tree/master/state/streeling/research-cycles',
        },
        {
          name: '80 Courses', color: '#58a6ff', sub: '6 LANGS',
          desc: '15 EN + 65 translations.',
          domain: 'music',
        },
      ],
    },
    {
      name: 'Driver', color: '#56b6c2', sub: '8-Phase',
      desc: 'WAKE→RECON→PLAN→EXECUTE→VERIFY→COMPOUND→PERSIST→SLEEP.',
      url: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/.claude/skills/demerzel-drive/SKILL.md',
      children: [
        {
          name: 'RECON', color: '#56b6c2',
          desc: 'Three-tier reconnaissance across repos.',
          children: [
            { name: 'Tier 1: Self',     color: '#56b6c2', desc: 'Schema validation, test coverage.' },
            { name: 'Tier 2: Repos',    color: '#56b6c2', desc: 'Repo state, CI, submodules.' },
            { name: 'Tier 3: Knowledge',color: '#56b6c2', desc: 'Assumptions, confidence gaps.' },
            { name: 'Link Verify',      color: '#56b6c2', desc: 'README link verification.' },
          ],
        },
        {
          name: 'COMPOUND', color: '#56b6c2',
          desc: 'Meta-compounding + README sync.',
          children: [
            { name: 'Evolve',     color: '#56b6c2', desc: 'Promote/demote governance artifacts.' },
            { name: 'README Sync',color: '#56b6c2', desc: 'Auto-sync artifact counts.' },
            { name: 'Knowledge',  color: '#56b6c2', desc: 'Package via /seldon deliver.' },
          ],
        },
        {
          name: 'Discord Bot', color: '#56b6c2', sub: 'LIVE',
          desc: 'Node.js bot (discord.js + Anthropic).',
          url: 'https://github.com/GuitarAlchemist/demerzel-bot',
        },
        { name: 'Reports', color: '#56b6c2', sub: 'SKILL', desc: '/demerzel report — 6 sections, 3 formats.' },
        {
          name: 'GitHub Pages', color: '#58a6ff', sub: '6 VIZ',
          desc: 'Interactive D3.js visualizations.',
          url: 'https://guitaralchemist.github.io',
          domain: 'infra',
        },
      ],
    },
    {
      name: 'Meta-Grammar', color: '#e5c07b', sub: '19 Living',
      desc: '19 EBNF grammars. Living evolution policy.',
      url: 'https://github.com/GuitarAlchemist/Demerzel/tree/master/grammars',
      children: [
        {
          name: 'MetaBuild', color: '#e5c07b', sub: 'FACTORY',
          desc: 'Factory of factories — one command, full department.',
          url: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/.claude/skills/demerzel-metabuild/SKILL.md',
          children: [
            { name: 'Dept Factory',    color: '#e5c07b', desc: 'Grammar + dept + weights + tests.' },
            { name: 'Skill Factory',   color: '#e5c07b', desc: 'Skill + schema + tests.' },
            { name: 'Grammar Factory', color: '#e5c07b', desc: 'Grammar + evolution hooks.' },
            { name: 'Pipeline Factory',color: '#e5c07b', desc: 'MOG pipeline + skill.' },
            { name: 'Repo Factory',    color: '#e5c07b', desc: 'GitHub repo + governance.' },
          ],
        },
        {
          name: 'MetaFix', color: '#e5c07b', sub: '5 LEVELS',
          desc: 'Instance → batch → detect → prevent → system.',
          url: 'https://github.com/GuitarAlchemist/Demerzel/blob/master/.claude/skills/demerzel-metafix/SKILL.md',
        },
        { name: 'MOG', color: '#e06c75', sub: '42 TASKS', desc: 'MCP Orchestration Grammar.' },
        {
          name: 'Grammar Prefixes', color: '#e5c07b',
          desc: 'core (3), music (4), sci (5), gov (3), human (4).',
          children: [
            { name: 'core- (3)',  color: '#f0883e', desc: 'meta-grammar, scientific-method, state-machines.' },
            { name: 'music- (4)', color: '#7289da', desc: 'theory, guitar-technique, musicology, satriani.' },
            { name: 'sci- (5)',   color: '#e06c75', desc: 'acoustics, algorithms, math-proof, ml-pipelines, audio-eng.' },
            { name: 'gov- (3)',   color: '#4cb050', desc: 'blind-spot, bs-generators, product-mgmt.' },
            { name: 'human- (4)', color: '#c678dd', desc: 'cog-sci, futurology, philosophy, psychohistory.' },
          ],
        },
        {
          name: 'External', color: '#8b949e', sub: '15 REFS',
          desc: 'External grammars for distillation.',
          status: 'horizon',
        },
      ],
    },
    {
      name: 'Conscience', color: '#4cb050', sub: 'Maturing',
      desc: 'Signals, regrets, patterns. Nascent → maturing.',
      url: 'https://github.com/GuitarAlchemist/Demerzel/tree/master/state/conscience',
      children: [
        {
          name: 'Signals', color: '#4cb050', sub: '5 FIRED',
          desc: '4 fully resolved, 1 substantially addressed.',
          children: [
            { name: 'Harm Proximity',  color: '#4cb050', desc: 'ga ungoverned agents — resolved.' },
            { name: 'Silence',         color: '#4cb050', desc: 'No consumer beliefs — addressed.' },
            { name: 'Stale Action ×2', color: '#4cb050', desc: 'Stale submodules, stale articles — resolved.' },
            { name: 'Remediation',     color: '#4cb050', desc: 'In-flight remediation — resolved.' },
          ],
        },
        {
          name: 'Beliefs', color: '#4cb050', sub: '3 ACTIVE',
          desc: 'Integrity 0.98, tests 0.80, integration 0.90.',
          children: [
            { name: 'Integrity',    color: '#4cb050', sub: 'T 0.98', desc: 'Framework structurally complete.' },
            { name: 'Test Coverage',color: '#4cb050', sub: 'T 0.80', desc: 'Core personas tested. Expanded set pending.' },
            { name: 'Integration',  color: '#4cb050', sub: 'T 0.90', desc: 'All 3 repos governed on main.' },
            { name: '4 Archived',   color: '#8b949e', desc: 'Superseded 2026-03-17 beliefs.', status: 'horizon' },
          ],
        },
        { name: 'Patterns',       color: '#c678dd', sub: '5 FOUND', desc: '4 anti-patterns + 1 positive.' },
        { name: 'Cont. Learning', color: '#c678dd', sub: 'POLICY',  desc: 'Observe → extract → promote.' },
        {
          name: 'ML Pipelines', color: '#e06c75', sub: 'ix',
          desc: 'Rust — 83 tests.',
          url: 'https://github.com/GuitarAlchemist/ix',
        },
        {
          name: 'tars MCP', color: '#e06c75', sub: '151 TOOLS',
          desc: 'F# reasoning engine.',
          url: 'https://github.com/GuitarAlchemist/tars',
        },
        { name: 'Context Budget', color: '#c678dd', sub: 'ECC', desc: 'Token overhead audit.' },
      ],
    },
  ],
};

// ---------------------------------------------------------------------------
// Transform raw node → RoadmapNode (without IDs; IDs assigned by assignIds)
// ---------------------------------------------------------------------------
function transformNode(raw: RawNode): RoadmapNode {
  const domain: Domain = raw.domain ?? colorToDomain(raw.color);
  const node: RoadmapNode = {
    id: '',           // filled in by assignIds
    name: raw.name,
    color: raw.color,
    domain,
    description: raw.desc,
  };
  if (raw.sub)       node.sub = raw.sub;
  if (raw.url)       node.url = raw.url;
  if (raw.grammarUrl) node.grammarUrl = raw.grammarUrl;
  if (raw.status)    node.status = raw.status;
  if (raw.children)  node.children = raw.children.map(transformNode);
  return node;
}

// ---------------------------------------------------------------------------
// ID assignment — kebab-case, unique within siblings via index suffix
// ---------------------------------------------------------------------------
function toKebab(name: string): string {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
}

export function assignIds(
  node: RoadmapNode,
  parentId = '',
  depth = 0,
): RoadmapNode {
  const slug = toKebab(node.name);
  node.id = parentId ? `${parentId}--${slug}` : slug;
  node._depth = depth;
  if (node.children) {
    // Deduplicate IDs among siblings
    const seen = new Map<string, number>();
    node.children.forEach((child) => {
      const base = toKebab(child.name);
      const count = seen.get(base) ?? 0;
      seen.set(base, count + 1);
      const childSlug = count === 0 ? base : `${base}-${count}`;
      const childId = `${node.id}--${childSlug}`;
      child.id = childId;
      child._depth = depth + 1;
      assignIds(child, node.id, depth + 1);
      // assignIds overwrites id; restore correct one when there are dups
      if (count > 0) {
        child.id = childId;
      }
    });
  }
  return node;
}

// ---------------------------------------------------------------------------
// Build the exported tree (transform + assign IDs)
// ---------------------------------------------------------------------------
const _rawTransformed = transformNode(RAW_TREE);
export const ROADMAP_TREE: RoadmapNode = assignIds(_rawTransformed);

// ---------------------------------------------------------------------------
// Parent map
// ---------------------------------------------------------------------------
export const parentMap = new WeakMap<RoadmapNode, RoadmapNode | null>();

export function buildParentMap(
  node: RoadmapNode,
  parent: RoadmapNode | null = null,
): void {
  parentMap.set(node, parent);
  if (node.children) {
    node.children.forEach((child) => buildParentMap(child, node));
  }
}

// Initialise on module load
buildParentMap(ROADMAP_TREE);

// ---------------------------------------------------------------------------
// Utility: flatten tree
// ---------------------------------------------------------------------------
export function flattenTree(node: RoadmapNode): RoadmapNode[] {
  const result: RoadmapNode[] = [node];
  if (node.children) {
    node.children.forEach((child) => result.push(...flattenTree(child)));
  }
  return result;
}

// ---------------------------------------------------------------------------
// Utility: get ancestors (root → node path, excluding the node itself)
// ---------------------------------------------------------------------------
export function getAncestors(node: RoadmapNode): RoadmapNode[] {
  const ancestors: RoadmapNode[] = [];
  let current: RoadmapNode | null | undefined = parentMap.get(node);
  while (current != null) {
    ancestors.unshift(current);
    current = parentMap.get(current);
  }
  return ancestors;
}

// ---------------------------------------------------------------------------
// Utility: search tree — returns Set of matching node IDs (fuzzy, with ancestors)
// ---------------------------------------------------------------------------
export function searchTree(root: RoadmapNode, query: string): Set<string> {
  const q = query.trim().toLowerCase();
  if (!q) return new Set();

  const matched = new Set<string>();
  const allNodes = flattenTree(root);

  allNodes.forEach((node) => {
    const haystack =
      `${node.name} ${node.description} ${node.sub ?? ''}`.toLowerCase();
    if (haystack.includes(q)) {
      matched.add(node.id);
      // Also include all ancestors so the path stays visible
      getAncestors(node).forEach((anc) => matched.add(anc.id));
    }
  });

  return matched;
}

// ---------------------------------------------------------------------------
// Stats
// ---------------------------------------------------------------------------
export const STATS: StatItem[] = [
  {
    label: 'Repos',
    value: '3',
    url: 'https://github.com/GuitarAlchemist',
  },
  {
    label: 'Policies',
    value: '24',
    url: 'https://github.com/GuitarAlchemist/Demerzel/tree/master/policies',
  },
  {
    label: 'Personas',
    value: '14',
    url: 'https://github.com/GuitarAlchemist/Demerzel/tree/master/personas',
  },
  {
    label: 'Grammars',
    value: '19',
    url: 'https://github.com/GuitarAlchemist/Demerzel/tree/master/grammars',
  },
  {
    label: 'Courses',
    value: '80',
    url: 'https://github.com/GuitarAlchemist/Demerzel/tree/master/state/streeling',
  },
  {
    label: 'Tests',
    value: '100+',
    url: 'https://github.com/GuitarAlchemist/Demerzel/tree/master/tests/behavioral',
  },
];
