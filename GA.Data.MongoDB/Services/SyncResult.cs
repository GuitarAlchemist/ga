namespace GA.Data.MongoDB.Services;

public class SyncResult
{
    public Dictionary<Type, long> Counts { get; } = new();
    public List<string> Errors { get; } = [];
}
