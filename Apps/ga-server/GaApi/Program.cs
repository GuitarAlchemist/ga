using System.Threading.RateLimiting;
using AllProjects.ServiceDefaults;
using Microsoft.AspNetCore.HttpOverrides;
using GA.Business.Core.Session;
using GaApi.Extensions;
using GaApi.Hubs;
using GaApi.Controllers;
using GaApi.Services;
using GaApi.GraphQL.Queries;
using Microsoft.AspNetCore.RateLimiting;
using MudBlazor;
using MudBlazor.Services;
using Path = System.IO.Path;

var builder = WebApplication.CreateBuilder(args);

// Add shared configuration
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "../../appsettings.Shared.json"), true, true);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Ensure user-secrets are loaded in Development (Aspire defaults may not include
// them, and GenerateAssemblyInfo=false in the .csproj suppresses the auto-emitted
// UserSecretsIdAttribute — so the generic variant can't find the ID).
if (builder.Environment.IsDevelopment())
    builder.Configuration.AddUserSecrets(userSecretsId: "0c749f53-3fda-4099-a8d4-ee77ffdd1913", reloadOnChange: false);

// Add Redis distributed cache (Aspire integration)
builder.AddRedisDistributedCache("redis");

// Add services to the container.

// Register core infrastructure (MongoDB, ILGPU, Redis)
builder.Services.AddGaInfrastructure(builder.Configuration);

// Register AI services (Ollama, Embeddings, Vector Search, Chatbot)
builder.Services.AddAiServices(builder.Configuration);

// Register voicing search services (GPU-accelerated semantic search for guitar voicings)
builder.Services.AddVoicingSearchServices(builder.Configuration);

// Add caching services
builder.Services.AddCachingServices(builder.Configuration);

// Register monadic services (health check, chords)
builder.Services.AddMonadicHealthCheckService();
builder.Services.AddMonadicChordService();

// Register standard health check service (used by HealthController)
builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();

// Register contextual chord services
builder.Services.AddSingleton<ContextualChordService>();
builder.Services.AddSingleton<VoicingFilterService>();

// Shared LLM concurrency gate (3 parallel calls) — applied to both hub and REST controller
builder.Services.AddSingleton<ILlmConcurrencyGate, LlmConcurrencyGate>();

// Add session context provider (scoped = one per HTTP request)
builder.Services.AddSessionContextScoped();

// Add HTTP client for external services
builder.Services.AddHttpClient();

// Add SignalR for WebSocket support
builder.Services.AddSignalR();

// Belief state service — reads/updates tetravalent belief files
builder.Services.AddSingleton<BeliefStateService>();

// Visual critic — Claude vision analysis of Prime Radiant screenshots
builder.Services.AddSingleton<VisualCriticService>();

// Pipeline execution — runs brainstorm/plan/build/review/compound via Claude Code CLI
builder.Services.AddSingleton<PipelineExecutionService>();

// Governance file watcher — pushes updates via SignalR when governance files change
builder.Services.AddHostedService<GovernanceWatcherService>();

// Register GovernanceController for DI (used by GovernanceHub)
builder.Services.AddTransient<GovernanceController>();

builder.Services.AddControllers()
    .ConfigureApplicationPartManager(manager =>
    {
        // GA.Fretboard.Service is referenced for shared types but runs as its own service.
        // Exclude its assembly from controller discovery to prevent AmbiguousMatchException
        // (e.g., both assemblies define ContextualChordsController on the same route prefix).
        var fretboardPart = manager.ApplicationParts
            .FirstOrDefault(p => p.Name == "GA.Fretboard.Service");
        if (fretboardPart != null)
            manager.ApplicationParts.Remove(fretboardPart);
    });

// Add Blazor Server and MudBlazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
});

