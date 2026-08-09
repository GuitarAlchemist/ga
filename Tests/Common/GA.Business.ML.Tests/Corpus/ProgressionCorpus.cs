namespace GA.Business.ML.Tests.Corpus;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
///     Deterministic loader for the held-out progression-to-voicing evaluation
///     corpus (GuitarAlchemist/ga#627, story #623).
/// </summary>
/// <remarks>
///     <para>
///         The corpus is evaluation data, not production configuration: no
///         runtime code path reads it, and it deliberately states what a
///         musically correct system must answer rather than what the product
///         currently does. Keeping it under the test project - and off the
///         <c>Common/GA.Business.Config</c> production-config tree - is the
///         separation #627 asks for.
///     </para>
///     <para>
///         Determinism means three things here, each pinned by a test:
///         the file is located the same way regardless of build configuration
///         (repo-root marker walk, same pattern as <c>RoutingEvalHarness</c>);
///         cases are required to be stored in ascending id order, so iteration
///         order is the file order is the sorted order; and no ambient state
///         (clock, environment, culture) reaches the parsed result.
///     </para>
///     <para>
///         No <c>.csproj</c> change accompanies this loader on purpose - the
///         corpus is read from source rather than copied to the output
///         directory, because <c>**/*.csproj</c> is a one-way-door path under
///         <c>agent-blackbox.policy.json</c>.
///     </para>
/// </remarks>
internal static class ProgressionCorpus
{
    /// <summary>The <c>schema_version</c> this loader understands.</summary>
    public const string SupportedSchemaVersion = "1.0.0";

    public const string CorpusFileName = "progression-corpus.v1.json";
    public const string SchemaFileName = "progression-corpus.v1.schema.json";
    public const string ReadmeFileName = "README.md";

    /// <summary>The twelve coverage categories required by GuitarAlchemist/ga#627.</summary>
    public static readonly IReadOnlyList<string> RequiredCategories =
    [
        "major-ii-V-I",
        "minor-ii-V-i",
        "major-I-vi-IV-V",
        "deceptive-cadence",
        "borrowed-iv",
        "borrowed-bVII",
        "secondary-dominant",
        "modal",
        "starts-away-from-tonic",
        "spelled-out-accidentals",
        "alternate-tuning",
        "ambiguous"
    ];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict
    };

    /// <summary>Absolute path of the directory holding the corpus and its schema.</summary>
    public static string Directory { get; } = Path.Combine(
        RepoRoot(), "Tests", "Common", "GA.Business.ML.Tests", "Corpus", "Progressions");

    public static string CorpusPath => Path.Combine(Directory, CorpusFileName);

    public static string SchemaPath => Path.Combine(Directory, SchemaFileName);

    /// <summary>
    ///     The hand-written case index that ships beside the data. It is the
    ///     in-repo "list of all cases" #627 asks for, so it is held to the data
    ///     by a test rather than by care.
    /// </summary>
    public static string ReadmePath => Path.Combine(Directory, ReadmeFileName);

    /// <summary>
    ///     Where the machine-readable pass/fail matrix lives, alongside the other
    ///     <c>state/quality</c> snapshots this repo keeps under version control.
    /// </summary>
    public static string EvidenceDirectory { get; } =
        Path.Combine(RepoRoot(), "state", "quality", "progression-corpus");

    public static string MatrixPath => Path.Combine(EvidenceDirectory, "progression-corpus-matrix.json");

    /// <summary>Reads the corpus file verbatim. Throws an actionable error when it is missing.</summary>
    public static string ReadCorpusText() => ReadRequired(CorpusPath, "corpus");

    /// <summary>Reads the schema file verbatim. Throws an actionable error when it is missing.</summary>
    public static string ReadSchemaText() => ReadRequired(SchemaPath, "schema");

    /// <summary>Parses the corpus into a <see cref="JsonDocument" /> for schema validation.</summary>
    public static JsonDocument LoadDocument() => JsonDocument.Parse(ReadCorpusText());

    /// <summary>Parses the schema into a <see cref="JsonDocument" />.</summary>
    public static JsonDocument LoadSchemaDocument() => JsonDocument.Parse(ReadSchemaText());

    /// <summary>
    ///     Deserialises the corpus and enforces the two invariants that make
    ///     iteration deterministic: a supported schema version, and cases stored
    ///     in ascending ordinal id order.
    /// </summary>
    public static ProgressionCorpusFile Load()
    {
        var file = JsonSerializer.Deserialize<ProgressionCorpusFile>(ReadCorpusText(), Options)
                   ?? throw new InvalidOperationException($"{CorpusPath} deserialised to null");

        if (file.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"{CorpusFileName} declares schema_version '{file.SchemaVersion}' but this loader " +
                $"supports '{SupportedSchemaVersion}'. A schema bump needs a matching loader change.");
        }

        var ids = file.Cases.Select(c => c.Id).ToList();
        var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (!ids.SequenceEqual(sorted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{CorpusFileName} stores cases out of order. Cases must be written in ascending " +
                $"ordinal id order so that file order, iteration order and sort order are the same " +
                $"thing. Got [{string.Join(", ", ids)}].");
        }

        return file;
    }

    private static string ReadRequired(string path, string what)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"progression {what} not found at '{path}'. Expected it under " +
                $"Tests/Common/GA.Business.ML.Tests/Corpus/Progressions (GuitarAlchemist/ga#627).",
                path);
        }

        return File.ReadAllText(path);
    }

    /// <summary>
    ///     Walks up from the test bin directory to the repository root, matching
    ///     <c>RoutingEvalHarness.ResolveQualityDir</c> so both harnesses agree on
    ///     what "the repo" means regardless of build-config nesting.
    /// </summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (System.IO.Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"no repository root (.git or AllProjects.slnx) above '{AppContext.BaseDirectory}'");
    }
}

