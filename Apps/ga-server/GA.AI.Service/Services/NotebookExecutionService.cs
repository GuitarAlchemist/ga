namespace GA.AI.Service.Services;

using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.CSharp;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Formatting;
using System.Text;

public class NotebookExecutionService : IDisposable
{
    private readonly CompositeKernel _kernel;

    public NotebookExecutionService()
    {
        // Initialize Composite Kernel with C# support
        _kernel = new CompositeKernel
        {
            new CSharpKernel()
            // Add F# or other kernels here if needed
        };

        _kernel.DefaultKernelName = "csharp";
    }

    public async Task<ExecutionResult> ExecuteCodeAsync(string code)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var resultData = new List<object>();

        var command = new SubmitCode(code);
        var result = await _kernel.SendAsync(command);

        foreach (var e in result.Events)
        {
            switch (e)
            {
                case StandardOutputValueProduced stdout:
                    outputBuilder.AppendLine(stdout.FormattedValues.FirstOrDefault(v => v.MimeType == "text/plain")?.Value);
                    break;
                case StandardErrorValueProduced stderr:
                    errorBuilder.AppendLine(stderr.FormattedValues.FirstOrDefault(v => v.MimeType == "text/plain")?.Value);
                    break;
                case ReturnValueProduced returnValue:
                    if (returnValue.Value != null)
                    {
                        resultData.Add(returnValue.Value);
                        outputBuilder.AppendLine(returnValue.FormattedValues.FirstOrDefault(v => v.MimeType == "text/plain")?.Value);
                    }
                    break;
                case DisplayEvent displayEvent:
                     // Handle rich output if needed
                     var html = displayEvent.FormattedValues.FirstOrDefault(v => v.MimeType == "text/html")?.Value;
                     if (html != null) resultData.Add(new { mime = "text/html", value = html });
                     break;
                 case CommandFailed failed:
                    errorBuilder.AppendLine(failed.Message);
                    break;
            }
        }

        return new ExecutionResult
        {
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString(),
            Results = resultData
        };
    }

    public void Dispose()
    {
        _kernel.Dispose();
    }
}

public class ExecutionResult
{
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public List<object> Results { get; set; } = new();
}
