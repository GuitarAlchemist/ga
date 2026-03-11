---
status: pending
priority: p3
issue_id: "054"
tags: [code-review, performance, react, frontend, debounce, network]
---

# fetchAndShowVoicing Has No Debounce — Rapid Clicks Fire Concurrent Requests

## Problem Statement
`fetchAndShowVoicing` in `GAChatPanel` has no debounce. When 7 diatonic chord buttons are visible, rapid user interaction (e.g., arrow-key navigation or quick mouse clicks) can fire 7 concurrent fetch requests simultaneously. While abort-on-supersede logic is already in place, the requests are still initiated and consume server resources before being cancelled.

## Proposed Solution
- Add a 150ms leading-edge debounce before issuing the fetch call
- The abort-on-supersede mechanism already in place handles the cancellation side; debounce prevents the burst from being issued at all
- Use a `useRef`-held `setTimeout` id (or a lightweight debounce utility) — avoid introducing a heavy dependency for a simple timer

**File:** `ReactComponents/ga-react-components/src/components/GAChatPanel.tsx`

## Acceptance Criteria
- [ ] Clicking 7 chord buttons in rapid succession issues at most 1 fetch request (after debounce settles)
- [ ] Debounce delay is 150ms (leading-edge preferred so first click feels instant)
- [ ] Existing abort-on-supersede logic is preserved and still functions
- [ ] No new npm dependency added for debounce (use `setTimeout`/`clearTimeout` via `useRef`)
- [ ] TypeScript strict mode passes with no `any` introduced
- [ ] Manual test: single chord click still responds immediately (leading-edge behavior)
