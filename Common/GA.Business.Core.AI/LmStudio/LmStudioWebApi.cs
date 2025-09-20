namespace GA.Business.Core.AI.LmStudio;

/// <summary>
/// Web API for LM Studio integration
/// </summary>
public static class LmStudioWebApi
{
    /// <summary>
    /// Starts the LM Studio Web API
    /// </summary>
    public static async Task StartAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        
        // Add MongoDB and embedding services
        // builder.Services.AddMongoDbServices();
        
        // Add LM Studio integration services
        builder.Services.AddLmStudioIntegration();
        
        // Add CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowAll");
        app.UseAuthorization();
        app.MapControllers();

        await app.RunAsync();
    }
}
