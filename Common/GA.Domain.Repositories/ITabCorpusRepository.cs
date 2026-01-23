namespace GA.Domain.Repositories;

using Core.Tabs;

public interface ITabCorpusRepository
{
    Task SaveAsync(TabCorpusItem item);
    Task<TabCorpusItem?> GetByIdAsync(string id);
    Task<IEnumerable<TabCorpusItem>> GetAllAsync();
    Task<bool> ExistsAsync(string id);
    Task<long> CountAsync();
}