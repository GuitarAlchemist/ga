namespace GaApi.Extensions;

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>
///     Document filter to add tag descriptions
/// </summary>
public class SwaggerTagDescriptionFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context) =>
        swaggerDoc.Tags =
        [
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
        ];
}
