namespace GaMcpServer;

using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentGovernance.Audit;
using Microsoft.Extensions.Logging;

/// <summary>
/// Emits VSM algedonic signals when the Agent Governance Toolkit denies or rate-limits
/// an MCP tool call. Mirrors the contract enforced by <c>Scripts/algedonic-emit.ps1</c>
/// and the JSON Schema at <c>docs/contracts/algedonic-signal.schema.json</c>:
/// append-only, one JSON object per line, UTF-8 without BOM.
///
/// The PowerShell emitter is the canonical reference; this writer exists so the
/// .NET MCP server can emit without shelling out (the stdio MCP transport runs
/// hot and a sub-process per signal would add latency to the deny path).
///
/// Severity mapping (from the task brief):
///   <see cref="GovernanceEventType.PolicyCheck"/>      → info  (only when a rate-limit hit)
///   <see cref="GovernanceEventType.PolicyViolation"/>  → warn
///   <see cref="GovernanceEventType.ToolCallBlocked"/>  → warn  (unless data points to credential probe)
/// Suspected credential probes are escalated to <c>critical</c> based on the matched-rule name.
/// </summary>
public sealed class AlgedonicEmitter
{
    /// <summary>Default inbox location relative to the repo root.</summary>
    public const string DefaultInboxRelativePath = "state/algedonic/inbox.jsonl";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // Match the PowerShell emitter's compact output. Schema mandates one
        // JSON object per line — no indentation, no trailing whitespace.
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // Keep <, >, & literal so policy messages with HTML-ish content survive
        // a round-trip through the projector that reads the inbox.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Object lock guarding appends — multi-threaded callers (MCP request handlers) share one writer.</summary>
    private readonly object _writeGate = new();

    private readonly string _inboxPath;
    private readonly ILogger<AlgedonicEmitter> _logger;

    /// <summary>
    /// Construct the emitter pointed at <paramref name="inboxPath"/>. When null/empty,
    /// the inbox is resolved by walking up from <see cref="AppContext.BaseDirectory"/>
    /// until a <c>state/algedonic/</c> sibling is found, then falling back to
    /// <c>{BaseDirectory}/state/algedonic/inbox.jsonl</c>.
    /// </summary>
    public AlgedonicEmitter(ILogger<AlgedonicEmitter> logger, string? inboxPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _inboxPath = string.IsNullOrWhiteSpace(inboxPath)
            ? ResolveDefaultInboxPath()
            : inboxPath!;
    }

    /// <summary>The resolved absolute path the emitter will write to.</summary>
    public string InboxPath => _inboxPath;

    /// <summary>
    /// Translate a governance event into an algedonic signal and append it to the inbox.
    /// Safe to call from a hot path: failures are logged but never thrown — the contract
    /// is "never break a request because the alert channel is wedged."
    /// </summary>
    public void Emit(GovernanceEvent evt)
    {
        if (evt is null) return;

        try
        {
            var signal = BuildSignal(evt);
            var line = JsonSerializer.Serialize(signal, JsonOptions);
            AppendLine(line);
        }
        catch (Exception ex)
        {
            // Algedonic channel is best-effort; if it breaks we want to know
            // but we never want to fail the originating MCP request.
            _logger.LogError(ex, "Algedonic signal emission failed for event {EventType}", evt.Type);
        }
    }

