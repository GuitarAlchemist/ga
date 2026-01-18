# n8n Workflow Specification: `ga_tab_analysis`

## Objective
hardened, deterministic orchestration of guitar tab analysis.

## Core Concepts
- **Idempotency**: All runs initiated with a `request_id` (UUID).
- **Traceability**: Accumulate `trace` object (Inputs, Decisions, Output).
- **Clean Room**: Narrator only sees validated, structured data.

## Nodes Structure

### 1. Webhook (Start)
- **Method**: POST
- **Path**: `/analyze-tab`
- **Body**: `{ "tab_content": "...", "request_id": "UUID", "attempt": 1 }`

### 2. Heuristic Classifier (Code Node)
- **Concept**: Deterministic first, cheap and fast.
- **Logic**:
  ```javascript
  const text = items[0].json.tab_content;
  const tabLines = text.match(/^[eBGDAE]\|/gm)?.length || 0;
  const dashDensity = (text.match(/-/g)?.length || 0) / text.length;

  if (tabLines > 2 || dashDensity > 0.1) return { type: 'TAB' };
  if (text.startsWith('/') || text.startsWith('!')) return { type: 'COMMAND' };
  return { type: 'QUESTION' }; // Fallback
  ```

### 3. Switch (Router)
- **Rules**:
  - `type == 'TAB'` -> **Dev Agent**
  - `type == 'QUESTION'` -> **RAG Agent**
  - `type == 'COMMAND'` -> **Command Parser**

### 4. Dev Agent (Tool Caller)
- **Tools**: `GuitarAlchemist API`
- **Endpoint**: `POST http://host.docker.internal:5000/api/analyze`
- **Payload**: `{ "tab": "..." }`
- **Output**: Structured Analysis JSON.

### 5. QA Agent (Strict Logic) (Code Node + LLM Fallback)
- **Step A**: **Deterministic Check** (Code Node)
  - Verify `key` in `[C, C#, D...]`.
  - Verify `style` in Enums.
  - Regex check for illegal characters in chords.
- **Step B**: **LLM Check** (Only if needed or for nuanced grounding)
  - Output:
    ```json
    {
      "status": "FAIL",
      "errors": [
        {"code":"QA_FAIL", "path":"key", "message":"H# is not a key"}
      ]
    }
    ```

### 6. Failure Handler (If QA Fails)
- **Action**: Call `ga_autopilot_triage` workflow.
- **Payload**:
  ```json
  {
      "request_id": "items[0].json.request_id",
      "workflow": "ga_tab_analysis",
      "attempt": "items[0].json.attempt",
      "error": "items[0].json.qa_error",
      "trace_ref": "trace://" + $now + "/" + items[0].json.request_id + ".json"
  }
  ```
- **Stop**: End workflow early (Autopilot handles the retry).

### 7. Clean Room (Set Node)
- **Task**: Strip all raw inputs, intermediate thoughts, and prompts. Keep ONLY validated Analysis JSON.

### 8. PM / Narrator Agent (LLM Chain)
- **Input**: Clean Analysis JSON.
- **Prompt**: "Summarize these facts for a guitarist. Do not invent."

### 9. Trace Logger & Response
- **Action**: Save complete trace to file/DB (for regression).
- **Response**: Final JSON with Narrative.
