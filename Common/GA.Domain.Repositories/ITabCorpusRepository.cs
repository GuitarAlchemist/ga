namespace GA.Domain.Repositories;

using GA.Domain.Core.Theory.Tabs;

public interface ITabCorpusRepository
{
    Task SaveAsync(TabCorpusItem item);
    Task<TabCorpusItem?> GetByIdAsync(string id);
    Task<IEnumerable<TabCorpusItem>> GetAllAsync();
    Task<bool> ExistsAsync(string id);
    Task<long> CountAsync();
}