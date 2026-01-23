# Testing Session Context with Chatbot

## Test Setup

The session context is already integrated into the chatbot via `ChatbotSessionOrchestrator`. Now we can test it!

## Test Scenarios

### Test 1: Default Context (Standard Tuning, No Preferences)

**Prompt**: "Show me some chords"

**Expected Behavior**: 
- System prompt includes: `Tuning: E2 A2 D3 G3 B3 E4`
- General chord suggestions

### Test 2: Set to Beginner + C Major

**Session Context**:
```csharp
sessionContext.UpdateContext(ctx => ctx
    .WithSkillLevel(SkillLevel.Beginner)
    .WithKey(Keys.C.Major)
);
```

**Prompt**: "Show me some chords"

**Expected Behavior**:
- System prompt includes:
  - `Skill Level: Beginner`
  - `Current Key: C Major`
- Should suggest easier chords in C major (C, F, G, Am, etc.)

### Test 3: Jazz Context

**Session Context**:
```csharp
sessionContext.UpdateContext(ctx => ctx
    .WithSkillLevel(SkillLevel.Advanced)
    .WithGenre(MusicalGenre.Jazz)
    .WithKey(Keys.G.Major)
);
```

**Prompt**: "Show me some chords for a ii-V-I"

**Expected Behavior**:
- System prompt includes:
  - `Skill Level: Advanced`
  - `Musical Genre: Jazz`
  - `Current Key: G Major`
- Should suggest jazz voicings for Am7-D7-Gmaj7 in G major

### Test 4: Fretboard Range Constraint

**Session Context**:
```csharp
sessionContext.UpdateContext(ctx => ctx
    .WithRange(FretboardRange.OpenPosition())
);
```

**Prompt**: "Show me a G chord"

**Expected Behavior**:
- System prompt includes: `Fretboard Range: Frets 0-3`
- Should suggest open position G chord

## How to Test

1. **Build GaApi** ✅ (Already integrated)
2. **Run the server**: `dotnet run --project Apps/ga-server/GaApi`
3. **Send chat requests** via:
   - Swagger UI at `http://localhost:5000/swagger`
   - POST to `/api/chatbot/chat`
   - Or use the existing chatbot UI

## Example Request

```bash
curl -X POST http://localhost:5000/api/chatbot/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Show me some beginner chords",
    "conversationHistory": [],
    "useSemanticSearch": true
  }'
```

## Expected System Prompt (visible in logs)

```
You are Guitar Alchemist, an expert guitar teacher and music theory assistant.
You help guitarists learn chords, scales, techniques, and music theory.
Provide clear, practical advice with specific fretboard examples where useful.
Explain complex concepts in simple terms, tailored to guitarists.

CURRENT SESSION CONTEXT:
- Tuning: E2 A2 D3 G3 B3 E4
- Skill Level: Beginner
- Musical Genre: Rock

Use this session context to provide more relevant and personalized responses.
When suggesting chords or scales, consider the current key, skill level, and preferences.

Be concise but thorough. Use markdown formatting when helpful.
If you do not know something, say so honestly.
```

## Updating Session Context Programmatically

For now, session context can be updated in code (future: add API endpoints):

```csharp
// In Program.cs or a controller
app.MapPost("/api/session/update", (ISessionContextProvider ctx) =>
{
    ctx.UpdateContext(session => session
        .WithSkillLevel(SkillLevel.Intermediate)
        .WithGenre(MusicalGenre.Jazz)
    );
    return Results.Ok(ctx.GetContext());
});
```

## Verification Points

✅ **Build succeeds**
✅ **Session context injected into orchestrator**
✅ **System prompt includes context**
⏳ **Manual testing with various contexts**
⏳ **Verify LLM uses context in responses**
