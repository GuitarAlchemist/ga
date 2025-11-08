namespace ScenesService.Models;

public record PortalDto(string From, string To, float[] Quad);
// Quad = 12 floats [x0,y0,z0, x1,y1,z1, x2,y2,z2, x3,y3,z3]

public record CellMeshDto(string MeshId, string? MaterialId = null);

public record CellDto(string CellId, List<CellMeshDto> Meshes);

public record SceneBuildRequestDto(
    string SceneId,
    List<CellDto> Cells,
    List<PortalDto> Portals,
    Dictionary<string, string>? Materials = null,
    Dictionary<string, string>? Props = null
);

public record SceneBuildResponseDto(
    string SceneId,
    string GlbPath,
    string ETag,
    long Bytes
);

public enum BuildStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Canceled
}

public record BuildJobDto(
    string JobId,
    string SceneId,
    BuildStatus Status,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? StartedUtc = null,
    DateTimeOffset? CompletedUtc = null,
    string? Error = null,
    int Attempt = 0,
    int MaxAttempts = 3
);

public record EnqueueBuildRequestDto(
    string SceneId,
    List<CellDto> Cells,
    List<PortalDto> Portals,
    Dictionary<string, string>? Materials = null,
    Dictionary<string, string>? Props = null
);
