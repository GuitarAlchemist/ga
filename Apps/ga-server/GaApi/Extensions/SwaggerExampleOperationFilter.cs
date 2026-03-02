namespace GaApi.Extensions;

using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

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
