---
status: pending
priority: p3
issue_id: "052"
tags: [code-review, quality, rop, error-handling, voicing]
---

# VoicingComfortService Uses try/catch to Manufacture a Result — ROP Violation

## Problem Statement
`VoicingComfortService.GetComfortRankedAsync` wraps a call in `try/catch ArgumentException` purely to convert an exception into a `Result`. This is a ROP anti-pattern: the exception should never be thrown in the first place. The fix belongs upstream in `VoicingFilterService`, which should validate its inputs and return `Result` directly instead of throwing `ArgumentException`.

## Proposed Solution
- Update `VoicingFilterService` to validate inputs and return `Result<T, VoicingError>` instead of throwing `ArgumentException`
- Remove the `try/catch` block in `VoicingComfortService.GetComfortRankedAsync`
- Propagate `Result` through the call chain using `bind`/`map` combinators from `GA.Core.Functional`

**File:** `Apps/ga-server/GaApi/Services/VoicingComfortService.cs`

## Acceptance Criteria
- [ ] `VoicingFilterService` returns `Result<T, VoicingError>` for invalid inputs instead of throwing
- [ ] `VoicingComfortService.GetComfortRankedAsync` contains no `try/catch` blocks
- [ ] Error propagation uses `Result` combinators (`bind`, `map`, `mapError`)
- [ ] Existing voicing-related tests pass after refactor
- [ ] No new `throw` statements introduced in service layer