    /// <summary>
    /// Build the algedonic signal payload as an ordered <see cref="Dictionary{TKey,TValue}"/>.
    /// Field order matches <c>algedonic-signal.schema.json</c> and the PowerShell emitter so
    /// diff'ing the inbox stays readable.
    /// </summary>
    internal Dictionary<string, object?> BuildSignal(GovernanceEvent evt)
    {
        var severity = MapSeverity(evt);
        var summary = BuildSummary(evt);
        var details = BuildDetails(evt);
        var policyName = evt.PolicyName ?? "(none)";

        return new Dictionary<string, object?>
        {
            ["id"] = NewUuidv7(),
            ["schema"] = "algedonic-signal-v0.1.0",
            ["emitted_at"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["repo"] = "ga",
            ["source"] = "gamcp-governance",
            ["severity"] = severity,
            ["summary"] = Truncate(summary, 140),
            ["details"] = details,
            ["evidence_url"] = null,
            ["affected_artifacts"] = new[] { "GaMcpServer/Policies/governance.yaml" },
            ["ttl_hours"] = 24,
            ["escalation"] = new Dictionary<string, object?>
            {
                ["on_unack_after_hours"] = severity switch
                {
                    "info" => (int?)null,
                    "warn" => 24,
                    "fail" => 4,
                    "critical" => 1,
                    _ => 24
                },
                ["route_to"] = severity == "critical" ? "on-call" : "operator"
            },
            ["ack"] = new Dictionary<string, object?>
            {
                ["acked"] = false,
                ["acked_by"] = null,
                ["acked_at"] = null,
                ["resolution"] = null
            },
            ["supersedes"] = Array.Empty<string>()
        };
    }

    private static string MapSeverity(GovernanceEvent evt)
    {
        // Prompt-injection detector denies surface in evt.Data — the middleware
        // synthesises a ToolCallBlocked with injection_type + threat_level set,
        // but PolicyName is null because there's no matched YAML rule. Promote
        // High/Critical threat levels to "critical" so the operator queue
        // doesn't drown them in the warn-volume of rate-limit hits.
        var threatLevel = TryGetData(evt, "threat_level");
        if (string.Equals(threatLevel, "Critical", StringComparison.OrdinalIgnoreCase)
            || string.Equals(threatLevel, "High", StringComparison.OrdinalIgnoreCase))
        {
            return "critical";
        }

        // Explicit deny rules in governance.yaml prefixed deny-credential / deny-env
        // are also treated as critical. Kept here as forward-compat for when the
        // YAML condition language gains substring matching and these rules can
        // move out of the injection detector blocklist.
        if (evt.PolicyName is { Length: > 0 } name &&
            (name.StartsWith("deny-credential", StringComparison.OrdinalIgnoreCase)
             || name.StartsWith("deny-env", StringComparison.OrdinalIgnoreCase)))
        {
            return "critical";
        }

        return evt.Type switch
        {
            GovernanceEventType.ToolCallBlocked => "warn",
            GovernanceEventType.PolicyViolation => "warn",
            GovernanceEventType.PolicyCheck => "info",
            _ => "info"
        };
    }

    private static string BuildSummary(GovernanceEvent evt)
    {
        var tool = TryGetData(evt, "tool_name") ?? TryGetData(evt, "tool") ?? "(unknown-tool)";
        return evt.Type switch
        {
            GovernanceEventType.ToolCallBlocked => $"MCP tool '{tool}' blocked by policy '{evt.PolicyName ?? "(none)"}'",
            GovernanceEventType.PolicyViolation => $"Policy violation on tool '{tool}' ({evt.PolicyName ?? "(none)"})",
            GovernanceEventType.PolicyCheck => $"Policy check fired on tool '{tool}' ({evt.PolicyName ?? "(none)"})",
            _ => $"Governance event {evt.Type} on tool '{tool}'"
        };
    }

    private static string BuildDetails(GovernanceEvent evt)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"event_type={evt.Type}; agent_id={evt.AgentId ?? "(none)"}; ");
        sb.Append(CultureInfo.InvariantCulture, $"policy={evt.PolicyName ?? "(none)"}; event_id={evt.EventId}");
        if (evt.Data is { Count: > 0 } data)
        {
            sb.Append("; data={");
            var first = true;
            foreach (var kv in data)
            {
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(CultureInfo.InvariantCulture, $"{kv.Key}={Truncate(kv.Value?.ToString() ?? "null", 80)}");
            }
            sb.Append('}');
        }
        return sb.ToString();
    }

    private static string? TryGetData(GovernanceEvent evt, string key)
    {
        if (evt.Data is null) return null;
        return evt.Data.TryGetValue(key, out var v) ? v?.ToString() : null;
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max
            ? value
            : value.Substring(0, max);

    private void AppendLine(string jsonLine)
    {
        var dir = Path.GetDirectoryName(_inboxPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Append under a process-wide lock. The contract's <4 KB-per-signal
        // ceiling means a single Write is atomic on the file systems we ship
        // on (NTFS, ext4); the lock just keeps concurrent emits from
        // interleaving partial lines.
        lock (_writeGate)
        {
            using var stream = new FileStream(
                _inboxPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);
            using var writer = new StreamWriter(stream, Utf8NoBom);
            writer.WriteLine(jsonLine);
        }
    }

    /// <summary>
    /// Default inbox resolution. Walks up from the running binary directory
    /// looking for a <c>state/algedonic/</c> folder so the emitter still
    /// writes to the canonical inbox when launched from
    /// <c>GaMcpServer/bin/Debug/net10.0/</c>. Falls back to creating the
    /// folder relative to the binary if none is found (matches the script
    /// behavior in <c>Scripts/algedonic-emit.ps1</c>).
    /// </summary>
    internal static string ResolveDefaultInboxPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, DefaultInboxRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var stateDir = Path.GetDirectoryName(candidate);
            if (stateDir is not null && Directory.Exists(stateDir))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, DefaultInboxRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// UUIDv7 generator — 48-bit Unix-ms timestamp + version + random tail.
    /// Mirrors the PowerShell emitter so ids collate in emission order and
    /// stay within the schema's <c>[A-Za-z0-9_-]+</c> filename-safe constraint.
    /// </summary>
    internal static string NewUuidv7()
    {
        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Span<byte> bytes = stackalloc byte[16];

        bytes[0] = (byte)((unixMs >> 40) & 0xFF);
        bytes[1] = (byte)((unixMs >> 32) & 0xFF);
        bytes[2] = (byte)((unixMs >> 24) & 0xFF);
        bytes[3] = (byte)((unixMs >> 16) & 0xFF);
        bytes[4] = (byte)((unixMs >> 8) & 0xFF);
        bytes[5] = (byte)(unixMs & 0xFF);

        var random = bytes[6..];
        RandomNumberGenerator.Fill(random);

        // Version (7) in top nibble of byte 6, variant (10b) in top 2 bits of byte 8.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"{hex.AsSpan(0, 8)}-{hex.AsSpan(8, 4)}-{hex.AsSpan(12, 4)}-{hex.AsSpan(16, 4)}-{hex.AsSpan(20, 12)}";
    }
}
