---
status: pending
priority: p2
issue_id: "047"
tags: [code-review, performance, fsharp, fsi, concurrency]
---

# FSI Session Pool: Global Gate Kills Throughput

## Problem Statement
`GaFsiSessionPool` has a global `SemaphoreSlim(1,1)` gate, which makes a pool of 2 sessions useless — only one eval can run at a time. All concurrency benefit of the pool is negated, and callers queue behind a single lock regardless of which session is free.

## Proposed Solution
- Move the semaphore (or equivalent lock) to be per-session rather than global
- Use a channel or `BlockingCollection` as the back-pressure choke point so that `acquireSession` is truly async and callers wait only for a free session slot
- Ensure session release is idempotent and exception-safe (finally / `use` binding)
- Avoid `Thread.Sleep` or synchronous blocking inside async workflows

**File:** `Common/GA.Business.DSL/Interpreter/GaFsiSessionPool.fs`

## Acceptance Criteria
- [ ] Two concurrent evals can run simultaneously when two sessions exist in the pool
- [ ] `acquireSession` does not block the thread pool — it is fully async
- [ ] Releasing a session back to the pool is exception-safe
- [ ] Load test: 4 concurrent callers with 2-session pool complete in ~2× single-eval time, not ~4×
- [ ] No global lock serializes session acquisition
