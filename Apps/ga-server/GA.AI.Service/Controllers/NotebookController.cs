namespace GA.AI.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using GA.AI.Service.Services;

[ApiController]
[Route("api/[controller]")]
public class NotebookController : ControllerBase
{
    private readonly string _notebooksRoot = Path.Combine(Directory.GetCurrentDirectory(), "Notebooks");

    private readonly NotebookExecutionService _executionService;

    public NotebookController(NotebookExecutionService executionService)
    {
        _executionService = executionService;
        if (!Directory.Exists(_notebooksRoot))
        {
            Directory.CreateDirectory(_notebooksRoot);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotebookInfo>>> GetNotebooks()
    {
        if (!Directory.Exists(_notebooksRoot)) return Ok(Enumerable.Empty<NotebookInfo>());

        var ipynbFiles = Directory.GetFiles(_notebooksRoot, "*.ipynb", SearchOption.AllDirectories);
        var dibFiles = Directory.GetFiles(_notebooksRoot, "*.dib", SearchOption.AllDirectories);
        
        var results = new System.Collections.Concurrent.ConcurrentBag<NotebookInfo>();

        Parallel.ForEach(ipynbFiles.Concat(dibFiles), file => 
        {
            var info = new NotebookInfo
            {
                Id = Path.GetRelativePath(_notebooksRoot, file).Replace("\\", "/"),
                Name = Path.GetFileNameWithoutExtension(file),
                Path = Path.GetRelativePath(_notebooksRoot, file).Replace("\\", "/"),
                LastModified = System.IO.File.GetLastWriteTimeUtc(file),
                Type = Path.GetExtension(file).ToLower() == ".dib" ? "dib" : "ipynb"
            };

            // Parse tags for ipynb files
            if (info.Type == "ipynb")
            {
                try
                {
                    using var fs = System.IO.File.OpenRead(file);
                    using var doc = JsonDocument.Parse(fs);
                    if (doc.RootElement.TryGetProperty("metadata", out var metadata))
                    {
                        if (metadata.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
                        {
                            info.Tags = tagsElement.EnumerateArray()
                                .Select(t => t.GetString())
                                .Where(t => !string.IsNullOrEmpty(t))
                                .ToList()!;
                        }
                    }
                }
                catch 
                { 
                    // Ignore parse errors, just return without tags
                }
            }

            results.Add(info);
        });

        return Ok(results.OrderBy(r => r.Name));
    }

    [HttpGet("{*path}")]
    public async Task<ActionResult<object>> GetNotebook(string path)
    {
        path = System.Net.WebUtility.UrlDecode(path);
        var fullPath = Path.GetFullPath(Path.Combine(_notebooksRoot, path));
        
        // Security check
        if (!fullPath.StartsWith(_notebooksRoot, System.StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        var json = await System.IO.File.ReadAllTextAsync(fullPath);
        try 
        {
            if (fullPath.EndsWith(".dib", System.StringComparison.OrdinalIgnoreCase))
            {
                // Simple wrapper for .dib content to look like a one-cell notebook for our viewer
                return Ok(new {
                    cells = new[] {
                        new {
                            cell_type = "code",
                            source = (await System.IO.File.ReadAllLinesAsync(fullPath)).Select(line => line + "\n"),
                            outputs = Enumerable.Empty<object>()
                        }
                    }
                });
            }
            var notebook = JsonSerializer.Deserialize<JsonElement>(json);
            return Ok(notebook);
        }
        catch (JsonException)
        {
            return BadRequest("Invalid .ipynb format.");
        }
    }

    [HttpPost("execute")]
    public async Task<ActionResult<ExecutionResult>> ExecuteCode([FromBody] ExecuteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) return BadRequest("Code is required");
        var result = await _executionService.ExecuteCodeAsync(request.Code);
        return Ok(result);
    }
}

public class ExecuteRequest
{
    public string Code { get; set; } = string.Empty;
}

public class NotebookInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = "ipynb";
    public System.DateTime LastModified { get; set; }
    public List<string> Tags { get; set; } = new();
}
