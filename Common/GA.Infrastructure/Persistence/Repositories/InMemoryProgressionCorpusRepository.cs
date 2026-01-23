namespace GA.Infrastructure.Persistence.Repositories;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GA.Domain.Core.Tabs;
using GA.Domain.Repositories;

public class InMemoryProgressionCorpusRepository : IProgressionCorpusRepository
{
    private readonly ConcurrentDictionary<string, ProgressionCorpusItem> _store = new();

    public Task SaveAsync(ProgressionCorpusItem item)
    {
        _store[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ProgressionCorpusItem>> GetByStyleAsync(string style)
    {
        var items = _store.Values.Where(x => x.StyleLabel == style);
        return Task.FromResult(items);
    }

    public Task<IEnumerable<ProgressionCorpusItem>> GetAllAsync()
    {
        return Task.FromResult((IEnumerable<ProgressionCorpusItem>)_store.Values);
    }

    public Task<long> CountAsync()
    {
        return Task.FromResult((long)_store.Count);
    }
}