// Add YARP Reverse Proxy for API Gateway
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add GraphQL Server
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddTypeExtension<ChordNamingQuery>()
    .AddTypeExtension<MusicTheoryQuery>()
    .AddTypeExtension<DomainSchemaQuery>()
    .AddTypeExtension<MongoCollectionsQuery>()
    .AddProjections()
    .AddFiltering()
    .AddSorting();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Fix schema ID collisions by using full type names (including namespace)
    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    c.SwaggerDoc("v1", new()
    {
        Title = "Guitar Alchemist Chatbot API",
        Version = "v1",
        Description = @"RESTful API for Guitar Alchemist Chatbot",
        Contact = new()
        {
            Name = "Guitar Alchemist",
            Url = new("https://github.com/GuitarAlchemist/ga")
        }
    });

    c.EnableAnnotations();
});

// Add CORS support with WebSocket support
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173", "http://localhost:5174", "http://localhost:5176", "http://localhost:5177", "http://localhost:8501",
                "https://demos.guitaralchemist.com", "https://guitaralchemist.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR WebSocket connections + OAuth refresh cookies
    });
});

// ─── OAuth2 + JWT authentication ─────────────────────────────────────────
// Configuration path: Authentication:* (user-secrets in dev, env vars in prod)
// See C:/Users/spare/source/repos/ga/Apps/ga-server/GaApi/Properties/launchSettings.json
builder.Services.Configure<GaApi.Services.JwtSettings>(
    builder.Configuration.GetSection("Authentication:Jwt"));
builder.Services.Configure<GaApi.Services.AuthOwnerSettings>(
    builder.Configuration.GetSection("Authentication"));
builder.Services.AddSingleton<GaApi.Services.UserService>();
builder.Services.AddSingleton<GaApi.Services.TokenService>();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var githubClientId = builder.Configuration["Authentication:GitHub:ClientId"];
var githubClientSecret = builder.Configuration["Authentication:GitHub:ClientSecret"];
var jwtSigningKey = builder.Configuration["Authentication:Jwt:SigningKey"];

// JWT is the default scheme for API auth. External providers are added as
// extra schemes used only during the OAuth challenge/callback dance.
var authBuilder = builder.Services
    .AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (!string.IsNullOrWhiteSpace(jwtSigningKey))
        {
            var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                Convert.FromBase64String(jwtSigningKey));
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Authentication:Jwt:Issuer"] ?? "ga-server",
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Authentication:Jwt:Audience"] ?? "ga-clients",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        }
        // Allow SignalR clients to send JWT via access_token query param on WebSocket handshake
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            },
        };
    })
    .AddCookie(); // ephemeral cookie used during the OAuth round-trip only

if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
        // Route the OAuth callback through /api/* so Cloudflare Tunnel forwards it to the
        // backend. The default /signin-google path gets swallowed by the SPA catch-all
        // route and returns the React app instead of being handled by the Google middleware.
        options.CallbackPath = "/api/auth/signin-google";
    });
}

if (!string.IsNullOrWhiteSpace(githubClientId) && !string.IsNullOrWhiteSpace(githubClientSecret))
{
    authBuilder.AddGitHub(options =>
    {
        options.ClientId = githubClientId;
        options.ClientSecret = githubClientSecret;
        options.SignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
        options.Scope.Add("user:email");
    });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
});

builder.Services.AddProblemDetails();

