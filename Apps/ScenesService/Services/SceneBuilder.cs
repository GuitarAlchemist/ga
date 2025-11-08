namespace ScenesService.Services;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Models;
using SharpGLTF.Schema2;

public sealed class GlbSceneBuilder
{
    public async Task<(string path, long bytes, string etag)> BuildGlbAsync(SceneBuildRequestDto req, ISceneStore store)
    {
        // Create a minimal GLB with just empty nodes and metadata
        var model = ModelRoot.CreateModel();
        var scene = model.UseScene("Scene");

        // Add cell nodes with metadata
        foreach (var cell in req.Cells)
        {
            var node = scene.CreateNode($"Cell_{cell.CellId}");

            // Add extras metadata
            var cellExtras = JsonNode.Parse(JsonSerializer.Serialize(new
            {
                type = "cell",
                cellId = cell.CellId
            }));
            node.Extras = cellExtras;
        }

        // Add portal nodes with metadata
        foreach (var portal in req.Portals)
        {
            var node = scene.CreateNode($"Portal_{portal.From}_to_{portal.To}");

            var portalExtras = JsonNode.Parse(JsonSerializer.Serialize(new
            {
                type = "portal",
                from = portal.From,
                to = portal.To,
                quad = portal.Quad
            }));
            node.Extras = portalExtras;
        }

        // Save to memory stream
        using var ms = new MemoryStream();
        model.WriteGLB(ms);
        var bytes = ms.ToArray();

        // Generate ETag
        var etag = Convert.ToHexString(SHA256.HashData(bytes))[..16];

        // Save with metadata
        var meta = new
        {
            req.SceneId,
            Cells = req.Cells.Select(c => c.CellId).ToArray(),
            Portals = req.Portals.Count,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        await store.SaveAsync(req.SceneId, bytes, meta, etag);
        return ("", bytes.LongLength, etag);
    }
}
