using FloorManager.Components;
using FloorManager.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add HttpClient for API calls with Aspire service discovery
builder.Services.AddHttpClient("GaApi", client =>
    {
        client.BaseAddress = new Uri("http://gaapi"); // Aspire service discovery
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddServiceDiscovery(); // Enable Aspire service discovery

// Add floor service
builder.Services.AddScoped<FloorService>();

// Add monadic floor service (temporarily disabled due to type inference issues)
// builder.Services.AddScoped<FloorManager.Services.MonadicFloorService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map Aspire default endpoints (health checks, liveness)
app.MapDefaultEndpoints();

app.Run();