// Per-IP rate limiting: 300 requests/minute to support Prime Radiant multi-panel polling
// (health check every 30s + N panels × refresh intervals + governance queries + grammar API)
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        // Exempt health/status endpoints from rate limiting
        var path = ctx.Request.Path.Value ?? "";
        if (path.Contains("/status") || path.Contains("/health"))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }
        return RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? ctx.Request.Headers.Host.ToString(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();


// Configure the HTTP request pipeline.

// Add custom middleware (order matters!)

// Trust X-Forwarded-* headers from the Cloudflare tunnel / reverse proxy so that
// Request.Scheme, Request.Host, and generated OAuth redirect URIs match the public
// origin (demos.guitaralchemist.com) instead of the upstream Kestrel host (localhost:5232).
// Must run before UseAuthentication so OAuth middleware sees the corrected scheme/host.
//
// Safety: we only apply forwarded headers when X-Forwarded-Host matches the known public
// origin. This prevents partial/inconsistent header sets (e.g. Vite dev proxy sending only
// X-Forwarded-Proto=https) from producing bogus URIs like https://localhost:5232/.
const string PublicHost = "demos.guitaralchemist.com";
app.Use(async (ctx, next) =>
{
    // Cloudflare Tunnel (cloudflared) sets X-Forwarded-Proto + CF-Connecting-IP, but does
    // NOT set X-Forwarded-Host — so we can't rely on the standard ForwardedHeaders
    // middleware to recover the public origin. Detect CF-routed requests via CF headers
    // and synthesise X-Forwarded-Host = demos.guitaralchemist.com so downstream code
    // (OAuth redirect URI generation) produces the correct public URL.
    var isCloudflareRequest = !string.IsNullOrEmpty(ctx.Request.Headers["CF-Connecting-IP"].ToString());
    if (isCloudflareRequest)
    {
        ctx.Request.Headers["X-Forwarded-Host"] = PublicHost;
    }
    else
    {
        // Strip any stray forwarded headers on direct/localhost requests so generated URIs
        // match the actual Kestrel host (localhost:5232) — avoids https://localhost:5232
        // bogus URIs from partial header sets (e.g. Vite dev proxy sending only Proto).
        ctx.Request.Headers.Remove("X-Forwarded-Proto");
        ctx.Request.Headers.Remove("X-Forwarded-Host");
        ctx.Request.Headers.Remove("X-Forwarded-For");
    }
    await next();
});
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    AllowedHosts = { PublicHost },
};
// Cloudflare can connect from any IP — clear the default localhost-only allowlist.
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Guitar Alchemist API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Guitar Alchemist API Documentation";
    });
}


// Use CORS
app.UseCors("AllowAll");

// Use rate limiting (must be before UseAuthorization)
app.UseRateLimiter();

// Authentication + Authorization (JWT for APIs, OAuth cookie for the /api/auth flow)
app.UseAuthentication();
app.UseAuthorization();

// Dev-only guard: block unauthenticated access to the chatbot API in non-Development environments.
// CORS + rate limiting provide weak protection; this gate prevents accidental public exposure
// while full bearer auth is designed. Replace with [Authorize] + AddAuthentication once a
// token/key issuance strategy is in place (see todo 036).
if (!app.Environment.IsDevelopment())
{
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api/chatbot")
            && !ctx.Request.Headers.ContainsKey("X-Api-Key"))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        await next(ctx);
    });
}

// Enable static files for Blazor
app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();

app.MapGraphQL();

app.MapGet("/api/stats", async (VectorSearchService vs) => Results.Ok(await vs.GetStatsAsync())).WithName("GetStats");
// /stats (without /api prefix) kept for backwards compatibility with older clients
app.MapGet("/stats", async (VectorSearchService vs) => Results.Ok(await vs.GetStatsAsync())).WithName("GetStatsRoot");

// Map YARP Reverse Proxy routes (API Gateway)
app.MapReverseProxy();

// Map SignalR hubs
app.MapHub<ChatbotHub>("/hubs/chatbot");
app.MapHub<GovernanceHub>("/hubs/governance");
app.MapHub<PipelineHub>("/hubs/pipeline");

// Map Aspire default endpoints (health checks, liveness)
app.MapDefaultEndpoints();

// Add API info endpoint
app.MapGet("/api", () => new
{
    message = "Guitar Alchemist Chatbot API",
    version = "1.0.0",
    timestamp = DateTime.UtcNow,
    documentation = "/swagger"
});

app.Run();

namespace GaApi
{
    public partial class Program
    {
    }
}
