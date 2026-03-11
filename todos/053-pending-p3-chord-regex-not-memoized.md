---
status: pending
priority: p3
issue_id: "053"
tags: [code-review, performance, react, frontend, regex]
---

# Chord Regex Has Shared lastIndex State and Is Not Memoized

## Problem Statement
`CHORD_REGEX` is defined at module level with the `/g` flag. The `/g` flag causes the regex object to maintain `lastIndex` state across calls, which is shared across all renders and components. Additionally, `ChordAnnotatedText` re-scans the full text on every parent re-render even when the text has not changed, producing O(n) work on unchanged input.

## Proposed Solution
- Move regex construction inside a `useMemo([text])` hook in `ChordAnnotatedText` so it is recreated only when `text` changes
- Creating the regex inside the memo scope eliminates shared `lastIndex` state (each scan gets a fresh regex instance)
- Alternatively, use a non-global regex with explicit iteration if the pattern must remain module-level

**File:** `ReactComponents/ga-react-components/src/components/GAChatPanel.tsx`

## Acceptance Criteria
- [ ] `CHORD_REGEX` (or equivalent) is not a module-level `/g` regex shared across renders
- [ ] `ChordAnnotatedText` does not re-scan text when the `text` prop has not changed
- [ ] No `lastIndex`-related bugs when multiple `ChordAnnotatedText` instances render simultaneously
- [ ] TypeScript strict mode passes with no `any` introduced
- [ ] Existing chord annotation behavior is visually unchanged
