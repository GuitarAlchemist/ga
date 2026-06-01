#!/usr/bin/env python3
"""Audit internal markdown links in docs/ against a git ref's tree.

Reports relative links whose target file does not exist in the tree.
Reads file content from git (so it audits the ref, not the working copy).
Usage: python Scripts/audit-doc-links.py [ref]   (default ref: origin/main)
"""
import subprocess, sys, re, posixpath
from collections import defaultdict

REF = sys.argv[1] if len(sys.argv) > 1 else "origin/main"

def git(*args):
    return subprocess.run(["git", *args], capture_output=True, text=True, encoding="utf-8").stdout

# Full set of tracked paths in the ref (for existence checks of any target).
all_paths = set(p for p in git("ls-tree", "-r", "--name-only", REF).splitlines() if p)
# Directories that exist (any prefix of a tracked file) — a link to a dir is valid.
all_dirs = set()
for p in all_paths:
    parts = p.split("/")
    for i in range(1, len(parts)):
        all_dirs.add("/".join(parts[:i]))
docs = [p for p in all_paths if p.startswith("docs/") and p.endswith(".md")]

# Markdown inline links: [text](target)  — capture target, ignore images? include both.
LINK = re.compile(r"\]\(\s*([^)\s]+?)\s*(?:\"[^\"]*\")?\)")

broken = defaultdict(list)   # doc -> [(target, resolved)]
n_links = 0
for doc in docs:
    text = git("show", f"{REF}:{doc}")
    docdir = posixpath.dirname(doc)
    for m in LINK.finditer(text):
        target = m.group(1)
        # Skip external / anchors / mailto / absolute-site links.
        if target.startswith(("http://", "https://", "mailto:", "#", "//")):
            continue
        # Strip any in-page anchor.
        path_part = target.split("#", 1)[0]
        if not path_part:
            continue  # pure anchor within same doc
        # Only audit links that look like repo files (have an extension or a slash).
        if "." not in posixpath.basename(path_part) and "/" not in path_part:
            continue
        n_links += 1
        # Resolve relative to the doc's directory; handle leading ./ and ../
        if path_part.startswith("/"):
            resolved = path_part.lstrip("/")
        else:
            resolved = posixpath.normpath(posixpath.join(docdir, path_part))
        if resolved not in all_paths and resolved not in all_dirs:
            broken[doc].append((target, resolved))

total_broken = sum(len(v) for v in broken.values())
print(f"ref={REF}  docs={len(docs)}  internal_links_checked={n_links}  broken={total_broken}  docs_with_broken={len(broken)}")
print("=" * 70)
for doc in sorted(broken):
    print(f"\n{doc}")
    for target, resolved in broken[doc]:
        print(f"    BROKEN  [{target}]  ->  {resolved}")
