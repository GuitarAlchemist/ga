---
status: pending
priority: p2
issue_id: "045"
tags: [code-review, security, fsharp, dsl]
---

# IO Closures: Unrestricted File Path Access

## Problem Statement
`io.readFile` and `io.writeFile` in `IoClosures.fs` have no path restrictions — any caller can read/write arbitrary filesystem paths including `/etc/passwd`, `/run/secrets`, and other sensitive system files.

## Proposed Solution
- Add `AllowedBasePaths` validation before any file operation
- Canonicalize paths with `Path.GetFullPath` to prevent traversal attacks (`../../etc/passwd`)
- Reject any path that does not resolve to within an allowed base directory
- Surface a descriptive error (not an exception) when access is denied

**File:** `Common/GA.Business.DSL/Closures/BuiltinClosures/IoClosures.fs`

## Acceptance Criteria
- [ ] `io.readFile` rejects paths outside allowed base directories
- [ ] `io.writeFile` rejects paths outside allowed base directories
- [ ] Paths are canonicalized with `Path.GetFullPath` before comparison
- [ ] Symlink traversal and `..` segments cannot escape allowed dirs
- [ ] Rejection returns a structured error, not a thrown exception
- [ ] Unit tests cover: allowed path, path traversal attempt, absolute path outside base, symlink escape
