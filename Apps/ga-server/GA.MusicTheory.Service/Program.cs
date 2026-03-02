#pragma warning disable SKEXP0001
using System.Reflection;
using Hellang.Middleware.ProblemDetails;
using System.Text.Json;
using System.Threading.RateLimiting;
using GA.MusicTheory.Service.Models;
using GA.MusicTheory.Service.Services;
using AllProjects.ServiceDefaults;
using GA.Infrastructure.Documentation;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults
builder.AddServiceDefaults();

// Add Redis distributed caching
builder.AddRedisDistributedCache("redis");

// Add controllers
builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; });

// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB"));

// Register services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<SchemaDiscoveryService>();
builder.Services.AddScoped<IMonadicChordService, MonadicChordService>();
builder.Services.AddScoped<IMonadicHealthCheckService, MonadicHealthCheckService>();

builder.Services.AddMemoryCache();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Music Theory Service",
        Version = "v1",
        Description = "Music theory logic, chord classification, and metadata"
    });

    c.EnableAnnotations();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add problem details middleware
builder.Services.AddProblemDetails(options =>
{
    options.IncludeExceptionDetails = (ctx, ex) => builder.Environment.IsDevelopment();
    options.MapToStatusCode<NotImplementedException>(StatusCodes.Status501NotImplemented);
    options.MapToStatusCode<ArgumentException>(StatusCodes.Status400BadRequest);
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// builder.Services.AddRateLimiter(options => ...);

var app = builder.Build();

// Configure middleware pipeline
app.UseProblemDetails();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Music Theory Service v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowAll");
// app.UseRateLimiter();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
