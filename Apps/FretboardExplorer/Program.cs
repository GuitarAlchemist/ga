using FretboardExplorer.Components;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor services
builder.Services.AddMudServices();

// Add HTTP client for GaApi
builder.Services.AddHttpClient("GaApi", client =>
{
    var gaApiUrl = builder.Configuration["GaApi:BaseUrl"] ?? "https://localhost:7001";
    client.BaseAddress = new Uri(gaApiUrl);
});

// TODO: Add GraphQL client when schema is ready
// builder.Services
//     .AddFretboardExplorerClient()
//     .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://localhost:7001/graphql"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseStaticFiles();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map Aspire default endpoints (health checks, liveness)
app.MapDefaultEndpoints();

app.Run();
