namespace GA.Infrastructure.Persistence.Repositories;

using System.Collections.Concurrent;
using Domain.Core.Theory.Harmony.Progressions;
using Domain.Repositories;

public class InMemoryProgressionCorpusRepository : IProgressionCorpusRepository
{
    private readonly ConcurrentDictionary<string, ProgressionCorpusItem> _store = new();

    public Task SaveAsync(ProgressionCorpusItem item)
    {
        var now = DateTime.UtcNow;
        var normalized = item with
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id,
            CreatedAt = item.CreatedAt == default ? now : item.CreatedAt,
            UpdatedAt = now
        };

        _store[normalized.Id] = normalized;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ProgressionCorpusItem>> GetByStyleAsync(string style)
    {
        var items = _store.Values.Where(x => x.StyleLabel.Equals(style, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(items);
    }

    public Task<IEnumerable<ProgressionCorpusItem>> GetAllAsync() =>
        Task.FromResult((IEnumerable<ProgressionCorpusItem>)_store.Values);

    public Task<long> CountAsync() => Task.FromResult((long)_store.Count);
}
