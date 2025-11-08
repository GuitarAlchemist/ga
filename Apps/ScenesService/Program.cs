using System.Text.Json;
using Microsoft.Net.Http.Headers;
using MongoDB.Driver;
using ScenesService.Models;
using ScenesService.Services;

var builder = WebApplication.CreateBuilder(args);

// CORS pour ton front local (ajuste l'origine)
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173", "http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders(HeaderNames.ETag)));

// cache + compression (optionnel)
builder.Services.AddResponseCompression();
builder.Services.AddResponseCaching();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Scenes Service API",
        Version = "v1",
        Description =
            "Backend de génération et diffusion de scènes GLB avec métadonnées cells/portals, persistance MongoDB/GridFS, et scheduler de builds."
    });
});

// MongoDB setup
var mongoUrl = builder.Configuration["MONGO_URL"] ?? "mongodb://localhost:27017/";
var mongo = new MongoClient(mongoUrl).GetDatabase("scenes_db");

builder.Services.AddSingleton(mongo);
builder.Services.AddSingleton<ISceneStore>(sp => new MongoSceneStore(mongoUrl));
builder.Services.AddSingleton<IJobStore, MongoJobStore>();
builder.Services.AddSingleton<ICancelStore, MongoCancelStore>();
builder.Services.AddSingleton<GlbSceneBuilder>();
builder.Services.AddHostedService<SceneBuildWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.UseCors();

// Build endpoint (direct build)
app.MapPost("/scenes/build", async (HttpContext context, GlbSceneBuilder builder, ISceneStore store) =>
    {
        try
        {
            // Read the raw body and parse as JSON
            using var reader = new StreamReader(context.Request.Body);
            var json = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(json))
            {
                return Results.BadRequest("Request body is empty");
            }

            var req = JsonSerializer.Deserialize<SceneBuildRequestDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (req == null)
            {
                return Results.BadRequest("Invalid request body");
            }

            var (path, bytes, etag) = await builder.BuildGlbAsync(req, store);
            return Results.Ok(new SceneBuildResponseDto(req.SceneId, path, etag, bytes));
        }
        catch (JsonException ex)
        {
            return Results.BadRequest($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Results.Problem($"Error building scene: {ex.Message}");
        }
    })
    .WithName("BuildScene")
    .WithOpenApi();

// Serve .glb with ETag + Range
app.MapGet("/scenes/{id}.glb",
    async (string id, HttpRequest req, HttpResponse res, ISceneStore store, CancellationToken ct) =>
    {
        var head = await store.HeadAsync(id, ct);
        if (head is null)
        {
            return Results.NotFound();
        }

        res.Headers.ETag = $"\"{head.Value.etag}\"";
        res.Headers.AcceptRanges = "bytes";

        // 304 ?
        if (req.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var inm) &&
            inm.ToString().Trim('"') == head.Value.etag)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        var opened = await store.OpenReadAsync(id, ct);
        if (opened is null)
        {
            return Results.NotFound();
        }

        var (stream, length, etag, lastMod) = opened.Value;
        // ASP.NET sait gérer Range si Stream.CanSeek == true
        return Results.Stream(stream, "model/gltf-binary",
            lastModified: lastMod.UtcDateTime,
            enableRangeProcessing: true);
    });

app.MapGet("/scenes/{id}/meta", async (string id, ISceneStore store, CancellationToken ct) =>
{
    var json = await store.MetaJsonAsync(id, ct);
    return json is null ? Results.NotFound() : Results.Text(json, "application/json");
});

// Job endpoints
app.MapPost("/jobs/enqueue", async (HttpContext context, IJobStore jobs, CancellationToken ct) =>
{
    var req = await context.Request.ReadFromJsonAsync<EnqueueBuildRequestDto>(ct);
    if (req == null)
    {
        return Results.BadRequest("Invalid request body");
    }

    // on réutilise SceneBuildRequestDto (même schéma que le builder)
    var sceneReq = new SceneBuildRequestDto(req.SceneId, req.Cells, req.Portals, req.Materials, req.Props);
    var id = await jobs.EnqueueAsync(sceneReq, ct);
    return Results.Ok(new { jobId = id });
});

app.MapGet("/jobs/{id}", async (string id, IJobStore jobs, CancellationToken ct) =>
{
    var job = await jobs.GetAsync(id, ct);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

app.MapPost("/jobs/{id}/cancel", async (string id, ICancelStore cancel, IJobStore jobs, CancellationToken ct) =>
{
    var j = await jobs.GetAsync(id, ct);
    if (j is null)
    {
        return Results.NotFound();
    }

    await cancel.CancelAsync(id, ct);
    return Results.Accepted($"/jobs/{id}");
});

app.MapGet("/jobs", async (IMongoDatabase db, int take = 50, CancellationToken ct = default) =>
{
    var list = await db.GetCollection<BuildJobDto>("scene_jobs")
        .Find(FilterDefinition<BuildJobDto>.Empty)
        .SortByDescending(j => j.CreatedUtc)
        .Limit(take)
        .ToListAsync(ct);
    return Results.Ok(list);
});

app.Run();
