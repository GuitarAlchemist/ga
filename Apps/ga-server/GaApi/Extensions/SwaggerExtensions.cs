namespace GaApi.Extensions;

using System.Reflection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using Path = System.IO.Path;

/// <summary>
///     Extensions for configuring Swagger/OpenAPI documentation
/// </summary>
public static class SwaggerExtensions
{
    /// <summary>
    ///     Add comprehensive Swagger configuration
    /// </summary>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
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
                Contact = new OpenApiContact
                {
                    Name = "Guitar Alchemist Team",
                    Email = "support@guitaralchemist.com",
                    Url = new Uri("https://guitaralchemist.com")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
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
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
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

    private static string GetControllerName(string? controllerName)
    {
        return controllerName switch
        {
            "MusicalKnowledge" => "🎼 Musical Knowledge",
            "ChordProgressions" => "🎵 Chord Progressions",
            "GuitarTechniques" => "🎸 Guitar Techniques",
            "SpecializedTunings" => "🎛️ Specialized Tunings",
            "Analytics" => "📊 Analytics & Insights",
            "UserPersonalization" => "👤 User Personalization",
            _ => controllerName ?? "API"
        };
    }
}

/// <summary>
///     Operation filter to add examples to Swagger documentation
/// </summary>
public class SwaggerExampleOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add examples for common parameters
        if (operation.Parameters != null)
        {
            foreach (var parameter in operation.Parameters)
            {
                AddParameterExamples(parameter);
            }
        }

        // Add response examples
        AddResponseExamples(operation, context);
    }

    private void AddParameterExamples(OpenApiParameter parameter)
    {
        switch (parameter.Name.ToLowerInvariant())
        {
            case "query":
                parameter.Example = new OpenApiString("jazz");
                parameter.Description += "\n\nExample values: jazz, blues, rock, classical, hendrix, satriani";
                break;
            case "category":
                parameter.Example = new OpenApiString("Jazz");
                parameter.Description += "\n\nExample values: Jazz, Blues, Rock, Classical, Folk, Metal";
                break;
            case "difficulty":
                parameter.Example = new OpenApiString("Intermediate");
                parameter.Description += "\n\nExample values: Beginner, Intermediate, Advanced, Expert";
                break;
            case "artist":
                parameter.Example = new OpenApiString("Jimi Hendrix");
                parameter.Description += "\n\nExample values: Jimi Hendrix, Joe Satriani, John Coltrane, The Beatles";
                break;
            case "name":
                if (parameter.Description?.Contains("chord") == true)
                {
                    parameter.Example = new OpenApiString("Hendrix Chord");
                }
                else if (parameter.Description?.Contains("progression") == true)
                {
                    parameter.Example = new OpenApiString("ii-V-I");
                }
                else if (parameter.Description?.Contains("technique") == true)
                {
                    parameter.Example = new OpenApiString("Alternate Picking");
                }
                else if (parameter.Description?.Contains("tuning") == true)
                {
                    parameter.Example = new OpenApiString("Drop D");
                }

                break;
        }
    }

    private void AddResponseExamples(OpenApiOperation operation, OperationFilterContext context)
    {
        // This could be expanded to add specific response examples
        // based on the operation and return types
    }
}

/// <summary>
///     Document filter to add tag descriptions
/// </summary>
public class SwaggerTagDescriptionFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Tags = new List<OpenApiTag>
        {
            new()
            {
                Name = "🎼 Musical Knowledge",
                Description =
                    "Unified access to all musical concepts with search, filtering, and analytics capabilities"
            },
            new()
            {
                Name = "🎵 Chord Progressions",
                Description = "Harmonic progressions across genres with Roman numeral analysis and examples"
            },
            new()
            {
                Name = "🎸 Guitar Techniques",
                Description = "Playing techniques from basic strumming to advanced lead guitar methods"
            },
            new()
            {
                Name = "🎛️ Specialized Tunings",
                Description = "Alternative tuning systems, extended range instruments, and studio techniques"
            },
            new()
            {
                Name = "📊 Analytics & Insights",
                Description = "Musical relationship analysis, usage patterns, and intelligent recommendations"
            },
            new()
            {
                Name = "👤 User Personalization",
                Description = "User profiles, learning paths, progress tracking, and personalized experiences"
            }
        };
    }
}
