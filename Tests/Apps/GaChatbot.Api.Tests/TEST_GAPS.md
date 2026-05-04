# GaChatbot.Api Test Gaps

Last reviewed: 2026-04-30

## Covered now

- Root page returns the mini chat UI and links the operational endpoints.
- `/api` service metadata shape.
- `/api/chatbot/status` controller contract using a fake readiness service.
- `/api/chatbot/examples` prompt list availability.
- `/api/chatbot/chat` empty-message validation.
- `/api/chatbot/chat` max-length validation.
- `/api/chatbot/chat` busy response when the LLM concurrency gate is saturated.
- `/api/chatbot/chat` request mapping for conversation history and response metadata.
- `/api/chatbot/chat/stream` SSE routing metadata, chunk ordering, `[DONE]`, busy errors, and empty-message errors.
- `/api/chatbot/agui/stream` AG-UI happy path, event ordering, busy errors, internal errors, short run IDs, and empty-message validation.
- Provider readiness for unsupported providers, Ollama non-success, and Ollama request failures.
- CORS preflight for configured allowed origins.
- Full algebra route smoke through the real application stack, including IX-compatible grounding.

## Remaining gaps

- Browser E2E coverage: no automated test currently fills the mini chat UI, clicks Send, and verifies rendered messages in a real browser.
- Provider failure behavior: add tests for slow provider timeout, malformed provider response, and Anthropic/OpenAI configuration errors if those providers are enabled.
- Frontend integration: the React/Nebula chat surface still needs coverage against the thin API base URL and CORS settings.
- Conversation history limits: there is no truncation or budget test for large histories sent from the browser UI.
- Security headers and CORS: same-origin behavior and disallowed origins should be pinned before deployment.
- Accessibility: the mini UI needs keyboard and accessible-name checks in browser tests.
- Performance envelope: no latency budget test exists for lightweight algebra routing or provider-backed freeform chat.
- Operational lifecycle: scripts that start/status/test the chatbot API need tests or smoke coverage to ensure they do not drift from launch settings.
