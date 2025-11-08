namespace GA.Business.Assets.Assets;

using System.Security.Cryptography;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

/// <summary>
///     Service for managing 3D assets in the BSP DOOM Explorer
///     NOTE: This is a file-system based implementation. For MongoDB integration,
///     use GA.Data.MongoDB.Services.AssetService instead.
/// </summary>
[PublicAPI]
public class AssetLibraryService : IAssetLibraryService
{
    private readonly string _assetStoragePath;
    private readonly ILogger<AssetLibraryService> _logger;
    private readonly Dictionary<string, AssetMetadata> _metadataCache;

    public AssetLibraryService(ILogger<AssetLibraryService> logger)
    {
        _logger = logger;
        _metadataCache = new Dictionary<string, AssetMetadata>();

        // Default storage path - can be configured via appsettings
        _assetStoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GuitarAlchemist",
            "Assets"
        );

        // Ensure storage directory exists
        Directory.CreateDirectory(_assetStoragePath);

        // Load existing metadata
        LoadMetadataCache();
    }

    /// <inheritdoc />
    public Task<AssetMetadata> ImportBlenderModelAsync(string path, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Importing Blender model from {Path}", path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Blender file not found: {path}");
        }

        if (!path.EndsWith(".blend", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("File must be a .blend file", nameof(path));
        }

        // TODO: Implement Blender to GLB conversion
        // This would require calling Blender CLI or using a conversion service
        // For now, throw NotImplementedException
        throw new NotImplementedException(
            "Blender to GLB conversion not yet implemented. " +
            "Please convert to GLB manually and use ImportGlbAsync instead."
        );
    }

    /// <inheritdoc />
    public async Task<AssetMetadata> ImportGlbAsync(
        string path,
        AssetMetadata? metadata = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Importing GLB file from {Path}", path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"GLB file not found: {path}");
        }

        if (!path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("File must be a .glb file", nameof(path));
        }

        // Read file
        var fileData = await File.ReadAllBytesAsync(path, cancellationToken);
        var fileInfo = new FileInfo(path);

        // Generate ID from file hash
        var id = GenerateAssetId(fileData);

        // Copy to asset storage
        var storagePath = Path.Combine(_assetStoragePath, $"{id}.glb");
        await File.WriteAllBytesAsync(storagePath, fileData, cancellationToken);

        // Extract metadata from GLB (basic implementation)
        var extractedMetadata = await ExtractGlbMetadataAsync(fileData, cancellationToken);

        // Merge with provided metadata
        var finalMetadata = new AssetMetadata
        {
            Id = id,
            Name = metadata?.Name ?? Path.GetFileNameWithoutExtension(path),
            Category = metadata?.Category ?? AssetCategory.Decorative,
            GlbPath = storagePath,
            PolyCount = extractedMetadata.PolyCount,
            License = metadata?.License ?? "Unknown",
            Source = metadata?.Source ?? "Local Import",
            Author = metadata?.Author,
            Tags = metadata?.Tags ?? new Dictionary<string, string>(),
            Bounds = extractedMetadata.Bounds,
            FileSizeBytes = fileInfo.Length,
            IsOptimized = false,
            ImportedBy = metadata?.ImportedBy
        };

        // Cache metadata
        _metadataCache[id] = finalMetadata;
        await SaveMetadataCacheAsync();

        _logger.LogInformation(
            "Imported asset {Id} ({Name}) with {PolyCount} polygons",
            finalMetadata.Id,
            finalMetadata.Name,
            finalMetadata.PolyCount
        );

        return finalMetadata;
    }

    /// <inheritdoc />
    public Task<AssetMetadata> OptimizeForWebGPUAsync(string glbPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Optimizing GLB for WebGPU: {Path}", glbPath);

        // TODO: Implement optimization
        // - Decimate geometry if poly count > 10k
        // - Compress textures
        // - Remove unused materials
        // - Merge meshes where possible

        throw new NotImplementedException("GLB optimization not yet implemented");
    }

    /// <inheritdoc />
    public Task<List<AssetMetadata>> GetAssetsByCategoryAsync(AssetCategory category,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting assets for category {Category}", category);

        var assets = _metadataCache.Values
            .Where(m => m.Category == category)
            .OrderBy(m => m.Name)
            .ToList();

        return Task.FromResult(assets);
    }

    /// <inheritdoc />
    public Task<AssetMetadata?> GetAssetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting asset by ID: {Id}", id);

        _metadataCache.TryGetValue(id, out var metadata);
        return Task.FromResult(metadata);
    }

    /// <inheritdoc />
    public Task<List<AssetMetadata>> GetAllAssetsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting all assets");

        var assets = _metadataCache.Values
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .ToList();

        return Task.FromResult(assets);
    }

    /// <inheritdoc />
    public Task<List<AssetMetadata>> SearchByTagsAsync(Dictionary<string, string> tags,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching assets by tags: {Tags}", string.Join(", ", tags.Keys));

        var assets = _metadataCache.Values
            .Where(m => tags.All(tag => m.Tags.TryGetValue(tag.Key, out var value) && value == tag.Value))
            .OrderBy(m => m.Name)
            .ToList();

        return Task.FromResult(assets);
    }

    /// <inheritdoc />
    public async Task<byte[]> DownloadGlbAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading GLB for asset {Id}", id);

        var glbPath = Path.Combine(_assetStoragePath, $"{id}.glb");

        if (!File.Exists(glbPath))
        {
            throw new FileNotFoundException($"GLB file not found for asset {id}");
        }

        return await File.ReadAllBytesAsync(glbPath, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAssetAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting asset {Id}", id);

        var glbPath = Path.Combine(_assetStoragePath, $"{id}.glb");

        if (File.Exists(glbPath))
        {
            File.Delete(glbPath);
        }

        // Remove from cache
        _metadataCache.Remove(id);
        await SaveMetadataCacheAsync();
    }

    /// <inheritdoc />
    public async Task UpdateMetadataAsync(AssetMetadata metadata, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating metadata for asset {Id}", metadata.Id);

        // Update cache
        _metadataCache[metadata.Id] = metadata;
        await SaveMetadataCacheAsync();
    }

    private void LoadMetadataCache()
    {
        var metadataPath = Path.Combine(_assetStoragePath, "metadata.json");
        if (File.Exists(metadataPath))
        {
            try
            {
                var json = File.ReadAllText(metadataPath);
                var metadata = JsonSerializer.Deserialize<List<AssetMetadata>>(json);
                if (metadata != null)
                {
                    foreach (var item in metadata)
                    {
                        _metadataCache[item.Id] = item;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load metadata cache");
            }
        }
    }

    private async Task SaveMetadataCacheAsync()
    {
        var metadataPath = Path.Combine(_assetStoragePath, "metadata.json");
        try
        {
            var json = JsonSerializer.Serialize(_metadataCache.Values.ToList(), new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(metadataPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save metadata cache");
        }
    }

    // Helper methods

    private static string GenerateAssetId(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static Task<(int PolyCount, BoundingBox? Bounds)> ExtractGlbMetadataAsync(
        byte[] glbData,
        CancellationToken cancellationToken)
    {
        // TODO: Parse GLB file to extract:
        // - Polygon count
        // - Bounding box
        // - Material count
        // - Texture count

        // For now, return placeholder values
        return Task.FromResult<(int, BoundingBox?)>((1000, null));
    }
}
