namespace GA.Data.MongoDB.Services;

using Business.Assets.Assets;
using global::MongoDB.Driver.GridFS;
using Microsoft.Extensions.Logging;
using Models;

/// <summary>
///     MongoDB-backed implementation of asset storage and retrieval
/// </summary>
[PublicAPI]
public class AssetService
{
    private readonly IMongoCollection<AssetDocument> _collection;
    private readonly GridFSBucket _gridFs;
    private readonly ILogger<AssetService> _logger;

    public AssetService(
        IMongoDatabase database,
        ILogger<AssetService> logger)
    {
        _collection = database.GetCollection<AssetDocument>("assets");
        _gridFs = new GridFSBucket(database);
        _logger = logger;

        // Create indexes
        CreateIndexesAsync().GetAwaiter().GetResult();
    }

    private async Task CreateIndexesAsync()
    {
        var indexKeys = Builders<AssetDocument>.IndexKeys;

        // Index on category for fast category queries
        await _collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AssetDocument>(
                indexKeys.Ascending(d => d.Category),
                new CreateIndexOptions { Name = "idx_category" }
            )
        );

        // Index on tags for tag-based search
        await _collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AssetDocument>(
                indexKeys.Ascending("Tags"),
                new CreateIndexOptions { Name = "idx_tags" }
            )
        );

        // Index on name for text search
        await _collection.Indexes.CreateOneAsync(
            new CreateIndexModel<AssetDocument>(
                indexKeys.Text(d => d.Name),
                new CreateIndexOptions { Name = "idx_name_text" }
            )
        );
    }

    /// <summary>
    ///     Store GLB file in GridFS and create asset document
    /// </summary>
    public async Task<AssetDocument> CreateAssetAsync(
        AssetMetadata metadata,
        byte[] glbData,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating asset {Name} in MongoDB", metadata.Name);

        // Upload GLB to GridFS
        var glbFileId = await _gridFs.UploadFromBytesAsync(
            $"{metadata.Id}.glb",
            glbData,
            new GridFSUploadOptions
            {
                Metadata = new BsonDocument
                {
                    { "assetId", metadata.Id },
                    { "contentType", "model/gltf-binary" }
                }
            },
            cancellationToken
        );

        // Create document
        var document = new AssetDocument
        {
            Id = ObjectId.GenerateNewId(),
            Name = metadata.Name,
            Category = metadata.Category,
            GlbFileId = glbFileId,
            GlbPath = metadata.GlbPath,
            PolyCount = metadata.PolyCount,
            License = metadata.License,
            Source = metadata.Source,
            Author = metadata.Author,
            Tags = metadata.Tags,
            Bounds = metadata.Bounds != null ? BoundingBoxData.FromBoundingBox(metadata.Bounds) : null,
            FileSizeBytes = metadata.FileSizeBytes,
            IsOptimized = metadata.IsOptimized,
            ImportedBy = metadata.ImportedBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);

        _logger.LogInformation("Created asset {Id} with GridFS file {FileId}", metadata.Id, glbFileId);

        return document;
    }

    /// <summary>
    ///     Get asset by MongoDB ObjectId
    /// </summary>
    public async Task<AssetDocument?> GetAssetByIdAsync(
        ObjectId id,
        CancellationToken cancellationToken = default)
    {
        return await _collection.Find(d => d.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    ///     Get asset by custom asset ID (hash)
    /// </summary>
    public async Task<AssetDocument?> GetAssetByCustomIdAsync(
        string customId,
        CancellationToken cancellationToken = default)
    {
        // Search by GlbPath which contains the custom ID
        return await _collection.Find(d => d.GlbPath != null && d.GlbPath.Contains(customId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    ///     Get all assets by category
    /// </summary>
    public async Task<List<AssetDocument>> GetAssetsByCategoryAsync(
        AssetCategory category,
        CancellationToken cancellationToken = default)
    {
        return await _collection.Find(d => d.Category == category)
            .SortBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     Get all assets
    /// </summary>
    public async Task<List<AssetDocument>> GetAllAssetsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _collection.Find(_ => true)
            .SortBy(d => d.Category)
            .ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     Search assets by tags
    /// </summary>
    public async Task<List<AssetDocument>> SearchByTagsAsync(
        Dictionary<string, string> tags,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<AssetDocument>.Filter.Empty;

        foreach (var (key, value) in tags)
        {
            filter &= Builders<AssetDocument>.Filter.Eq($"Tags.{key}", value);
        }

        return await _collection.Find(filter)
            .SortBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     Download GLB file from GridFS
    /// </summary>
    public async Task<byte[]> DownloadGlbAsync(
        ObjectId glbFileId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Downloading GLB file {FileId} from GridFS", glbFileId);

        return await _gridFs.DownloadAsBytesAsync(glbFileId, cancellationToken: cancellationToken);
    }

    /// <summary>
    ///     Delete asset and its GLB file
    /// </summary>
    public async Task DeleteAssetAsync(
        ObjectId id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting asset {Id}", id);

        // Get asset to find GLB file ID
        var asset = await GetAssetByIdAsync(id, cancellationToken);
        if (asset == null)
        {
            _logger.LogWarning("Asset {Id} not found", id);
            return;
        }

        // Delete GLB file from GridFS
        if (asset.GlbFileId.HasValue)
        {
            await _gridFs.DeleteAsync(asset.GlbFileId.Value, cancellationToken);
            _logger.LogInformation("Deleted GridFS file {FileId}", asset.GlbFileId.Value);
        }

        // Delete thumbnail if exists
        if (asset.ThumbnailFileId.HasValue)
        {
            await _gridFs.DeleteAsync(asset.ThumbnailFileId.Value, cancellationToken);
        }

        // Delete document
        await _collection.DeleteOneAsync(d => d.Id == id, cancellationToken);

        _logger.LogInformation("Deleted asset {Id}", id);
    }

    /// <summary>
    ///     Update asset metadata
    /// </summary>
    public async Task UpdateMetadataAsync(
        ObjectId id,
        AssetMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating metadata for asset {Id}", id);

        var update = Builders<AssetDocument>.Update
            .Set(d => d.Name, metadata.Name)
            .Set(d => d.Category, metadata.Category)
            .Set(d => d.License, metadata.License)
            .Set(d => d.Source, metadata.Source)
            .Set(d => d.Author, metadata.Author)
            .Set(d => d.Tags, metadata.Tags)
            .Set(d => d.IsOptimized, metadata.IsOptimized)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);

        if (metadata.Bounds != null)
        {
            update = update.Set(d => d.Bounds, BoundingBoxData.FromBoundingBox(metadata.Bounds));
        }

        await _collection.UpdateOneAsync(
            d => d.Id == id,
            update,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    ///     Convert AssetDocument to AssetMetadata
    /// </summary>
    public static AssetMetadata ToMetadata(AssetDocument document)
    {
        return new AssetMetadata
        {
            Id = document.Id.ToString(),
            Name = document.Name,
            Category = document.Category,
            GlbPath = document.GlbPath ?? string.Empty,
            PolyCount = document.PolyCount,
            License = document.License,
            Source = document.Source,
            Author = document.Author,
            Tags = document.Tags,
            Bounds = document.Bounds?.ToBoundingBox(),
            FileSizeBytes = document.FileSizeBytes,
            IsOptimized = document.IsOptimized,
            ImportedBy = document.ImportedBy
        };
    }
}
