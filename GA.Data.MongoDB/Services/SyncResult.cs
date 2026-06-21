namespace GA.Data.MongoDB.Services;

public class SyncResult
{
    public Dictionary<Type, long> Counts { get; } = [];
    public List<string> Errors { get; } = [];
}
