# n8n Workflow Specification: `ga_tab_ingestion`

## Objective
Ingest raw guitar tab content with deterministic cleaning first, minimizing LLM usage.

## Nodes Structure

### 1. Webhook (Start)
- **Method**: POST
- **Path**: `/ingest-tab`
- **Body**: `{ "url": "https://tabs.ultimate-guitar.com/..." }`

### 2. HTTP Request (Fetcher)
- **Method**: GET
- **URL**: `{{$json.url}}`
- **Response Format**: String (HTML)
- **Error Handling**: Catch 403/Blocked -> Reroute to "Source Adapter" (future Selenium/Headless).

### 3. HTML Extract (Parser)
- **Selector**: `pre` (Standard) or `.js-tab-content` (UG).
- **Output**: `tab_raw`

### 4. Deterministic Cleaner (Code Node)
- **Logic**:
  ```javascript
  const text = items[0].json.tab_raw;
  // Keep lines that look like tabs or chords
  const lines = text.split('\n');
  const cleanLines = lines.filter(line => 
     /^[eBGDAE]\|/.test(line) || // String line
     /^[A-G][b#]?m?(aj|dim|aug|sus)?[0-9]*/.test(line) || // Chord symbol
     /^\|/.test(line) // Bar line
  );
  
  if (cleanLines.length < 6) throw new Error("PARSER_MISMATCH");
  return { tab_clean: cleanLines.join('\n') };
  ```

### 5. LLM Cleaner (Fallback)
- **Trigger**: Run ONLY if Deterministic Cleaner errors.
- **Prompt**: "Rescue this tab content..."
- **Output**: `tab_clean`

### 6. Handoff (Internal)
- **Method**: POST
- **URL**: `http://localhost:5678/webhook/analyze-tab` (Or internal n8n sub-workflow)
- **Body**: `{ "tab_content": "{{$json.tab_clean}}" }`
- **Authentication**: Header Auth
