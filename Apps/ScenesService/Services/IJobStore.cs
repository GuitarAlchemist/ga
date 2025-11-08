namespace ScenesService.Services;

using Models;

public interface IJobStore
{
    Task<string> EnqueueAsync(SceneBuildRequestDto req, CancellationToken ct = default);
    Task<BuildJobDto?> GetAsync(string jobId, CancellationToken ct = default);
    Task UpdateAsync(BuildJobDto job, CancellationToken ct = default);
    IAsyncEnumerable<BuildJobDto> DequeueBatchAsync(int take, CancellationToken ct = default);
}

public interface ICancelStore
{
    Task<bool> IsCanceledAsync(string jobId, CancellationToken ct = default);
    Task CancelAsync(string jobId, CancellationToken ct = default);
}
