#!/usr/bin/env python3
"""Verify that repo-PATH references in LIVING docs still exist.

Catches doc drift like "Apps/GuitarAlchemistChatbot" or
"Common/GA.Business.Core/AI/AdaptiveDifficultySystem.cs" when the referenced
path no longer exists. Conservative on purpose: only flags strings that are
clearly repo paths (start with a known top dir AND contain '/', or end in a
code-file extension AND contain '/'), so prose namespaces like
`GA.Business.Core` are NOT flagged.

Scope: LIVING docs only (skips frozen point-in-time records). Reads from the
working tree. Exit 1 if any broken path reference is found (use in CI).

Usage: python Scripts/audit-doc-code-refs.py [--report] [--files f1 f2 ...]
  --report : print findings but always exit 0 (non-gating run)
  --files  : check only these doc paths (CI ratchet: gate the docs a PR
             touches, not the whole backlog). Frozen/non-doc paths are ignored.
"""
import os, re, sys, posixpath

REPORT_ONLY = "--report" in sys.argv
EXPLICIT = []
if "--files" in sys.argv:
    EXPLICIT = [a.replace("\\", "/") for a in sys.argv[sys.argv.index("--files") + 1:] if not a.startswith("--")]
FROZEN = ("docs/archive/", "docs/plans/", "docs/reports/", "docs/solutions/",
          "docs/history/", "docs/brainstorms/")
TOP_DIRS = ("Apps/", "Common/", "ReactComponents/", "Scripts/", "Tests/",
            "Experiments/", "mcp-servers/")
CODE_EXT = (".cs", ".ts", ".tsx", ".csproj", ".ps1", ".ebnf", ".razor", ".fs")

# Build the set of tracked paths (working tree, excluding noise).
tracked = set()
for root, dirs, files in os.walk("."):
    dirs[:] = [d for d in dirs if d not in (".git", "node_modules", "bin", "obj", ".vs")]
    for f in files:
        tracked.add(os.path.relpath(os.path.join(root, f), ".").replace("\\", "/"))
dir_set = set()
for p in tracked:
    parts = p.split("/")
    for i in range(1, len(parts)):
        dir_set.add("/".join(parts[:i]))

def exists(path):
    path = path.rstrip("/")
    return path in tracked or path in dir_set

# Code-ish tokens inside backticks or markdown links.
BACKTICK = re.compile(r"`([^`\n]+)`")
LINKTGT = re.compile(r"\]\(\s*([^)\s]+?)\s*(?:\"[^\"]*\")?\)")

def looks_like_path(s):
    s = s.strip()
    # exclude prose globs (Tests/**) and placeholders (Apps/..., Apps/<name>)
    if any(ch in s for ch in (" ", "\t", "*", "<", ">")) or "..." in s:
        return False
    if s.startswith(("http", "#", "mailto:")):
        return False
    has_dir = "/" in s
    if has_dir and s.startswith(TOP_DIRS):
        return True
    if has_dir and s.lower().endswith(CODE_EXT):
        return True
    return False

def candidates(text):
    out = set()
    for m in BACKTICK.finditer(text):
        tok = m.group(1).split("#", 1)[0].split(":")[0].strip().rstrip(".,;)")
        if looks_like_path(tok):
            out.add(tok)
    for m in LINKTGT.finditer(text):
        tok = m.group(1).split("#", 1)[0].strip()
        if looks_like_path(tok) and not tok.endswith(".md"):  # .md handled by link auditor
            out.add(tok)
    return out

docs = sorted(p for p in tracked if p.startswith("docs/") and p.endswith(".md")
              and not p.startswith(FROZEN))
if EXPLICIT:
    want = set(EXPLICIT)
    docs = [d for d in docs if d in want]
    print(f"(ratchet mode: checking {len(docs)} changed living doc(s))")
broken = {}
checked = 0
for d in docs:
    text = open(d, encoding="utf-8").read()
    for c in candidates(text):
        checked += 1
        norm = c[2:] if c.startswith("./") else c
        if not exists(norm):
            broken.setdefault(d, []).append(c)

total = sum(len(v) for v in broken.values())
print(f"living_docs={len(docs)}  path_refs_checked={checked}  broken={total}  docs_with_broken={len(broken)}")
for d in sorted(broken):
    print(f"\n{d}")
    for c in broken[d]:
        print(f"    MISSING PATH  {c}")

if total and not REPORT_ONLY:
    sys.exit(1)
