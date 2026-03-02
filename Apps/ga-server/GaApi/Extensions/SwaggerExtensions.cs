namespace GaApi.Extensions;

using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using Path = System.IO.Path;

/// <summary>
///     Extensions for configuring Swagger/OpenAPI documentation
/// </summary>
public static class SwaggerExtensions
{
    private static string GetControllerName(string? controllerName) =>
        controllerName switch
        {
            "MusicalKnowledge" => "🎼 Musical Knowledge",
            "ChordProgressions" => "🎵 Chord Progressions",
            "GuitarTechniques" => "🎸 Guitar Techniques",
            "SpecializedTunings" => "🎛️ Specialized Tunings",
            "Analytics" => "📊 Analytics & Insights",
            "UserPersonalization" => "👤 User Personalization",
            _ => controllerName ?? "API"
        };

    /// <summary>
    ///     Add comprehensive Swagger configuration
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new()
            {
                Title = "Guitar Alchemist Musical Knowledge API",
                Version = "v1",
                Description = @"
# Guitar Alchemist Musical Knowledge API

A comprehensive REST API for accessing musical knowledge from YAML configurations, including:

- **Iconic Chords**: Famous chords from music history with theoretical analysis
- **Chord Progressions**: Common and advanced harmonic progressions across genres
- **Guitar Techniques**: Playing techniques from basic to advanced levels
- **Specialized Tunings**: Alternative tuning systems and configurations
- **Analytics**: Musical relationship analysis and recommendations
- **Personalization**: User profiles, learning paths, and customized recommendations

## Features

### 🎵 Musical Knowledge Access
- Search across all musical concepts
- Filter by category, difficulty, artist, genre
- Get detailed theoretical information
- Find related concepts and progressions

### 📊 Advanced Analytics
- Musical relationship analysis
- Usage pattern insights
- Personalized recommendations
- Learning progression paths

### 👤 User Personalization
- Custom user profiles and preferences
- Personalized learning paths
- Progress tracking and statistics
- Adaptive recommendations

### ⚡ Performance & Reliability
- Database caching for fast access
- Real-time configuration updates
- Comprehensive validation
- Detailed logging and monitoring

## Getting Started

1. **Explore Musical Concepts**: Start with `/api/MusicalKnowledge/search?query=jazz`
2. **Get Statistics**: Check `/api/MusicalKnowledge/statistics` for overview
3. **Browse by Category**: Use `/api/MusicalKnowledge/category/{category}`
4. **Create User Profile**: POST to `/api/UserPersonalization/profile`
5. **Get Recommendations**: Use `/api/Analytics/recommendations/{userId}`

## Authentication

Currently, the API is open for exploration. User-specific endpoints require a valid user ID.

## Rate Limiting

Standard rate limiting applies. Contact support for higher limits.
",
                Contact = new()
                {
                    Name = "Guitar Alchemist Team",
                    Email = "support@guitaralchemist.com",
                    Url = new("https://guitaralchemist.com")
                },
                License = new()
                {
                    Name = "MIT License",
                    Url = new("https://opensource.org/licenses/MIT")
                }
            });

            // Add XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }

            // Add examples and schemas
            // c.EnableAnnotations(); // Requires Swashbuckle.AspNetCore.Annotations package
            c.UseInlineDefinitionsForEnums();

            // Group endpoints by tags
            c.TagActionsBy(api => [GetControllerName(api.ActionDescriptor.RouteValues["controller"])]);

            // Add security definitions (for future authentication)
            c.AddSecurityDefinition("Bearer", new()
            {
                Description =
                    "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            // Custom operation filters
            c.OperationFilter<SwaggerExampleOperationFilter>();
            c.DocumentFilter<SwaggerTagDescriptionFilter>();
        });

        return services;
    }

    /// <summary>
    ///     Configure Swagger UI with custom settings
    /// </summary>
    public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
    {
        app.UseSwagger(c => { c.RouteTemplate = "api-docs/{documentName}/swagger.json"; });

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/api-docs/v1/swagger.json", "Guitar Alchemist Musical Knowledge API v1");
            c.RoutePrefix = "api-docs";
            c.DocumentTitle = "Guitar Alchemist API Documentation";

            // Custom CSS and JavaScript
            c.InjectStylesheet("/swagger-ui/custom.css");
            c.InjectJavascript("/swagger-ui/custom.js");

            // UI configuration
            c.DefaultModelsExpandDepth(2);
            c.DefaultModelExpandDepth(2);
            c.DocExpansion(DocExpansion.List);
            c.EnableDeepLinking();
            c.EnableFilter();
            c.EnableValidator();
            c.ShowExtensions();
            c.ShowCommonExtensions();

            // Try it out configuration
            c.SupportedSubmitMethods(
                SubmitMethod.Get,
                SubmitMethod.Post,
                SubmitMethod.Put,
                SubmitMethod.Delete
            );
        });

        return app;
    }
}
