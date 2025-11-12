namespace GA.Knowledge.Service.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

using System.Runtime.CompilerServices;
using GA.Business.Assets.Assets;

/// <summary>
///     API endpoints for managing 3D assets
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AssetsController(
    IAssetLibraryService assetService,
    ILogger<AssetsController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Get all assets
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all assets</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<AssetMetadata>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AssetMetadata>>> GetAllAssets(
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting all assets");

        var assets = await assetService.GetAllAssetsAsync(cancellationToken);

        return Ok(assets);
    }

    /// <summary>
    ///     Get assets by category
    /// </summary>
    /// <param name="category">Asset category</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of assets in the category</returns>
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(List<AssetMetadata>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<AssetMetadata>>> GetAssetsByCategory(
        AssetCategory category,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting assets for category {Category}", category);

        var assets = await assetService.GetAssetsByCategoryAsync(category, cancellationToken);

        return Ok(assets);
    }

    /// <summary>
    ///     Get asset by ID
    /// </summary>
    /// <param name="id">Asset ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Asset metadata</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AssetMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssetMetadata>> GetAssetById(
        string id,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting asset {Id}", id);

        var asset = await assetService.GetAssetByIdAsync(id, cancellationToken);

        if (asset == null)
        {
            return NotFound($"Asset {id} not found");
        }

        return Ok(asset);
    }

    /// <summary>
    ///     Search assets by tags
    /// </summary>
    /// <param name="tags">Tags to search for (query string format: tag1=value1&tag2=value2)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching assets</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<AssetMetadata>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AssetMetadata>>> SearchByTags(
        [FromQuery] Dictionary<string, string> tags,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Searching assets by tags: {Tags}", string.Join(", ", tags.Keys));

        var assets = await assetService.SearchByTagsAsync(tags, cancellationToken);

        return Ok(assets);
    }

    /// <summary>
    ///     Download GLB file for an asset
    /// </summary>
    /// <param name="id">Asset ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>GLB file</returns>
    [HttpGet("{id}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadGlb(
        string id,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Downloading GLB for asset {Id}", id);

        try
        {
            var glbData = await assetService.DownloadGlbAsync(id, cancellationToken);

            return File(glbData, "model/gltf-binary", $"{id}.glb");
        }
        catch (FileNotFoundException)
        {
            return NotFound($"GLB file not found for asset {id}");
        }
    }

    /// <summary>
    ///     Delete an asset
    /// </summary>
    /// <param name="id">Asset ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsset(
        string id,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Deleting asset {Id}", id);

        var asset = await assetService.GetAssetByIdAsync(id, cancellationToken);
        if (asset == null)
        {
            return NotFound($"Asset {id} not found");
        }

        await assetService.DeleteAssetAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    ///     Update asset metadata
    /// </summary>
    /// <param name="id">Asset ID</param>
    /// <param name="metadata">Updated metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated asset metadata</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AssetMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AssetMetadata>> UpdateMetadata(
        string id,
        [FromBody] AssetMetadata metadata,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating metadata for asset {Id}", id);

        if (id != metadata.Id)
        {
            return BadRequest("Asset ID in URL does not match ID in body");
        }

        var existingAsset = await assetService.GetAssetByIdAsync(id, cancellationToken);
        if (existingAsset == null)
        {
            return NotFound($"Asset {id} not found");
        }

        await assetService.UpdateMetadataAsync(metadata, cancellationToken);

        return Ok(metadata);
    }

    /// <summary>
    ///     Get asset categories
    /// </summary>
    /// <returns>List of available asset categories</returns>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public ActionResult<List<string>> GetCategories()
    {
        var categories = Enum.GetNames(typeof(AssetCategory)).ToList();
        return Ok(categories);
    }

    /// <summary>
    ///     Stream all assets (for large datasets)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of assets</returns>
    [HttpGet("stream")]
    [Produces("application/x-ndjson")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async IAsyncEnumerable<AssetMetadata> StreamAllAssets(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        logger.LogInformation("Streaming all assets");

        var assets = await assetService.GetAllAssetsAsync(cancellationToken);

        foreach (var asset in assets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            yield return asset;

            // Add small delay for backpressure control
            await Task.Delay(1, cancellationToken);
        }
    }

    /// <summary>
    ///     Stream assets by category (for large datasets)
    /// </summary>
    /// <param name="category">Asset category</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of assets</returns>
    [HttpGet("category/{category}/stream")]
    [Produces("application/x-ndjson")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async IAsyncEnumerable<AssetMetadata> StreamAssetsByCategory(
        AssetCategory category,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        logger.LogInformation("Streaming assets for category {Category}", category);

        var assets = await assetService.GetAssetsByCategoryAsync(category, cancellationToken);

        foreach (var asset in assets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            yield return asset;

            // Add small delay for backpressure control
            await Task.Delay(1, cancellationToken);
        }
    }
}
