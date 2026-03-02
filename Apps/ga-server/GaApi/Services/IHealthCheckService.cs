namespace GaApi.Services;

using Models;

/// <summary>
///     Service for performing comprehensive health checks
/// </summary>
public interface IHealthCheckService
{
    Task<HealthCheckResponse> GetHealthAsync();
    Task<ServiceHealth> CheckDatabaseAsync();
    Task<ServiceHealth> CheckVectorSearchAsync();
    Task<ServiceHealth> CheckMemoryCacheAsync();
}
