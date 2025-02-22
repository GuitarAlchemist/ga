namespace GA.Data.MongoDB.Services;

public interface ISyncService
{
    Task<bool> SyncAsync();
    Task<long> GetCountAsync();
}

public interface ISyncService<T> : ISyncService where T : class
{
}
