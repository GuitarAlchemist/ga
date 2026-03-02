namespace GA.Infrastructure.Persistence.Repositories;

using System.Collections.Concurrent;
using Domain.Core.Theory.Tabs;
using Domain.Repositories;

public class InMemoryTabCorpusRepository : ITabCorpusRepository
{
    private readonly ConcurrentDictionary<string, TabCorpusItem> _store = new();

    public Task SaveAsync(TabCorpusItem item)
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

    public Task<TabCorpusItem?> GetByIdAsync(string id)
    {
        _store.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task<IEnumerable<TabCorpusItem>> GetAllAsync() => Task.FromResult((IEnumerable<TabCorpusItem>)_store.Values);

    public Task<bool> ExistsAsync(string id) => Task.FromResult(_store.ContainsKey(id));

    public Task<long> CountAsync() => Task.FromResult((long)_store.Count);
}
