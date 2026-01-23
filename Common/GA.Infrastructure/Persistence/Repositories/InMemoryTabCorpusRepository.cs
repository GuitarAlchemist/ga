namespace GA.Infrastructure.Persistence.Repositories;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using GA.Domain.Core.Tabs;
using GA.Domain.Repositories;

public class InMemoryTabCorpusRepository : ITabCorpusRepository
{
    private readonly ConcurrentDictionary<string, TabCorpusItem> _store = new();

    public Task SaveAsync(TabCorpusItem item)
    {
        _store[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<TabCorpusItem?> GetByIdAsync(string id)
    {
        _store.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task<IEnumerable<TabCorpusItem>> GetAllAsync()
    {
        return Task.FromResult((IEnumerable<TabCorpusItem>)_store.Values);
    }

    public Task<bool> ExistsAsync(string id)
    {
        return Task.FromResult(_store.ContainsKey(id));
    }

    public Task<long> CountAsync()
    {
        return Task.FromResult((long)_store.Count);
    }
}
