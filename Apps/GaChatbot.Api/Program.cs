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
    // slash) resolves wwwroot/index.html's relative URLs against the
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
app.UseDefaultFiles();
app.UseStaticFiles();

if (allowedOrigins.Length > 0)
{
    app.UseCors("ChatbotClient");
}

app.MapControllers();

app.MapGet("/api", () => Results.Ok(new
{
    service = "ga-chatbot-api",
    version = "0.1.0",
    description = "Thin Guitar Alchemist chatbot API host"
}));

app.Run();

public partial class Program;
