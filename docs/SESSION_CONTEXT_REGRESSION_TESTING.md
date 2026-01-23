# Regression Testing Strategy for Session Context

## Overview

This document outlines our strategy for preventing regressions in the Musical Session Context feature.

## Test Pyramid

```
                    ┌─────────────┐
                    │   Manual    │  
                    │   Testing   │  
                    └─────────────┘
                  ┌─────────────────┐
                  │  Integration    │
                  │     Tests       │
                  └─────────────────┘
              ┌───────────────────────┐
              │     Unit Tests        │
              └───────────────────────┘
```

## Test Categories

### 1. Unit Tests ✅

**Location**: `Tests/Common/GA.Business.Core.Tests/Session/SessionContextTests.cs`

**Coverage**:
- ✅ `MusicalSessionContext` immutability
- ✅ Fluent update methods
- ✅ `FretboardRange` value object logic
- ✅ `InMemorySessionContextProvider` thread safety
- ✅ Event notifications

**Run**: `dotnet test Tests/Common/GA.Business.Core.Tests`

### 2. Integration Tests

**Location**: `Tests/Apps/GaApi.Tests/Integration/SessionContextChatbotTests.cs`

**Coverage**:
- ✅ Session context injection into `ChatbotSessionOrchestrator`
- ✅ System prompt generation includes context
- ✅ Context updates reflect in prompts
- ⏳ End-to-end chatbot flow with context

**Run**: `dotnet test Tests/Apps/GaApi.Tests`

### 3. Contract Tests

**Purpose**: Ensure the session context API contract doesn't break

**Example Test**:
```csharp
[Fact]
public void SessionContext_HasRequiredProperties()
{
    var context = MusicalSessionContext.Default();
    
    // These properties must always exist
    Assert.NotNull(context.Tuning);
    Assert.Equal(NotationStyle.Auto, context.NotationStyle);
    
    // These are the contract - breaking changes need major version bump
    var properties = typeof(MusicalSessionContext).GetProperties();
    Assert.Contains(properties, p => p.Name == "Tuning");
    Assert.Contains(properties, p => p.Name == "CurrentKey");
    Assert.Contains(properties, p => p.Name == "SkillLevel");
}
```

## CI/CD Integration

### Pre-commit Hooks

```bash
# .git/hooks/pre-commit
#!/bin/bash
echo "Running session context tests..."
dotnet test Tests/Common/GA.Business.Core.Tests/Session --verbosity quiet
if [ $? -ne 0 ]; then
    echo "❌ Session context tests failed!"
    exit 1
fi
```

### Build Pipeline

```yaml
# .github/workflows/test-session-context.yml
name: Session Context Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Unit Tests
        run: dotnet test Tests/Common/GA.Business.Core.Tests/Session
      
      - name: Integration Tests
        run: dotnet test Tests/Apps/GaApi.Tests/Integration
```

## Regression Scenarios to Test

### Scenario 1: Session Context Persists Across Requests (Scoped)

```csharp
[Fact]
public async Task SessionContext_PersistsAcrossMultipleMessagesInSameScope()
{
    var provider = new InMemorySessionContextProvider();
    provider.UpdateContext(ctx => ctx.WithSkillLevel(SkillLevel.Beginner));
    
    // First message
    var prompt1 = await orchestrator.BuildSystemPromptAsync("msg1", false, default);
    Assert.Contains("Beginner", prompt1);
    
    // Second message (same scope)
    var prompt2 = await orchestrator.BuildSystemPromptAsync("msg2", false, default);
    Assert.Contains("Beginner", prompt2);
}
```

### Scenario 2: Default Context is Valid

```csharp
[Fact]
public void DefaultContext_IsAlwaysValid()
{
    var context = MusicalSessionContext.Default();
    
    // Must have valid tuning
    Assert.NotNull(context.Tuning);
    Assert.Equal(6, context.Tuning.StringCount);
    
    // Optional fields can be null
    Assert.Null(context.CurrentKey);
}
```

### Scenario 3: Immutability is Maintained

