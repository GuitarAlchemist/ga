namespace GA.AI.Service.Controllers;

using System.Net;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class DocumentationController : ControllerBase
{
    private readonly string _docsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Documentation");

    public DocumentationController()
    {
        if (!Directory.Exists(_docsRoot))
        {
            Directory.CreateDirectory(_docsRoot);
        }
    }

    [HttpGet]
    public Task<ActionResult<IEnumerable<DocInfo>>> GetDocs()
    {
        if (!Directory.Exists(_docsRoot))
        {
            return Task.FromResult<ActionResult<IEnumerable<DocInfo>>>(Ok(Enumerable.Empty<DocInfo>()));
        }

        var files = Directory.GetFiles(_docsRoot, "*.md", SearchOption.AllDirectories);
        var docInfos = files.Select(f => new DocInfo
        {
            Id = Path.GetRelativePath(_docsRoot, f).Replace("\\", "/"),
            Title = Path.GetFileNameWithoutExtension(f).Replace("_", " "),
            Path = Path.GetRelativePath(_docsRoot, f).Replace("\\", "/"),
            LastModified = System.IO.File.GetLastWriteTimeUtc(f)
        });

        return Task.FromResult<ActionResult<IEnumerable<DocInfo>>>(Ok(docInfos));
    }

    [HttpGet("{*path}")]
    public async Task<ActionResult<string>> GetDoc(string path)
    {
        path = WebUtility.UrlDecode(path);
        var fullPath = Path.GetFullPath(Path.Combine(_docsRoot, path));

        // Security check
        if (!fullPath.StartsWith(_docsRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        var content = await System.IO.File.ReadAllTextAsync(fullPath);
        return Ok(content);
    }
}

public class DocInfo
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
}
