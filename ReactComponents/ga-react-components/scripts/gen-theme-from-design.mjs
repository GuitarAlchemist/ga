#!/usr/bin/env node
// scripts/gen-theme-from-design.mjs
//
// Reads /DESIGN.md at the repo root, parses the YAML frontmatter that
// defines the canonical design tokens (see docs/plans/2026-05-23-arch-
// design-md-adoption-plan.md Phase 2), and emits
// src/theme.ts as a generated MUI theme object.
//
// Run modes:
//   node scripts/gen-theme-from-design.mjs           # write src/theme.ts
//   node scripts/gen-theme-from-design.mjs --check   # exit 1 if out of sync (CI / pre-commit)
//
// The output file is hand-edit-discouraged. The generator stamps a header
// noting the DESIGN.md sha + generated_at timestamp.

import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import YAML from 'yaml';

const __dirname = dirname(fileURLToPath(import.meta.url));
const PROJECT_DIR = resolve(__dirname, '..');
const REPO_ROOT = resolve(PROJECT_DIR, '..', '..');
const DESIGN_MD = resolve(REPO_ROOT, 'DESIGN.md');
const OUT_FILE = resolve(PROJECT_DIR, 'src', 'theme.ts');

const checkOnly = process.argv.includes('--check');

function fail(message) {
    console.error(`✗ gen-theme-from-design: ${message}`);
    process.exit(1);
}

function readFrontmatter(designPath) {
    if (!existsSync(designPath)) fail(`DESIGN.md not found at ${designPath}`);
    const raw = readFileSync(designPath, 'utf-8');
    const m = raw.match(/^---\s*\r?\n([\s\S]*?)\r?\n---/);
    if (!m) fail('DESIGN.md has no YAML frontmatter');
    try {
        return { tokens: YAML.parse(m[1]), rawFrontmatter: m[1] };
    } catch (e) {
        fail(`DESIGN.md frontmatter is not valid YAML: ${e.message}`);
    }
}

function shaShort(text) {
    return createHash('sha256').update(text).digest('hex').slice(0, 8);
}

function emitTheme(tokens, sha) {
    const c = tokens.colors ?? {};
    const t = tokens.typography ?? {};
    const s = tokens.spacing ?? {};
    const r = tokens.rounded ?? {};
    const need = (token, group) => {
        if (!group[token]) fail(`DESIGN.md missing ${group === c ? 'colors' : group === t ? 'typography' : group === s ? 'spacing' : 'rounded'}.${token}`);
        return group[token];
    };

    // Defaults that are reasonable when a token is missing — MUI still requires
    // a 'contrastText'/'light'/'dark' chain on palette colors, so we let MUI
    // derive those from .main rather than hardcoding here.
    const out = `// AUTO-GENERATED from /DESIGN.md (sha ${sha}). DO NOT EDIT.
// Re-run: \`npm run gen:theme\` from ReactComponents/ga-react-components.
// Source of truth: ${'/'.repeat(0)}DESIGN.md at the repo root.
// Plan: docs/plans/2026-05-23-arch-design-md-adoption-plan.md (Phase 2).
//
// Generator: scripts/gen-theme-from-design.mjs
// Generated: ${new Date().toISOString()}

import { createTheme } from '@mui/material/styles';

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: '${need('primary', c)}' },
    secondary: { main: '${need('secondary', c)}' },
    success: { main: '${need('success', c)}' },
    warning: { main: '${need('warning', c)}' },
    error: { main: '${need('error', c)}' },
    background: {
      default: '${need('neutral', c)}',
      paper: '${need('surface', c)}',
    },
    text: {
      primary: '${need('text-primary', c)}',
      secondary: '${need('text-secondary', c)}',
      disabled: '${need('text-disabled', c)}',
    },
    divider: '${c.divider ?? '#e0e0e0'}',
  },
  typography: {
    fontFamily: ${JSON.stringify(t.fontFamily ?? 'Inter, system-ui, sans-serif')},
    h1: { fontSize: ${JSON.stringify(t.h1?.fontSize ?? '28px')}, fontWeight: ${t.h1?.fontWeight ?? 700}, lineHeight: ${t.h1?.lineHeight ?? 1.2} },
    h2: { fontSize: ${JSON.stringify(t.h2?.fontSize ?? '1.5rem')}, fontWeight: ${t.h2?.fontWeight ?? 700}, lineHeight: ${t.h2?.lineHeight ?? 1.25} },
    h3: { fontSize: ${JSON.stringify(t.h3?.fontSize ?? '1.25rem')}, fontWeight: ${t.h3?.fontWeight ?? 600}, lineHeight: ${t.h3?.lineHeight ?? 1.3} },
    h6: { fontSize: ${JSON.stringify(t.h6?.fontSize ?? '1rem')}, fontWeight: ${t.h6?.fontWeight ?? 600}, lineHeight: ${t.h6?.lineHeight ?? 1.4} },
    body1: { fontSize: ${JSON.stringify(t.body?.fontSize ?? '1rem')}, fontWeight: ${t.body?.fontWeight ?? 400}, lineHeight: ${t.body?.lineHeight ?? 1.5} },
    body2: { fontSize: ${JSON.stringify(t['body-sm']?.fontSize ?? '0.875rem')}, fontWeight: ${t['body-sm']?.fontWeight ?? 400}, lineHeight: ${t['body-sm']?.lineHeight ?? 1.5} },
    caption: { fontSize: ${JSON.stringify(t.caption?.fontSize ?? '0.75rem')}, fontWeight: ${t.caption?.fontWeight ?? 400}, lineHeight: ${t.caption?.lineHeight ?? 1.4} },
  },
  shape: {
    // MUI accepts a single borderRadius — use the spec's 'md' as the canonical
    // card radius. Components needing other radii can read from designTokens.
    borderRadius: ${parseInt(String(r.md ?? '6px'), 10)},
  },
  spacing: ${parseInt(String(s.sm ?? '8px'), 10)},  // base unit; sx={{ p: 2 }} => 16px === DESIGN.md spacing.md
});

// Exposed for components that need design tokens MUI's theme doesn't model
// (border, surface-alt, primary-soft, rounded.pill / xl, elevation, etc.).
// Read these as designTokens.border, designTokens.surface-alt, etc.
export const designTokens = ${JSON.stringify(tokens, null, 2)} as const;
`;
    return out;
}

const { rawFrontmatter, tokens } = readFrontmatter(DESIGN_MD);
const sha = shaShort(rawFrontmatter);
const generated = emitTheme(tokens, sha);

if (checkOnly) {
    if (!existsSync(OUT_FILE)) fail(`${OUT_FILE} does not exist — run \`npm run gen:theme\` first.`);
    const existing = readFileSync(OUT_FILE, 'utf-8');
    // Strip the Generated: line which is the only difference between runs
    // when content is identical — otherwise --check would always fail.
    const stripDate = (s) => s.replace(/^\/\/ Generated:.*$/m, '// Generated: <timestamp>');
    if (stripDate(existing) !== stripDate(generated)) {
        fail('src/theme.ts is out of sync with DESIGN.md. Run `npm run gen:theme` and commit the result.');
    }
    console.log('✓ src/theme.ts is in sync with DESIGN.md');
    process.exit(0);
}

writeFileSync(OUT_FILE, generated, 'utf-8');
console.log(`✓ src/theme.ts written (DESIGN.md sha ${sha})`);
