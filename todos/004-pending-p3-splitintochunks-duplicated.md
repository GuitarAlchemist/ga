---
status: pending
priority: p3
issue_id: "004"
tags: [code-review, chatbot, quality, duplication]
dependencies: []
---

# SplitIntoChunks method duplicated in ChatbotController and ChatbotHub

## Problem Statement

The `private static IEnumerable<string> SplitIntoChunks(string text)` method is identical in both `ChatbotController` and `ChatbotHub`. Any future change (e.g., adjusting chunk size, changing the sentence-boundary regex) must be made in two places.

## Findings

`Apps/ga-server/GaApi/Controllers/ChatbotController.cs:232`:
```csharp
private static IEnumerable<string> SplitIntoChunks(string text)
{
    if (string.IsNullOrEmpty(text)) yield break;
    var sentences = System.Text.RegularExpressions.Regex
        .Split(text, @"(?<=[.!?])\s+")
        .Where(s => !string.IsNullOrWhiteSpace(s));
    foreach (var sentence in sentences)
        yield return sentence;
}
```

`Apps/ga-server/GaApi/Hubs/ChatbotHub.cs:141`: exact duplicate.

## Proposed Solutions

### Option A: Extract to a static helper class

```csharp
// GaApi/Helpers/SseChunker.cs
namespace GaApi.Helpers;

internal static class SseChunker
{
    public static IEnumerable<string> Split(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        foreach (var s in Regex.Split(text, @"(?<=[.!?])\s+").Where(s => !string.IsNullOrWhiteSpace(s)))
            yield return s;
    }
}
```

**Pros:** Single source of truth; trivial change
**Cons:** New file
**Effort:** Small
**Risk:** None

### Option B: Move to ChatbotOrchestrationExtensions or a shared utility in GA.Business.Core.Orchestration

**Pros:** Accessible from any layer that formats responses
**Cons:** Puts presentation logic in a shared library
**Effort:** Small
**Risk:** Low

## Technical Details

- **Affected files:** `ChatbotController.cs`, `ChatbotHub.cs`

## Acceptance Criteria

- [ ] `SplitIntoChunks` / `Split` exists in exactly one place
- [ ] Both controller and hub call the shared version
- [ ] Build passes

## Work Log

- 2026-03-06: Found during code review of PR #2