// ── Parsed shape. Property names map to snake_case via JsonNamingPolicy. ──────

public sealed record ProgressionCorpusFile(
    string Schema,
    string SchemaVersion,
    string CorpusId,
    string CorpusVersion,
    string Status,
    bool HeldOut,
    string Issue,
    string ParentIssue,
    string Description,
    IReadOnlyList<ProgressionCase> Cases);

public sealed record ProgressionCase(
    string Id,
    string Category,
    string Title,
    CaseInput Input,
    CaseExpected Expected,
    CaseForbidden Forbidden,
    CaseUncertainty Uncertainty,
    CasePins Pins,
    CaseProvenance Provenance);

public sealed record CaseInput(
    IReadOnlyList<string> Chords,
    IReadOnlyList<string> CanonicalChords,
    string Instrument,
    CaseTuning Tuning,
    string? KeyHint,
    IReadOnlyList<SpellingGroup> SpellingEquivalenceGroups);

public sealed record SpellingGroup(string Canonical, IReadOnlyList<string> Spellings);

public sealed record CaseTuning(string Id, string Name, IReadOnlyList<string> OpenStrings);

public sealed record CaseExpected(
    string TonalCenter,
    IReadOnlyList<string> AcceptableTonalCenters,
    IReadOnlyList<string> RomanNumerals,
    IReadOnlyList<IReadOnlyList<string>> AlternativeRomanNumerals,
    IReadOnlyList<string> Functions,
    IReadOnlyList<ScaleFamily> ScaleFamilies,
    IReadOnlyList<ChordTones> RequiredChordTones,
    IReadOnlyList<string> ExplanationFacts,
    VoicingSequence VoicingSequence,
    string? Notes);

public sealed record ScaleFamily(string Chord, IReadOnlyList<string> Acceptable);

public sealed record ChordTones(
    string Chord,
    string Quality,
    string Root,
    int RootPitchClass,
    IReadOnlyList<int> PitchClasses,
    IReadOnlyList<string> Notes);

public sealed record VoicingSequence(string TuningId, IReadOnlyList<VoicingFrame> Frames);

public sealed record VoicingFrame(
    string Chord,
    string ShapeFamily,
    IReadOnlyList<int?> FretsLowToHigh,
    IReadOnlyList<int> SoundedPitchClasses);

public sealed record CaseForbidden(IReadOnlyList<string> TonalCenters, IReadOnlyList<string> Facts);

public sealed record CaseUncertainty(string ExpectedBehavior, int MinAlternatives, double? MaxConfidence);

public sealed record CasePins(IReadOnlyList<int> Issues, string? Rationale);

public sealed record CaseProvenance(string Source, string AddedIn, string Rationale);
