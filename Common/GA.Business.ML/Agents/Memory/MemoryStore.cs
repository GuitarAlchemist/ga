namespace GA.Business.ML.Agents.Memory;

using System.Collections.Concurrent;
using System.Text.Json;

/// <summary>
/// A single persistent memory entry.
/// </summary>
public sealed record MemoryEntry(
    string Key,
    string Type,
    string Content,
    string[] Tags,
    DateTimeOffset Timestamp);

/// <summary>
/// In-process agent memory backed by a JSON file at <c>~/.ga/memory.json</c>.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class MemoryStore
{
    private static readonly string StorePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ga", "memory.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ConcurrentDictionary<string, MemoryEntry> _entries;

    public MemoryStore()
    {
        _entries = Load();
    }

    /// <summary>
    /// Writes (upserts) a memory entry and persists to disk.
    /// </summary>
    public void Write(string key, string type, string content, string[]? tags = null)
    {
        var entry = new MemoryEntry(key, type, content, tags ?? [], DateTimeOffset.UtcNow);
        _entries[key] = entry;
        Save();
    }

    /// <summary>
    /// Reads a single entry by key, or null if not found.
    /// </summary>
    public MemoryEntry? Read(string key)
        => _entries.TryGetValue(key, out var entry) ? entry : null;

    /// <summary>
    /// Case-insensitive substring search across content and tags.
    /// </summary>
    public IReadOnlyList<MemoryEntry> Search(string query, string? type = null, string[]? tags = null)
    {
        var q = query.ToLowerInvariant();
        return _entries.Values
            .Where(e => type is null || e.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .Where(e => tags is null || tags.Any(t => e.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .Where(e => e.Content.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || e.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase))
                     || e.Key.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Timestamp)
            .ToList();
    }

    /// <summary>
    /// Returns summary statistics about the memory store.
    /// </summary>
    public (int TotalEntries, IReadOnlyDictionary<string, int> ByType) Stats()
    {
        var byType = _entries.Values
            .GroupBy(e => e.Type)
            .ToDictionary(g => g.Key, g => g.Count());
        return (_entries.Count, byType);
    }

    private void Save()
    {
        var dir = Path.GetDirectoryName(StorePath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(_entries.Values.ToList(), JsonOpts);
        File.WriteAllText(StorePath, json);
    }

    private static ConcurrentDictionary<string, MemoryEntry> Load()
    {
        var dict = new ConcurrentDictionary<string, MemoryEntry>();
        if (!File.Exists(StorePath)) return dict;

        try
        {
            var json = File.ReadAllText(StorePath);
            var entries = JsonSerializer.Deserialize<List<MemoryEntry>>(json, JsonOpts);
            if (entries is not null)
            {
                foreach (var e in entries)
                    dict[e.Key] = e;
            }
        }
        catch
        {
            // Corrupt file — start fresh
        }

        return dict;
    }
}
