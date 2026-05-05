using GaChatbot.Api.Extensions;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

Environment.SetEnvironmentVariable(
    "GA_STATE_DIR",
    Path.Combine(AppContext.BaseDirectory, "state"));

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole();
builder.Logging.AddDebug();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHttpClient();
builder.Services.AddTransient(_ => new HttpClient());
builder.Services.AddMinimalChatbotApi(builder.Configuration);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ChatbotClient", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

var app = builder.Build();

// Optional path-base for hosting under a public host's sub-path
// (e.g. demos.guitaralchemist.com/chatbot via Cloudflare Tunnel ingress
// `path: ^/chatbot(/.*)?$ -> localhost:5252`). Empty by default so direct
// localhost:5252/ access continues to work; UsePathBase only strips the
// prefix when present, so BOTH access modes coexist.
var pathBase = builder.Configuration["Chatbot:PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    // Normalise: must start with '/'. A misconfigured value like "chatbot"
    // (no leading slash) would silently bypass UsePathBase. Force the
    // slash so config typos still produce correct routing. Strip any
    // trailing slash on the configured value too — the redirect logic
    // below adds it back deliberately.
    if (!pathBase.StartsWith('/')) pathBase = "/" + pathBase;
    pathBase = pathBase.TrimEnd('/');

    var pathBaseNoSlash   = pathBase;
    var pathBaseWithSlash = pathBase + "/";

    // Trailing-slash redirect: must run BEFORE UsePathBase so it sees
    // the unstripped request path. A user landing at `/chatbot` (no
    // slash) resolves the inline HTML's relative URLs against the
    // parent dir, so `fetch('api/chatbot/chat')` becomes
    // `/api/chatbot/chat` at the host root — bypasses the Cloudflare
    // path-based ingress and 404s. PR #111 review flagged this as the
    // same regression class as shipped bug #2 (VexFlow not loaded).
    // 308 (permanent + preserveMethod) so POSTs redirect cleanly too.
    app.Use(async (ctx, next) =>
    {
        if (string.Equals(ctx.Request.Path.Value, pathBaseNoSlash, StringComparison.Ordinal))
        {
            var qs = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : string.Empty;
            ctx.Response.Redirect(pathBaseWithSlash + qs, permanent: true, preserveMethod: true);
            return;
        }
        await next();
    });

    app.UsePathBase(pathBaseNoSlash);
}

app.UseExceptionHandler();
app.UseStaticFiles();

if (allowedOrigins.Length > 0)
{
    app.UseCors("ChatbotClient");
}

