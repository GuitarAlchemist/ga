namespace GA.WebBlazorApp.Components.Grids;

using JetBrains.Annotations;
using Microsoft.JSInterop;

using Core.DesignPatterns;
using GA.Core.Extensions;
using Common;
using Dtos;

/// <inheritdoc />
/// <summary>
/// Loads data to an ag-grid instance through the gridOptions Javascript object.
/// </summary>
public class GenericDataGridLoader : IAsyncInitializable<GenericDataGridLoader.Inits>
{
    private readonly ILogger<GenericDataGridLoader> _logger;
    private Inits? _inits;

    [UsedImplicitly]
    public GenericDataGridLoader(ILogger<GenericDataGridLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads grid data.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task LoadDataAsync(
        DataGrid data,
        IProgress<GridLoaderProgressUpdate>? progress = null)
    {
        // Populate grid data
        var gridOptions = _inits?.GridOptions ?? throw new InvalidOperationException("Grid loader is not initialized");
        try
        {
            var index = 0;
            await gridOptions.InvokeVoidAsync("api.setRowData", (object)Array.Empty<object>());
            await gridOptions.InvokeVoidAsync("api.showLoadingOverlay");
            foreach (var chunk in data.Rows.Chunk(5000))
            {
                var nodes = chunk.ToArray();
                await gridOptions.InvokeVoidAsync("addRowDataAsync", (object) nodes).ConfigureAwait(false);
                index += chunk.Length;
                progress?.Report(new IndexUpdate(index));
            }
        }
        catch (Exception ex)
        {
            var msg = ex.GetMessageAndStackTrace($"{nameof(LoadDataAsync)} error");
            _logger.LogError(msg);
        }
        finally
        {
            await gridOptions.InvokeVoidAsync("api.hideOverlay");
        }
    }

    public record Inits(IJSObjectReference GridOptions);

    public async Task InitializeAsync(
        Inits inits,
        CancellationToken cancellationToken = default)
    {
        _inits = inits ?? throw new ArgumentNullException(nameof(inits));

        await Task.CompletedTask;
    }
}