namespace GA.AI.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
    public async Task<ActionResult<IEnumerable<DocInfo>>> GetDocs()
    {
        if (!Directory.Exists(_docsRoot)) return Ok(Enumerable.Empty<DocInfo>());

        var files = Directory.GetFiles(_docsRoot, "*.md", SearchOption.AllDirectories);
        var docInfos = files.Select(f => new DocInfo
        {
            Id = Path.GetRelativePath(_docsRoot, f).Replace("\\", "/"),
            Title = Path.GetFileNameWithoutExtension(f).Replace("_", " "),
            Path = Path.GetRelativePath(_docsRoot, f).Replace("\\", "/"),
            LastModified = System.IO.File.GetLastWriteTimeUtc(f)
        });

        return Ok(docInfos);
    }

    [HttpGet("{*path}")]
    public async Task<ActionResult<string>> GetDoc(string path)
    {
        path = System.Net.WebUtility.UrlDecode(path);
        var fullPath = Path.GetFullPath(Path.Combine(_docsRoot, path));
        
        // Security check
        if (!fullPath.StartsWith(_docsRoot, System.StringComparison.OrdinalIgnoreCase))
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
    public System.DateTime LastModified { get; set; }
}
