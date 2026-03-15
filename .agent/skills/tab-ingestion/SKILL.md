---
name: Tab Ingestion
description: Ingests a guitar tab from a URL using the browser subagent, cleans it, and posts it to the local GA API for analysis.
---

# Tab Ingestion Workflow

This skill automatically extracts guitar tablature from web pages (like Ultimate-Guitar), cleans the content, and submits it to the local Guitar Alchemist Analysis API.

## Requirements
*   `browser_subagent` capability
*   `run_command` capability (to invoke `Invoke-RestMethod` to the C# API)
*   The GaApi must be running at `http://localhost:7001` (specifically the `POST /api/analyze` endpoint).

## Execution Steps

### 1. Fetch Tab (Browser Subagent)
Use the `browser_subagent` to navigate to the provided URL. Your instructions to the subagent should be:
*   Navigate to the URL.
*   Find the main `<pre>` block or the core tablature div (`.js-tab-content`, etc).
*   Scrape the full text of that block.
*   Do NOT include the surrounding page clutter, ads, or comments.
*   Return the raw tab as a formatted JSON string or plain text block.

### 2. Clean/Verify the Tab
Review the output from the browser subagent using your native LLM capabilities.
Verify that:
*   It looks like a guitar tab (has string names like `e|---`, `B|---`, etc.).
*   Extract only the musical portions (chords and tab lines).
*   Store this in a temporary JSON file or inline string (`tab.json`).

### 3. Analyze Tab (API Request)
Construct a JSON payload file:
```json
{
  "tab": "<cleaned tab content>"
}
```
Use `run_command` with PowerShell's `Invoke-RestMethod` to submit this payload to the analysis API:
```pwsh
Invoke-RestMethod -Uri http://localhost:7001/api/analyze -Method Post -ContentType "application/json" -InFile .\tab.json | ConvertTo-Json -Depth 10
```

### 4. Output
Present the final JSON analysis results to the user or save it to a requested file. Ensure you mention the Detected Key, Extracted Chords, and the general complexity/metadata returned by the API.
