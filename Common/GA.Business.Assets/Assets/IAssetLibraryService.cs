namespace GA.Business.Assets.Assets;

using JetBrains.Annotations;

/// <summary>
///     Service for managing 3D assets in the BSP DOOM Explorer
/// </summary>
[PublicAPI]
public interface IAssetLibraryService
{
    /// <summary>
    ///     Import a Blender model (.blend file) and convert to GLB
    /// </summary>
    /// <param name="path">Path to the .blend file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Metadata for the imported asset</returns>
    Task<AssetMetadata> ImportBlenderModelAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Import a GLB file directly
    /// </summary>
    /// <param name="path">Path to the .glb file</param>
    /// <param name="metadata">Optional metadata to override defaults</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Metadata for the imported asset</returns>
    Task<AssetMetadata> ImportGlbAsync(string path, AssetMetadata? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Optimize a GLB file for WebGPU rendering
    /// </summary>
    /// <param name="glbPath">Path to the GLB file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Metadata for the optimized asset</returns>
    Task<AssetMetadata> OptimizeForWebGpuAsync(string glbPath, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all assets by category
    /// </summary>
    /// <param name="category">Asset category</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of asset metadata</returns>
    Task<List<AssetMetadata>> GetAssetsByCategoryAsync(AssetCategory category,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get asset by ID
    /// </summary>
    /// <param name="id">Asset ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Asset metadata or null if not found</returns>
    Task<AssetMetadata?> GetAssetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get all assets
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all asset metadata</returns>
    Task<List<AssetMetadata>> GetAllAssetsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Search assets by tags
    /// </summary>
    /// <param name="tags">Tags to search for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching asset metadata</returns>
    Task<List<AssetMetadata>> SearchByTagsAsync(Dictionary<string, string> tags,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Download the GLB file for an asset
    /// </summary>
    /// <param name="id">Asset ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>GLB file data</returns>
    Task<byte[]> DownloadGlbAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Delete an asset
    /// </summary>
    /// <param name="id">Asset ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAssetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Update asset metadata
    /// </summary>
    /// <param name="metadata">Updated metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateMetadataAsync(AssetMetadata metadata, CancellationToken cancellationToken = default);
}
