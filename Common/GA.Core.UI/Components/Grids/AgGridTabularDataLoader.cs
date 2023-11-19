namespace GA.Core.UI.Components.Grids;

using DesignPatterns;
using GA.Core.Extensions;
using Dtos;

/// <summary>
/// Loads data to an ag-grid instance through the gridOptions Javascript object.
/// </summary>
/// <inheritdoc />
[method: UsedImplicitly]
public class AgGridTabularDataLoader(ILogger<AgGridTabularDataLoader> logger) : IAsyncInitializable<AgGridTabularDataLoader.Inits>
{
    private readonly ILogger<AgGridTabularDataLoader> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private Inits? _inits;

    /// <summary>
    /// Loads grid data on the ag-grid (Client-side)
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public async Task LoadAsync(IEnumerable<TabularDataRow> rows)
    {
        // Populate grid data
        var gridOptions = _inits?.GridOptions ?? throw new InvalidOperationException("Grid loader is not initialized");
        try
        {
            var index = 0;
            await gridOptions.InvokeVoidAsync("api.setRowData", (object)Array.Empty<object>());
            await gridOptions.InvokeVoidAsync("api.showLoadingOverlay");

            foreach (var nodes in rows.Chunk(5000))
            {
                await gridOptions.InvokeVoidAsync("addRowDataAsync", (object)nodes).ConfigureAwait(false);
                index += nodes.Length;
            }
        }
        catch (Exception ex)
        {
            var msg = ex.GetMessageAndStackTrace($"{nameof(LoadAsync)} error");
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