app.MapControllers();
app.MapGet("/", () => Results.Content(
    """
    <!doctype html>
    <html lang="en">
    <head>
      <meta charset="utf-8">
      <meta name="viewport" content="width=device-width, initial-scale=1">
      <title>GA Chatbot</title>
      <style>
        :root { color-scheme: light; }
        * { box-sizing: border-box; }
        body {
          margin: 0;
          font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
          background: #f6f7f9;
          color: #172033;
        }
        main {
          display: grid;
          grid-template-columns: minmax(0, 1fr) 260px;
          gap: 24px;
          width: min(1120px, calc(100vw - 32px));
          margin: 24px auto;
        }
        h1 { margin: 0; font-size: 28px; font-weight: 700; }
        h2 { margin: 0 0 12px; font-size: 16px; }
        p { margin: 0; }
        .toolbar {
          display: flex;
          align-items: center;
          justify-content: space-between;
          gap: 16px;
          margin-bottom: 16px;
        }
        .status {
          display: inline-flex;
          align-items: center;
          gap: 8px;
          min-height: 32px;
          padding: 6px 10px;
          border: 1px solid #d7dce5;
          border-radius: 6px;
          background: white;
          font-size: 13px;
        }
        .dot { width: 9px; height: 9px; border-radius: 999px; background: #9ca3af; }
        .dot.ready { background: #168a4a; }
        .dot.down { background: #c2410c; }
        .chat, aside {
          border: 1px solid #d7dce5;
          border-radius: 8px;
          background: white;
        }
        .messages {
          min-height: 420px;
          max-height: calc(100vh - 210px);
          overflow: auto;
          padding: 16px;
        }
        .message {
          width: fit-content;
          max-width: 82%;
          margin: 0 0 12px;
          padding: 10px 12px;
          border-radius: 8px;
          white-space: pre-wrap;
        }
        .message.user {
          margin-left: auto;
          background: #1d4ed8;
          color: white;
        }
        .message.assistant {
          background: #eef2f7;
          color: #172033;
        }
        .notation {
          margin-top: 10px;
          padding: 10px;
          border: 1px solid #d7dce5;
          border-radius: 6px;
          background: #fff;
          overflow-x: auto;
        }
        .notation svg {
          display: block;
          max-width: none;
        }
        .notation-error {
          margin-top: 8px;
          padding: 8px;
          border: 1px solid #f1b8a2;
          border-radius: 6px;
          background: #fff7ed;
          color: #9a3412;
          font-size: 12px;
          white-space: pre-wrap;
        }
        .meta {
          margin-top: 8px;
          color: #5b6575;
          font-size: 12px;
        }
        .trace {
          margin-top: 8px;
          border-top: 1px solid #d7dce5;
          padding-top: 8px;
          color: #374151;
          font-size: 12px;
        }
        .trace summary { cursor: pointer; font-weight: 650; }
        .trace ol { margin: 8px 0 0 18px; padding: 0; }
        .trace li { margin: 0 0 6px; }
        .trace code {
          display: block;
          margin-top: 2px;
          white-space: pre-wrap;
          overflow-wrap: anywhere;
          color: #5b6575;
          font-family: ui-monospace, SFMono-Regular, Consolas, "Liberation Mono", monospace;
        }
        form {
          display: grid;
          grid-template-columns: minmax(0, 1fr) auto;
          gap: 10px;
          padding: 14px;
          border-top: 1px solid #d7dce5;
        }
        textarea {
          width: 100%;
          min-height: 48px;
          max-height: 160px;
          resize: vertical;
          border: 1px solid #c7ceda;
          border-radius: 6px;
          padding: 10px;
          font: inherit;
        }
        button {
          min-width: 88px;
          border: 0;
          border-radius: 6px;
          background: #1f6feb;
          color: white;
          font: inherit;
          font-weight: 650;
          cursor: pointer;
        }
        button:disabled { background: #94a3b8; cursor: wait; }
        aside { padding: 16px; height: fit-content; }
        .examples { display: grid; gap: 8px; }
        .example {
          width: 100%;
          min-height: 0;
          padding: 8px;
          border: 1px solid #d7dce5;
          background: #f8fafc;
          color: #172033;
          text-align: left;
          font-size: 13px;
          font-weight: 500;
        }
        .links {
          display: grid;
          gap: 8px;
          margin-top: 18px;
          font-size: 13px;
        }
        a { color: #1d4ed8; }
        @media (max-width: 820px) {
          main { grid-template-columns: 1fr; margin: 16px auto; }
          .toolbar { align-items: flex-start; flex-direction: column; }
          .messages { min-height: 360px; max-height: none; }
        }
      </style>
    </head>
    <body>
      <main>
        <section>
          <div class="toolbar">
            <div>
              <h1>GA Chatbot</h1>
              <p>Ask grounded music theory questions through the local chatbot API.</p>
            </div>
            <div class="status" aria-live="polite"><span id="statusDot" class="dot"></span><span id="statusText">Checking...</span></div>
          </div>
          <div class="chat">
            <div id="messages" class="messages" aria-live="polite">
              <div class="message assistant">Ready. Try a theory question or use one of the examples.</div>
            </div>
            <form id="chatForm">
              <textarea id="messageInput" placeholder="Ask about chords, scales, voice leading, or set-class algebra..." required>Are 0146 and 0137 z-related?</textarea>
              <button id="sendButton" type="submit">Send</button>
            </form>
          </div>
        </section>
        <aside>
          <h2>Examples</h2>
          <div id="examples" class="examples"></div>
          <div class="links">
            <a href="api/chatbot/status">Status JSON</a>
            <a href="api/chatbot/examples">Examples JSON</a>
            <a href="api">API metadata</a>
          </div>
        </aside>
      </main>
      <script src="vendor/vexflow/vexflow.js"></script>
      <script>
        const form = document.getElementById('chatForm');
        const input = document.getElementById('messageInput');
        const sendButton = document.getElementById('sendButton');
        const messages = document.getElementById('messages');
        const statusDot = document.getElementById('statusDot');
        const statusText = document.getElementById('statusText');
        const examples = document.getElementById('examples');
        const history = [];
        const CHAT_CLIENT_TIMEOUT_MS = 190000;
        let activeController = null;
        let activeTimer = null;
        let activeTimeout = null;
        let activeStartedAt = 0;
        const notationFencePattern = /```(?:vextab|vexflow|tab)\s*\n([\s\S]*?)```/gi;

        function addMessage(role, text, meta, trace) {
          const node = document.createElement('div');
          node.className = `message ${role}`;
          renderMessageContent(node, text);
          if (meta) {
            const metaNode = document.createElement('div');
            metaNode.className = 'meta';
            metaNode.textContent = meta;
            node.appendChild(metaNode);
          }
          if (trace?.steps?.length) {
            const traceNode = document.createElement('details');
            traceNode.className = 'trace';
            const summary = document.createElement('summary');
            summary.textContent = `Agentic trace - ${trace.steps.length} steps`;
            traceNode.appendChild(summary);

            const list = document.createElement('ol');
            for (const step of trace.steps) {
              const item = document.createElement('li');
              const title = document.createElement('div');
              title.textContent = `${step.name} - ${step.status} - ${step.elapsedMs}ms`;
              item.appendChild(title);
              if (step.attributes) {
                const attrs = document.createElement('code');
                attrs.textContent = JSON.stringify(step.attributes, null, 2);
                item.appendChild(attrs);
              }
              list.appendChild(item);
            }

            const ids = document.createElement('code');
            ids.textContent = `traceId=${trace.traceId}\nrunId=${trace.runId}\nprotocol=${trace.protocol}`;
            traceNode.appendChild(ids);
            traceNode.appendChild(list);
            node.appendChild(traceNode);
          }
          messages.appendChild(node);
          renderPendingNotation(node);
          messages.scrollTop = messages.scrollHeight;
        }

        function renderMessageContent(node, text) {
          notationFencePattern.lastIndex = 0;
          let cursor = 0;
          let match;
          while ((match = notationFencePattern.exec(text)) !== null) {
            appendText(node, text.slice(cursor, match.index));

            const notationNode = document.createElement('div');
            notationNode.className = 'notation';
            notationNode.dataset.notation = match[1].trim();
            node.appendChild(notationNode);

            cursor = match.index + match[0].length;
          }

          appendText(node, text.slice(cursor));
        }

        function appendText(node, text) {
          if (text) {
            node.appendChild(document.createTextNode(text));
          }
        }

        function renderPendingNotation(root) {
          const notationNodes = root.querySelectorAll('.notation[data-notation]');
          for (const notationNode of notationNodes) {
            try {
              renderTabNotation(notationNode, notationNode.dataset.notation || '');
            } catch (error) {
              notationNode.replaceChildren();
              const errorNode = document.createElement('div');
              errorNode.className = 'notation-error';
              errorNode.textContent = `Could not render notation: ${error.message}`;
              notationNode.appendChild(errorNode);
            }
          }
        }

        function renderTabNotation(container, notation) {
          if (!window.Vex?.Flow) {
            throw new Error('VexFlow is not loaded');
          }

          const positions = parseTabPositions(notation);
          if (positions.length === 0) {
            throw new Error('No tab positions found');
          }

          container.replaceChildren();
          const VF = window.Vex.Flow;
          const width = Math.max(520, positions.length * 58);
          const height = 150;
          const renderer = new VF.Renderer(container, VF.Renderer.Backends.SVG);
          renderer.resize(width, height);

          const context = renderer.getContext();
          const stave = new VF.TabStave(10, 36, width - 20);
          stave.addClef('tab').setContext(context).draw();

          const notes = positions.map(position => new VF.TabNote({
            positions: [position],
            duration: 'q'
          }));

          const voice = new VF.Voice({ num_beats: notes.length, beat_value: 4 });
          voice.addTickables(notes);

          const formatter = new VF.Formatter();
          formatter.joinVoices([voice]);
          formatter.format([voice], width - 90);
          voice.draw(context, stave);
        }

        function parseTabPositions(notation) {
          const tokenPattern = /(\d{1,2})\/(\d{1,2})/g;
          const preferFretString = /\b(?:tabstave|notes)\b/i.test(notation);
          const positions = [];
          let match;

          while ((match = tokenPattern.exec(notation)) !== null) {
            const left = Number.parseInt(match[1], 10);
            const right = Number.parseInt(match[2], 10);
            const position = normalizeTabPosition(left, right, preferFretString);
            if (position) {
              positions.push(position);
            }
          }

          return positions;
        }

        function normalizeTabPosition(left, right, preferFretString) {
          const primary = preferFretString
            ? { str: right, fret: left }
            : { str: left, fret: right };
          if (isValidTabPosition(primary)) {
            return primary;
          }

          const fallback = preferFretString
            ? { str: left, fret: right }
            : { str: right, fret: left };
          return isValidTabPosition(fallback) ? fallback : null;
        }

        function isValidTabPosition(position) {
          return Number.isInteger(position.str)
            && Number.isInteger(position.fret)
            && position.str >= 1
            && position.str <= 6
            && position.fret >= 0
            && position.fret <= 36;
        }

        async function refreshStatus() {
          try {
            const response = await fetch('api/chatbot/status');
            const status = await response.json();
            statusDot.className = `dot ${status.isAvailable ? 'ready' : 'down'}`;
            statusText.textContent = status.message || (status.isAvailable ? 'Available' : 'Unavailable');
          } catch {
            statusDot.className = 'dot down';
            statusText.textContent = 'API unavailable';
          }
        }

        async function loadExamples() {
          try {
            const response = await fetch('api/chatbot/examples');
            const items = await response.json();
            examples.replaceChildren(...items.map(text => {
              const button = document.createElement('button');
              button.type = 'button';
              button.className = 'example';
              button.textContent = text;
              button.addEventListener('click', () => {
                input.value = text;
                input.focus();
              });
              return button;
            }));
          } catch {
            examples.textContent = 'Examples unavailable.';
          }
        }

        sendButton.addEventListener('click', event => {
          if (!activeController) return;

          event.preventDefault();
          activeController.abort();
        });

        form.addEventListener('submit', async event => {
          event.preventDefault();
          if (activeController) {
            activeController.abort();
            return;
          }

          const message = input.value.trim();
          if (!message) return;

          addMessage('user', message);
          input.value = '';
          activeController = new AbortController();
          activeStartedAt = Date.now();
          sendButton.disabled = false;
          sendButton.textContent = 'Stop';
          statusText.textContent = 'Running... 0s';
          activeTimer = window.setInterval(() => {
            const elapsedSeconds = Math.floor((Date.now() - activeStartedAt) / 1000);
            statusText.textContent = `Running... ${elapsedSeconds}s`;
          }, 1000);
          activeTimeout = window.setTimeout(() => {
            activeController?.abort();
          }, CHAT_CLIENT_TIMEOUT_MS);

          try {
            const response = await fetch('api/chatbot/chat', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ message, conversationHistory: history }),
              signal: activeController.signal
            });

            if (!response.ok) {
              const body = await response.text();
              throw new Error(body || `HTTP ${response.status}`);
            }

            const result = await response.json();
            const answer = result.naturalLanguageAnswer || '(empty response)';
            const meta = `${result.agentId || 'unknown'} via ${result.routingMethod || 'unknown'} - ${result.elapsedMs ?? '?'}ms`;
            addMessage('assistant', answer, meta, result.trace);
            history.push({ role: 'user', content: message }, { role: 'assistant', content: answer });
          } catch (error) {
            const elapsedMs = Date.now() - activeStartedAt;
            const timedOut = elapsedMs >= CHAT_CLIENT_TIMEOUT_MS - 1000;
            const failureMessage = error.name === 'AbortError'
              ? (timedOut
                  ? `Request timed out after ${Math.round(CHAT_CLIENT_TIMEOUT_MS / 1000)}s.`
                  : 'Request cancelled.')
              : `Request failed: ${error.message}`;
            addMessage('assistant', failureMessage);
          } finally {
            window.clearInterval(activeTimer);
            window.clearTimeout(activeTimeout);
            activeController = null;
            activeTimer = null;
            activeTimeout = null;
            sendButton.disabled = false;
            sendButton.textContent = 'Send';
            input.focus();
            refreshStatus();
          }
        });

        refreshStatus();
        loadExamples();
      </script>
    </body>
    </html>
    """,
    "text/html"));

app.MapGet("/api", () => Results.Ok(new
{
    service = "ga-chatbot-api",
    version = "0.1.0",
    description = "Thin Guitar Alchemist chatbot API host"
}));

app.Run();

public partial class Program;
