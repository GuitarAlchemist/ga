namespace GA.Domain.Repositories;

using Core.Theory.Harmony.Progressions;

public interface IProgressionCorpusRepository
{
    Task SaveAsync(ProgressionCorpusItem item);
    Task<IEnumerable<ProgressionCorpusItem>> GetByStyleAsync(string style);
    Task<IEnumerable<ProgressionCorpusItem>> GetAllAsync();
    Task<long> CountAsync();
}
