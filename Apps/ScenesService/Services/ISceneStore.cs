namespace ScenesService.Services;

public interface ISceneStore
{
    Task SaveAsync(string sceneId, byte[] glb, object meta, string etag, CancellationToken ct = default);

    Task<(Stream stream, long length, string etag, DateTimeOffset lastModifiedUtc)?> OpenReadAsync(string sceneId,
        CancellationToken ct = default);

    Task<(string etag, long length, DateTimeOffset lastModifiedUtc)?> HeadAsync(string sceneId,
        CancellationToken ct = default);

    Task<string?> MetaJsonAsync(string sceneId, CancellationToken ct = default);
}
