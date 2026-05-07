namespace GA.Business.ML.Agents.Memory;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

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

    // Serializes Save() so concurrent Write() calls don't corrupt the JSON
    // file. The previous File.WriteAllText racing pattern produced
    // FileShare violations under modest concurrency and silently lost the
    // entire store when Load() then hit a JsonException — see PR #151
    // review (reliability finding rel-006). Cap is 1; the lock is held only
    // for the serialize+write window so it's not a hot path.
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public MemoryStore()
    {
        _entries = Load();
    }

    // Bounded so anonymous chatbot traffic at the public demo URL can't grow
    // ~/.ga/memory.json unbounded — see PR #151 review (reliability rel-007).
    // When over budget, evict the oldest entries by Timestamp before adding
    // the new one. Override via Memory:MaxEntries config in callers if needed.
    public const int DefaultMaxEntries = 10_000;

    /// <summary>
    /// Writes (upserts) a memory entry and persists to disk. Evicts oldest
    /// entries when the store exceeds <see cref="DefaultMaxEntries"/>.
    /// </summary>
    public void Write(string key, string type, string content, string[]? tags = null)
    {
        var entry = new MemoryEntry(key, type, content, tags ?? [], DateTimeOffset.UtcNow);
        _entries[key] = entry;

        if (_entries.Count > DefaultMaxEntries)
        {
            // Evict the oldest 10% so this work runs amortised, not per-write.
            // ConcurrentDictionary snapshot is consistent enough for trimming.
            var evictTarget = _entries.Count - (DefaultMaxEntries * 9 / 10);
            var oldest = _entries.Values
                .OrderBy(e => e.Timestamp)
                .Take(evictTarget)
                .Select(e => e.Key)
                .ToList();
            foreach (var k in oldest)
                _entries.TryRemove(k, out _);
        }

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

        // Snapshot under the lock so concurrent Writes that mutate _entries
        // mid-serialise don't produce truncated JSON, AND atomic-rename so a
        // crash mid-write can't leave a half-flushed file that Load()
        // silently swallows on next boot.
        _saveLock.Wait();
        try
        {
            var json    = JsonSerializer.Serialize(_entries.Values.ToList(), JsonOpts);
            var tmpPath = StorePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, StorePath, overwrite: true);
        }
        finally
        {
            _saveLock.Release();
        }
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
        catch (JsonException)
        {
            // Corrupt file — rename it so operators see the loss instead of
            // silently starting fresh; preserves the bytes for postmortem.
            try
            {
                var corruptPath = StorePath + $".corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                File.Move(StorePath, corruptPath, overwrite: false);
            }
            catch
            {
                // Best-effort rename; don't block boot if it fails.
            }
        }
        catch
        {
            // Other IO errors (file locked, permission denied) — start fresh
            // rather than crash boot.
        }

        return dict;
    }
}