```csharp
[Fact]
public void AllUpdateMethods_ReturnNewInstance()
{
    var original = MusicalSessionContext.Default();
    
    var methods = new Func<MusicalSessionContext>[]
    {
        () => original.WithSkillLevel(SkillLevel.Expert),
        () => original.WithGenre(MusicalGenre.Jazz),
        () => original.WithRange(FretboardRange.OpenPosition())
    };
    
    foreach (var method in methods)
    {
        var updated = method();
        Assert.NotSame(original, updated);
    }
}
```

### Scenario 4: System Prompt Format Stability

```csharp
[Fact]
public void SystemPrompt_AlwaysContainsSessionContextSection()
{
    var provider = new InMemorySessionContextProvider();
    var prompt = orchestrator.BuildSystemPromptAsync("test", false, default).Result;
    
    // Contract: System prompt must always have these markers
    Assert.Contains("CURRENT SESSION CONTEXT:", prompt);
    Assert.Contains("Tuning:", prompt);
}
```

## Monitoring in Production

### Log Verification

Add structured logging to track context usage:

```csharp
logger.LogInformation(
    "Chatbot request with context: Key={Key}, Skill={Skill}, Genre={Genre}",
    context.CurrentKey,
    context.SkillLevel,
    context.CurrentGenre
);
```

### Metrics to Track

1. **Context Usage Rate**: % of requests with non-default context
2. **Update Frequency**: How often context is updated
3. **Property Distribution**: Which properties are most commonly set

## Breaking Change Detection

### API Compatibility Tests

```csharp
[Fact]
public void SessionContext_BackwardCompatible()
{
    // Ensure old code still compiles
    var oldWay = MusicalSessionContext.Default();
    var updated = oldWay with { NotationStyle = NotationStyle.PreferSharps };
    
    Assert.Equal(NotationStyle.PreferSharps, updated.NotationStyle);
}
```

## Test Data Builders

For easier test setup:

```csharp
public class SessionContextBuilder
{
    private MusicalSessionContext _context = MusicalSessionContext.Default();
    
    public SessionContextBuilder ForBeginner()
    {
        _context = _context.WithSkillLevel(SkillLevel.Beginner);
        return this;
    }
    
    public SessionContextBuilder InJazz()
    {
        _context = _context.WithGenre(MusicalGenre.Jazz);
        return this;
    }
    
    public SessionContextBuilder InKey(string key)
    {
        // Parse key and set
        return this;
    }
    
    public MusicalSessionContext Build() => _context;
}

// Usage in tests
var context = new SessionContextBuilder()
    .ForBeginner()
    .InJazz()
    .Build();
```

## Regression Test Checklist

Before each release, verify:

- [ ] All unit tests pass
- [ ] Integration tests pass
- [ ] System prompt contains context section
- [ ] Default context is valid
- [ ] Immutability is maintained
- [ ] Thread safety under concurrent access
- [ ] Context updates raise events
- [ ] DI registration works (scoped, singleton)
- [ ] No memory leaks (context properly cleaned up)
- [ ] Performance acceptable (<1ms for context operations)

## Quick Verification Commands

```bash
# Run all session context tests
dotnet test --filter "FullyQualifiedName~Session"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run integration tests only
dotnet test --filter "Category=Integration&FullyQualifiedName~Session"

# Performance test
dotnet test --filter "FullyQualifiedName~Performance"
```

## Future Improvements

1. **Snapshot Testing**: Save/compare system prompt outputs
2. **Property-Based Testing**: Use FsCheck for exhaustive scenarios
3. **Load Testing**: Verify context under high concurrency
4. **A/B Testing**: Compare chatbot responses with/without context
5. **User Acceptance Testing**: Track if users find responses more relevant

## Summary

✅ **Comprehensive unit tests** prevent domain logic regressions  
✅ **Integration tests** verify end-to-end functionality  
✅ **Contract tests** prevent breaking API changes  
✅ **CI/CD integration** catches issues early  
✅ **Monitoring** tracks production usage  

**Result**: High confidence that session context will continue working as expected!
