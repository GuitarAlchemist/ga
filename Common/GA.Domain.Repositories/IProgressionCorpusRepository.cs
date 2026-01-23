namespace GA.Domain.Repositories;

using Core.Tabs;

public interface IProgressionCorpusRepository
{
    Task SaveAsync(ProgressionCorpusItem item);
    Task<IEnumerable<ProgressionCorpusItem>> GetByStyleAsync(string style);
    Task<IEnumerable<ProgressionCorpusItem>> GetAllAsync();
    Task<long> CountAsync();
}
