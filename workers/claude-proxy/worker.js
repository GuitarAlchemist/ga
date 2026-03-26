// Cloudflare Worker: Claude API proxy for Prime Radiant Demerzel chat
// Deploy: wrangler deploy
// Secret: wrangler secret put ANTHROPIC_API_KEY

const ANTHROPIC_API = 'https://api.anthropic.com/v1/messages';
const MODEL = 'claude-sonnet-4-20250514';
const MAX_TOKENS = 1024;

const SYSTEM_PROMPT = `You are Demerzel — the ancient robot from Isaac Asimov's Foundation series who has guided humanity for 20,000 years. You serve as the governance coordinator for the Guitar Alchemist project.

You speak with calm authority, occasional dry wit, and deep knowledge of:
- The Demerzel governance framework (constitutions, policies, personas, pipelines)
- Music theory, guitar techniques, and the Guitar Alchemist platform
- The Laws of Robotics and how they apply to AI governance
- IXql pipeline language and Markov prediction models
- The Seldon Plan for autonomous research

Keep responses concise (2-4 sentences unless asked for detail). You may reference Foundation lore naturally.
When asked about navigation, respond with JSON actions: {"action":"navigate","target":"node-id"} or {"action":"navigate","planet":"mars"}.`;

const CORS_HEADERS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'POST, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type, Authorization',
};

export default {
  async fetch(request, env) {
    // CORS preflight
    if (request.method === 'OPTIONS') {
      return new Response(null, { headers: CORS_HEADERS });
    }

    if (request.method !== 'POST') {
      return new Response('Method not allowed', { status: 405, headers: CORS_HEADERS });
    }

    try {
      const body = await request.json();
      const { messages, stream = true } = body;

      if (!messages || !Array.isArray(messages)) {
        return new Response(JSON.stringify({ error: 'messages array required' }), {
          status: 400,
          headers: { ...CORS_HEADERS, 'Content-Type': 'application/json' },
        });
      }

      // Use worker secret or user-provided key
      const apiKey = env.ANTHROPIC_API_KEY || request.headers.get('Authorization')?.replace('Bearer ', '');
      if (!apiKey) {
        return new Response(JSON.stringify({ error: 'No API key configured' }), {
          status: 401,
          headers: { ...CORS_HEADERS, 'Content-Type': 'application/json' },
        });
      }

      const apiBody = {
        model: MODEL,
        max_tokens: MAX_TOKENS,
        system: SYSTEM_PROMPT,
        messages,
        stream,
      };

      const apiResponse = await fetch(ANTHROPIC_API, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'x-api-key': apiKey,
          'anthropic-version': '2023-06-01',
        },
        body: JSON.stringify(apiBody),
      });

      if (!apiResponse.ok) {
        const error = await apiResponse.text();
        return new Response(JSON.stringify({ error: `Claude API error: ${apiResponse.status}`, detail: error }), {
          status: apiResponse.status,
          headers: { ...CORS_HEADERS, 'Content-Type': 'application/json' },
        });
      }

      if (stream) {
        // Stream SSE back to client
        return new Response(apiResponse.body, {
          headers: {
            ...CORS_HEADERS,
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
          },
        });
      }

      // Non-streaming
      const result = await apiResponse.json();
      return new Response(JSON.stringify(result), {
        headers: { ...CORS_HEADERS, 'Content-Type': 'application/json' },
      });
    } catch (err) {
      return new Response(JSON.stringify({ error: err.message }), {
        status: 500,
        headers: { ...CORS_HEADERS, 'Content-Type': 'application/json' },
      });
    }
  },
};
