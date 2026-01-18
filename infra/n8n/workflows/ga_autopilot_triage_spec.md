# n8n Workflow Specification: `ga_autopilot_triage`

## Objective
The "Immune System" of the virtual team. Enforces convergence, prevents infinite loops, and produces actionable repair patches or escalation tickets.

## Core Concepts
- **Convergence**: Limits retries via `attempt` counter.
- **Safety**: Agents propose patches; Logic Nodes apply them.
- **Immune Memory**: Checks if this specific error pattern has failed recently.

## Nodes Structure

### 1. Webhook (Internal - Invariants)
- **Method**: POST
- **Path**: `/autopilot-triage`
- **Body Schema**:
  ```json
  {
    "request_id": "uuid",
    "workflow": "ga_tab_analysis",
    "attempt": 1,
    "error": {
      "code": "QA_FAIL | PARSER_MISMATCH | API_TIMEOUT",
      "path": "key",
      "message": "..."
    },
    "trace_ref": "trace://2026-01-16/abc123.json"
  }
  ```

### 2. Immune Memory Check (Code Node)
- **Logic**:
  - Hash `(error.code + error.path + trace_input_hash)`.
  - Check against recent failure DB (or file cache).
  - If exists -> Mark as `RECURRING`.
  - Return `{ is_recurring: boolean }`.

### 3. Failure Categorizer (Code Node)
- **Logic**: (Replaces fragile string matching)
  ```javascript
  const input = items[0].json;
  
  // 1. Hard Stop for Recursion
  if (input.attempt >= 2) return { type: "ESCALATE", reason: "RETRY_LIMIT" };
  if (input.is_recurring) return { type: "ESCALATE", reason: "IMMUNE_REJECT" };

  // 2. Route by Code
  switch (input.error.code) {
    case "QA_FAIL": return { type: "QA" };
    case "PARSER_MISMATCH": return { type: "PARSER" };
    case "API_TIMEOUT": 
    case "503": return { type: "RETRYABLE" };
    default: return { type: "ESCALATE", reason: "UNKNOWN_ERROR" };
  }
  ```

### 4. Switch (Router)
- **Rules**:
  - `type == 'QA'` -> **QA Fixer Agent**
  - `type == 'PARSER'` -> **Parser Repair Proposal**
  - `type == 'RETRYABLE'` -> **Wait & Retry**
  - `type == 'ESCALATE'` -> **Escalation**

### 5. QA Fixer Agent (Strict Patching)
- **Prompt**:
  > You may only modify the field at `error.path`.
  > All other fields must be byte-for-byte identical.
  > If unrepairable, respond with `status: UNREPAIRABLE`.
- **Output Schema**:
  ```json
  {
    "status": "REPAIRED",
    "patched_field": "key",
    "old_value": "H#",
    "new_value": "B"
  }
  ```
- **Action**: Apply patch via **Set** Node (External to LLM), then re-submit to `ga_tab_analysis` with `attempt + 1`.

### 6. Parser Repair Proposal (Safe)
- **Constraint**: Do NOT modify regexes directly.
- **Output**:
  ```json
  {
    "symptom": "Missing 6th string",
    "suggested_rule_change": "Allow lowercase 'e|'",
    "confidence": 0.72
  }
  ```
- **Action**: Route to **Escalation** (Human Review required for code changes).

### 7. Escalation (Structured Ticket)
- **Action**: Append to `infra/n8n/logs/issues_backlog.md`
- **Template**:
  ```markdown
  ## 🚨 Autopilot Escalation
  - **Request ID**: `{{request_id}}`
  - **Workflow**: `{{workflow}}`
  - **Error**: `{{error.code}}` at `{{error.path}}`
  - **Reason**: `{{reason}}`
  - **Trace**: `{{trace_ref}}`
  ### Suggested Action
  - Verify if `{{error.new_value}}` is valid.
  ```
