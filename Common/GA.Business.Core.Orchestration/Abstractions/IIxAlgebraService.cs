namespace GA.Business.Core.Orchestration.Abstractions;

using GA.Business.Core.Orchestration.Models;

public interface IIxAlgebraService
{
    Task<IxAlgebraAnswer?> TryAnswerAsync(string query, CancellationToken cancellationToken = default);
}
