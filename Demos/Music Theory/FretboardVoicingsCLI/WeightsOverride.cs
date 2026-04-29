namespace FretboardVoicingsCLI;

using System.Text.Json;

/// <summary>
///     Per-partition weight overrides for OPTIC-K v4 index rebuilds.
///     <para>
///         Cross-repo contract with <c>ix-autoresearch</c>'s Target A loop:
///         IX writes a JSON config matching this shape, GA reads and
///         validates it, the index is rebuilt with overridden weights,
///         IX scores the resulting index. See
///         <c>docs/contracts/2026-04-27-optick-weights-config.contract.md</c>.
///     </para>
///     <para>
///         <b>JSON shape</b> (snake_case keys, all optional):
///         <code>
///         {
///           "structure_weight":  0.45,
///           "morphology_weight": 0.25,
///           "context_weight":    0.20,
///           "symbolic_weight":   0.10,
///           "modal_weight":      0.10,
///           "root_weight":       0.05
///         }
///         </code>
///         Missing keys keep their <see cref="EmbeddingSchema.SimilarityPartitions"/>
///         default; explicit zeros exclude the partition from similarity
///         (mirrors the schema's "weight = 0 ⇒ excluded" convention).
///     </para>
///     <para>
///         <b>Validation</b>: every weight must be finite and ≥ 0. Any
///         <c>NaN</c>, <c>±Inf</c>, or negative value rejects the entire
///         override (the rebuild aborts with a non-zero exit; ix-autoresearch
///         logs the failure in its <c>Iteration.error</c> field).
///     </para>
/// </summary>
public sealed class WeightsOverride
{
    /// <summary>
    ///     Schema version identifier carried in the JSON payload. Bumped only
    ///     on breaking changes (one-way door across both repos). v1 is
    ///     compatible with the original 6-partition layout shipped 2026-04-27.
    /// </summary>
    public const int SchemaVersion = 1;

    /// <summary>
    ///     Map from partition name (uppercase, matching
    ///     <see cref="EmbeddingPartition.Name"/>) to override weight.
    /// </summary>
    private readonly Dictionary<string, float> _byName;

    private WeightsOverride(Dictionary<string, float> byName)
    {
        _byName = byName;
    }

    /// <summary>
    ///     True iff <paramref name="partitionName"/> has an override; the
    ///     out parameter receives the override weight on success. Lookup
    ///     is case-insensitive.
    /// </summary>
    public bool TryGetWeight(string partitionName, out float weight)
        => _byName.TryGetValue(partitionName.ToUpperInvariant(), out weight);

    /// <summary>
    ///     Parse an override from JSON text. Validates that every present
    ///     weight is finite and ≥ 0. Returns a populated
    ///     <see cref="WeightsOverride"/> on success or throws
    ///     <see cref="JsonException"/> with a human-readable diagnostic
    ///     on failure.
    /// </summary>
    public static WeightsOverride FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(
                $"weights-config root must be a JSON object; got {root.ValueKind}");
        }

        var byName = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var (jsonKey, partitionName) in JsonKeyToPartition)
        {
            if (!root.TryGetProperty(jsonKey, out var element)) continue;
            if (element.ValueKind != JsonValueKind.Number)
            {
                throw new JsonException(
                    $"weights-config: '{jsonKey}' must be a number; got {element.ValueKind}");
            }

            // GetSingle accepts both 0.45 and 1 — fine.
            var value = element.GetSingle();
            if (!float.IsFinite(value))
            {
                throw new JsonException(
                    $"weights-config: '{jsonKey}' must be finite; got {value}");
            }

            if (value < 0f)
            {
                throw new JsonException(
                    $"weights-config: '{jsonKey}' must be ≥ 0; got {value}");
            }

            byName[partitionName] = value;
        }

        // Optional schema_version key for forward-compat. v1 accepts
        // anything ≤ 1; future versions reject older readers.
        if (root.TryGetProperty("schema_version", out var schemaElem)
            && schemaElem.ValueKind == JsonValueKind.Number
            && schemaElem.GetInt32() > SchemaVersion)
        {
            throw new JsonException(
                $"weights-config schema_version {schemaElem.GetInt32()} > supported {SchemaVersion}");
        }

        return new WeightsOverride(byName);
    }

    /// <summary>
    ///     Read + parse an override from <paramref name="path"/>. Surfaces
    ///     IO and parse errors as <see cref="InvalidOperationException"/>
    ///     with the file path included.
    /// </summary>
    public static WeightsOverride FromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"--weights-config: cannot read '{path}': {ex.Message}", ex);
        }

        try
        {
            return FromJson(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"--weights-config: cannot parse '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Snake-case JSON keys mapped to the upper-case partition names
    ///     used by <see cref="EmbeddingPartition.Name"/>. Adding a partition
    ///     here is a *one-way door* across the IX/GA repos: ratify in
    ///     the contract document first.
    /// </summary>
    private static readonly (string JsonKey, string PartitionName)[] JsonKeyToPartition =
    [
        ("structure_weight",  "STRUCTURE"),
        ("morphology_weight", "MORPHOLOGY"),
        ("context_weight",    "CONTEXT"),
        ("symbolic_weight",   "SYMBOLIC"),
        ("modal_weight",      "MODAL"),
        ("root_weight",       "ROOT"),
    ];

    /// <summary>
    ///     Diagnostic representation suitable for stderr logging when the
    ///     CLI applies the override. Excludes default-weight partitions
    ///     (i.e., those not overridden) for brevity.
    /// </summary>
    public override string ToString()
    {
        if (_byName.Count == 0) return "WeightsOverride{}";
        var parts = string.Join(", ", _byName.Select(kv => $"{kv.Key}={kv.Value:F4}"));
        return $"WeightsOverride{{{parts}}}";
    }
}
