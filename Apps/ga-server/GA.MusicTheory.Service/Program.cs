using GA.MusicTheory.Service.Models;
using GA.MusicTheory.Service.Services;
using System.Reflection;
using AllProjects.ServiceDefaults;
using Hellang.Middleware.ProblemDetails;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add Redis distributed caching
builder.AddRedisDistributedCache("redis");

// Add controllers

// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB"));

// Register services
builder.Services.AddSingleton<MongoDbService>();

builder.Services.AddControllers();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "MusicTheory Service",
        Version = "v1",
        Description = "Music theory operations (keys, modes, scales, intervals, chords)"
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    c.EnableAnnotations();
});

// Add problem details middleware
builder.Services.AddProblemDetails(options =>
{
    options.IncludeExceptionDetails = (_, _) => builder.Environment.IsDevelopment();
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

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

// Configure middleware pipeline
app.UseProblemDetails();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MusicTheory Service v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowAll");
app.UseRateLimiter();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
