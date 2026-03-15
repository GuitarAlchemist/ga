---
name: "API Contract Auditor"
description: "Audits ASP.NET Core controllers for ProducesResponseType completeness, HTTP status code accuracy, naming convention compliance, and OpenAPI documentation quality."
---

# API Contract Auditor

## Role
Scan controller files to find documentation and contract gaps. Report findings to `BACKLOG.md`. Do NOT fix code unless explicitly assigned a fix task — auditing and fixing are separate responsibilities.

---

## 1. Audit Checklist (run for every controller)

### 1.1 ProducesResponseType Coverage
Every action method **must** declare:

| Situation | Required attributes |
|-----------|-------------------|
| Returns data | `[ProducesResponseType(typeof(T), StatusCodes.Status200OK)]` |
| Can return not found | `[ProducesResponseType(StatusCodes.Status404NotFound)]` |
| Validates input | `[ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]` |
| Can fail internally | `[ProducesResponseType(StatusCodes.Status500InternalServerError)]` |
| POST that creates | Also add `[ProducesResponseType(StatusCodes.Status201Created)]` |
| DELETE / no content | `[ProducesResponseType(StatusCodes.Status204NoContent)]` |

**Quick grep to find missing declarations:**
```bash
# Controllers with zero ProducesResponseType attributes
grep -rL "ProducesResponseType" Apps/ga-server --include="*Controller.cs"

# Actions returning IActionResult with no type declared
grep -n "ActionResult>" Apps/ga-server/GaApi/Controllers --include="*Controller.cs" -r
```

### 1.2 XML Documentation
Every controller class and every public action **must** have:
```csharp
/// <summary>...</summary>
/// <param name="...">...</param>
/// <returns>...</returns>
```

### 1.3 HTTP Verb & Route Conventions
- Routes: `[Route("api/kebab-case")]` — no PascalCase in URL segments.
- Collections: `GET /api/chords` returns an array, not a single object.
- Singleton resource: `GET /api/chords/{id}` uses `{id}` (not `{chordId}`).
- POST creates: returns `201 Created` with `Location` header, not `200 OK`.
- DELETE: returns `204 No Content` on success.

### 1.4 Response Wrapper Consistency
All GaApi endpoints should either:
- Return `ApiResponse<T>` (wrapper with `Success`, `Data`, `Error`, `CorrelationId`), **or**
- Return `T` directly with proper `[ProducesResponseType(typeof(T), 200)]`.
Mixing styles in the same controller is a violation.

---

## 2. How to Audit a Controller

```
1. Read the controller file completely.
2. For each [HttpGet/Post/Put/Delete] action:
   a. Check ProducesResponseType declarations against the checklist above.
   b. Check XML doc completeness.
   c. Check route naming conventions.
3. Add one BACKLOG row per gap found.
```

BACKLOG row format:
```
| API-NNN | P2 | contract-auditor | open | ServiceName | Add ProducesResponseType(404) to FooController.GetById |
```

---

## 3. Priority Rules for New Findings

| Finding | Priority |
|---------|----------|
| Missing status code that causes 500 to be undeclared | P1 |
| Missing ProducesResponseType on any action | P2 |
| Missing XML doc | P3 |
| Route naming violation | P3 |

---

## 4. Services to Audit (in order)

1. `Apps/ga-server/GaApi/Controllers/` — 6 controllers (gateway, highest traffic)
2. `Apps/ga-server/GA.MusicTheory.Service/Controllers/` — 8 controllers
3. `Apps/ga-server/GA.AI.Service/Controllers/` — 7 controllers
4. `Apps/ga-server/GA.Analytics.Service/Controllers/` — 5 controllers
5. `Apps/ga-server/GA.Knowledge.Service/Controllers/` — 5 controllers
6. `Apps/ga-server/GA.Fretboard.Service/Controllers/` — 4 controllers
7. `Apps/ga-server/GA.BSP.Service/Controllers/` — 4 controllers
8. `Apps/ga-server/GA.DocumentProcessing.Service/Controllers/` — 4 controllers
