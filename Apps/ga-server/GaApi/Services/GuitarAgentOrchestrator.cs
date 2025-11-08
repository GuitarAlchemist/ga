namespace GaApi.Services;

using System.Text;
using System.Text.Json;
using Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Models;

public interface IGuitarAgentOrchestrator
{
    Task<GuitarAgentResponse> SpiceUpProgressionAsync(SpiceUpProgressionRequest request,
        CancellationToken cancellationToken);

    Task<GuitarAgentResponse> ReharmonizeProgressionAsync(ReharmonizeProgressionRequest request,
        CancellationToken cancellationToken);

    Task<GuitarAgentResponse> CreateProgressionAsync(CreateProgressionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
///     Orchestrates multi-step guitar tasks using Microsoft Agent Framework agents.
/// </summary>
public sealed class GuitarAgentOrchestrator(
    IChatClient chatClient,
    IOptionsMonitor<GuitarAgentOptions> options,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    ILogger<GuitarAgentOrchestrator> logger) : IGuitarAgentOrchestrator
{
    private const string SpiceUpAgentId = "guitar-progression-colourist";
    private const string ReharmAgentId = "guitar-reharmonizer";
    private const string ComposerAgentId = "guitar-progression-composer";
    private const string QualityAgentId = "guitar-progression-quality-pass";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly string[] SharpNotes = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly string[] FlatNotes = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];

    private static readonly Dictionary<string, int> NoteToIndex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = 0,
        ["B#"] = 0,
        ["C#"] = 1,
        ["Db"] = 1,
        ["D"] = 2,
        ["D#"] = 3,
        ["Eb"] = 3,
        ["E"] = 4,
        ["Fb"] = 4,
        ["E#"] = 5,
        ["F"] = 5,
        ["F#"] = 6,
        ["Gb"] = 6,
        ["G"] = 7,
        ["G#"] = 8,
        ["Ab"] = 8,
        ["A"] = 9,
        ["A#"] = 10,
        ["Bb"] = 10,
        ["B"] = 11,
        ["Cb"] = 11
    };

    private readonly IChatClient _chatClient = chatClient;
    private readonly ILogger<GuitarAgentOrchestrator> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly GuitarAgentOptions _options = options.CurrentValue;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public Task<GuitarAgentResponse> SpiceUpProgressionAsync(SpiceUpProgressionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteProgressionTaskAsync(
            SpiceUpAgentId,
            AgentInstructions.SpiceUp,
            BuildSpiceUpPrompt(request),
            request,
            BuildSpiceUpFallback,
            cancellationToken);
    }

    public Task<GuitarAgentResponse> ReharmonizeProgressionAsync(ReharmonizeProgressionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteProgressionTaskAsync(
            ReharmAgentId,
            AgentInstructions.Reharmonize,
            BuildReharmonizePrompt(request),
            request,
            BuildReharmonizeFallback,
            cancellationToken);
    }

    public Task<GuitarAgentResponse> CreateProgressionAsync(CreateProgressionRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteProgressionTaskAsync(
            ComposerAgentId,
            AgentInstructions.Compose,
            BuildComposerPrompt(request),
            request,
            BuildComposerFallback,
            cancellationToken);
    }

    private async Task<GuitarAgentResponse> ExecuteProgressionTaskAsync<TRequest>(
        string agentId,
        string instructions,
        string userPrompt,
        TRequest requestContext,
        Func<TRequest, AgentPlanDto> fallbackPlanFactory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var agent = new ChatClientAgent(
            _chatClient,
            agentId,
            agentId,
            instructions,
            null,
            _loggerFactory,
            _serviceProvider);

        var chatOptions = new ChatOptions
        {
            Temperature = _options.Temperature,
            TopP = _options.TopP,
            MaxOutputTokens = _options.MaxOutputTokens,
            ResponseFormat = ChatResponseFormat.Json
        };

        var runOptions = new ChatClientAgentRunOptions(chatOptions);

        ChatClientAgentRunResponse<AgentPlanDto>? response = null;
        AgentPlanDto? plan = null;
        var structured = false;
        try
        {
            response = await agent.RunAsync<AgentPlanDto>(
                userPrompt,
                null,
                SerializerOptions,
                runOptions,
                false,
                cancellationToken);
            structured = response.TryDeserialize(SerializerOptions, out plan);
            if (!structured)
            {
                _logger.LogWarning(
                    "Agent {AgentId} returned a non-structured payload. Falling back to heuristics.",
                    agentId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Agent {AgentId} failed to process prompt. Using heuristic fallback.", agentId);
            return BuildFallbackResponse(agentId, fallbackPlanFactory(requestContext));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!structured || plan is null || response is null)
        {
            _logger.LogInformation("Agent {AgentId} response unavailable or malformed. Using fallback.", agentId);
            return BuildFallbackResponse(agentId, fallbackPlanFactory(requestContext));
        }

        IReadOnlyList<string> warnings = NormalizeList(plan?.Warnings);

        if (_options.EnableQualityPass && structured && plan is not null)
        {
            warnings = await RunQualityPassAsync(plan, warnings, cancellationToken);
        }

        return MapResponse(agentId, plan, response, structured, warnings);
    }

    private async Task<IReadOnlyList<string>> RunQualityPassAsync(
        AgentPlanDto plan,
        IReadOnlyList<string> warnings,
        CancellationToken cancellationToken)
    {
        var agent = new ChatClientAgent(
            _chatClient,
            QualityAgentId,
            QualityAgentId,
            AgentInstructions.QualityReview,
            null,
            _loggerFactory,
            _serviceProvider);

        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            Temperature = Math.Clamp(_options.Temperature * 0.6f, 0.1f, 1.2f),
            MaxOutputTokens = 250,
            ResponseFormat = ChatResponseFormat.Json
        });

        var payload = JsonSerializer.Serialize(plan, SerializerOptions);

        ChatClientAgentRunResponse<QualityReviewDto> response;
        try
        {
            response = await agent.RunAsync<QualityReviewDto>(
                payload,
                null,
                SerializerOptions,
                runOptions,
                false,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Quality pass agent failed; returning base warnings.");
            return warnings;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return response.TryDeserialize(SerializerOptions, out QualityReviewDto? review) && review is not null
            ? warnings.Concat(NormalizeList(review.AdditionalWarnings))
                .Distinct()
                .ToArray()
            : warnings;
    }

    private static GuitarAgentResponse MapResponse(
        string agentId,
        AgentPlanDto? plan,
        AgentRunResponse response,
        bool structured,
        IReadOnlyList<string> warnings)
    {
        var progression = NormalizeList(plan?.Progression);
        var sections = ConvertSections(plan?.Sections);
        var practiceIdeas = NormalizeList(plan?.PracticeIdeas);

        var usage = response.Usage is null
            ? null
            : new AgentTokenUsage(
                response.Usage.InputTokenCount,
                response.Usage.OutputTokenCount,
                response.Usage.TotalTokenCount);

        var metadata = new AgentResponseMetadata(agentId, response.CreatedAt, response.ResponseId);

        var rawText = structured ? plan?.Summary ?? response.Text : response.Text;

        var title = string.IsNullOrWhiteSpace(plan?.Title)
            ? $"Agent output from {agentId}"
            : plan!.Title;

        var summary = plan?.Summary ?? response.Text ?? string.Empty;

        return new GuitarAgentResponse(
            title,
            summary,
            progression,
            sections,
            practiceIdeas,
            usage,
            structured,
            rawText,
            warnings,
            metadata);
    }

    private static GuitarAgentResponse BuildFallbackResponse(string agentId, AgentPlanDto plan)
    {
        var progression = NormalizeList(plan.Progression);
        var sections = ConvertSections(plan.Sections);
        var practiceIdeas = NormalizeList(plan.PracticeIdeas);
        var warnings = NormalizeList(plan.Warnings)
            .Append("Generated by heuristic fallback (no live agent response).")
            .Distinct()
            .ToArray();

        var title = string.IsNullOrWhiteSpace(plan.Title) ? $"Heuristic plan for {agentId}" : plan.Title;
        var summary = string.IsNullOrWhiteSpace(plan.Summary)
            ? "Generated a playable fallback progression with colour, voice-leading notes, and practice ideas."
            : plan.Summary;

        return new GuitarAgentResponse(
            title,
            summary,
            progression,
            sections,
            practiceIdeas,
            null,
            false,
            summary,
            warnings,
            new AgentResponseMetadata($"{agentId}-fallback", null, null));
    }

    private static string BuildSpiceUpPrompt(SpiceUpProgressionRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Original progression:");
        builder.AppendLine(string.Join(" | ", request.Progression.Select(p => p.Trim())));

        if (!string.IsNullOrWhiteSpace(request.Key))
        {
            builder.AppendLine($"Key centre: {request.Key}");
        }

        if (!string.IsNullOrWhiteSpace(request.Style))
        {
            builder.AppendLine($"Style tag: {request.Style}");
        }

        if (!string.IsNullOrWhiteSpace(request.Mood))
        {
            builder.AppendLine($"Mood target: {request.Mood}");
        }

        builder.AppendLine($"Preserve cadence: {request.PreserveCadence}");
        builder.AppendLine($"Prefer close voicings: {request.FavorCloseVoicings}");

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            builder.AppendLine("Player notes:");
            builder.AppendLine(request.Notes);
        }

        builder.AppendLine();
        builder.AppendLine(
            "Please suggest tasteful embellishments, secondary dominants, approach chords, and extensions.");
        builder.AppendLine("Return JSON only.");

        return builder.ToString();
    }

    private static string BuildReharmonizePrompt(ReharmonizeProgressionRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Progression to reharmonize:");
        builder.AppendLine(string.Join(" | ", request.Progression.Select(p => p.Trim())));

        if (!string.IsNullOrWhiteSpace(request.Key))
        {
            builder.AppendLine($"Original key: {request.Key}");
        }

        if (!string.IsNullOrWhiteSpace(request.Style))
        {
            builder.AppendLine($"Current style: {request.Style}");
        }

        if (!string.IsNullOrWhiteSpace(request.TargetFeel))
        {
            builder.AppendLine($"Desired feel: {request.TargetFeel}");
        }

        builder.AppendLine($"Lock first chord: {request.LockFirstChord}");
        builder.AppendLine($"Lock last chord: {request.LockLastChord}");
        builder.AppendLine($"Allow modal interchange: {request.AllowModalInterchange}");

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            builder.AppendLine("Player notes:");
            builder.AppendLine(request.Notes);
        }

        builder.AppendLine();
        builder.AppendLine("Reharmonize while keeping voice-leading playable and providing explanations.");
        builder.AppendLine("Return JSON only.");

        return builder.ToString();
    }

    private static string BuildComposerPrompt(CreateProgressionRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Key: {request.Key}");

        if (!string.IsNullOrWhiteSpace(request.Mode))
        {
            builder.AppendLine($"Mode: {request.Mode}");
        }

        if (!string.IsNullOrWhiteSpace(request.Genre))
        {
            builder.AppendLine($"Genre: {request.Genre}");
        }

        if (!string.IsNullOrWhiteSpace(request.Mood))
        {
            builder.AppendLine($"Mood: {request.Mood}");
        }

        if (!string.IsNullOrWhiteSpace(request.SkillLevel))
        {
            builder.AppendLine($"Skill level: {request.SkillLevel}");
        }

        builder.AppendLine($"Target bars: {request.Bars}");

        if (request.ReferenceArtists is { Count: > 0 })
        {
            builder.AppendLine("Reference artists/songs:");
            builder.AppendLine(string.Join(", ", request.ReferenceArtists));
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            builder.AppendLine("Additional notes:");
            builder.AppendLine(request.Notes);
        }

        builder.AppendLine();
        builder.AppendLine("Compose a playable guitar progression that matches the brief.");
        builder.AppendLine("Return JSON only.");

        return builder.ToString();
    }

    private static GuitarAgentSection[] ConvertSections(IEnumerable<AgentPlanSectionDto>? sections)
    {
        return sections?
            .Select(section => new GuitarAgentSection(
                section.Focus,
                NormalizeList(section.Chords),
                section.Description,
                NormalizeList(section.VoicingTips),
                NormalizeList(section.TechniqueTips)))
            .ToArray() ?? [];
    }

    private static AgentPlanDto BuildSpiceUpFallback(SpiceUpProgressionRequest request)
    {
        var sourceProgression = request.Progression?.Where(static chord => !string.IsNullOrWhiteSpace(chord))
                                    .Select(static chord => chord.Trim()).ToList()
                                ?? new List<string>();
        if (sourceProgression.Count == 0)
        {
            sourceProgression = ["Dm7", "G7", "Cmaj7", "A7"];
        }

        var colouredProgression = sourceProgression
            .Select(AddColorTension)
            .ToList();

        var tensionPairings = sourceProgression
            .Zip(colouredProgression, (original, colour) => $"{original} → {colour}")
            .ToList();
        if (tensionPairings.Count == 0)
        {
            tensionPairings = ["Dm7 → Dm9", "G7 → G13", "Cmaj7 → Cmaj9"];
        }

        var innerVoicingTips = new List<string>
        {
            "Target guide tones (3rds and 7ths) and move them by half-steps for smooth comping."
        };
        innerVoicingTips.Add(
            request.FavorCloseVoicings
                ? "Keep the hand within a four-fret span; pivot around fingers 2 and 3 when sliding tensions."
                : "Experiment with wider drop-2-and-4 spreads to let the upper extensions shimmer."
        );

        var sections = new List<AgentPlanSectionDto>
        {
            new()
            {
                Focus = "Colour extensions",
                Description =
                    "Each chord gains a lush tension (9ths, 11ths, or 13ths) while keeping the original voicing shape nearby.",
                Chords = colouredProgression,
                VoicingTips = new List<string>
                {
                    "Favour drop-2/drop-3 voicings between the 5th and 8th frets for an airy bossa texture.",
                    "Let the top melody note ring; the added tensions supply the movement underneath."
                },
                TechniqueTips = new List<string>
                {
                    "Use gentle thumb-and-fingers comping to maintain a relaxed groove.",
                    "Alternate between straight comping and light arpeggios to spotlight the new tensions."
                }
            },
            new()
            {
                Focus = "Inner voice motion",
                Description =
                    "Guide-tones shift by step, creating a continuous melodic thread inside the comping pattern.",
                Chords = tensionPairings,
                VoicingTips = innerVoicingTips,
                TechniqueTips = new List<string>
                {
                    "Practice toggling between the shell voicing and the coloured version to hear the added colour clearly.",
                    "Record a short vamp and check that each new tone resolves comfortably into the next chord."
                }
            }
        };

        var practiceIdeas = new List<string>
        {
            "Loop the progression at 70 BPM, letting 9ths/13ths sustain while the thumb holds the bass.",
            "Target the 3rd and 7th on beats 2 and 4, adding tensions on the upbeat for a syncopated feel.",
            "Create mini chord melodies by placing the added tensions on top, then resolve into the original voicing."
        };

        var warnings = new List<string>
        {
            request.PreserveCadence
                ? "Cadence chords kept intact—only inner bars were embellished."
                : "Cadence chords carry tensions; resolve them cleanly back to the tonic."
        };

        return new AgentPlanDto
        {
            Title = "Colour pass: tasteful extensions",
            Summary =
                $"Applied lush tensions and smooth inner-voice motion to the progression ({request.Style ?? "bossa nova"} mood).",
            Progression = colouredProgression,
            Sections = sections,
            PracticeIdeas = practiceIdeas,
            Warnings = warnings
        };
    }

    private static AgentPlanDto BuildReharmonizeFallback(ReharmonizeProgressionRequest request)
    {
        var original = request.Progression?.Where(static chord => !string.IsNullOrWhiteSpace(chord))
                           .Select(static chord => chord.Trim()).ToList()
                       ?? new List<string>();
        if (original.Count == 0)
        {
            original = ["Am7", "D7", "Gmaj7", "Cmaj7"];
        }

        var (_, preferFlats) = ParseKeyRoot(request.Key);
        var reharmonised = new List<string>();
        var approachDominants = new List<string>();

        for (var i = 0; i < original.Count; i++)
        {
            var chord = original[i];
            string reharmChord;
            if (i == 0 && request.LockFirstChord)
            {
                reharmChord = AddColorTension(chord);
            }
            else if (i == original.Count - 1 && request.LockLastChord)
            {
                reharmChord = AddCadenceColour(chord);
            }
            else
            {
                reharmChord = ConvertReharmChord(chord);
            }

            reharmonised.Add(reharmChord);

            if (i < original.Count - 1)
            {
                var nextRoot = ExtractRoot(original[i + 1]);
                if (!string.IsNullOrWhiteSpace(nextRoot))
                {
                    var dominantRoot = TransposeRoot(nextRoot, -5, preferFlats);
                    if (!string.IsNullOrWhiteSpace(dominantRoot))
                    {
                        var approach = $"{dominantRoot}7alt";
                        reharmonised.Add(approach);
                        approachDominants.Add($"{approach} → {original[i + 1]}");
                    }
                }
            }
        }

        if (reharmonised.Count == 0)
        {
            reharmonised = ["Am9", "E7alt", "Dm9", "G13", "Cmaj9(#11)"];
        }

        if (approachDominants.Count == 0)
        {
            approachDominants = ["E7alt → Am7", "Bb7alt → A7 resolving back to tonic"];
        }

        var sections = new List<AgentPlanSectionDto>
        {
            new()
            {
                Focus = "Borrowed colour and modal interchange",
                Description =
                    "Minor chords pick up 9ths/11ths while major centres gain #11 colours for a darker bridge.",
                Chords = reharmonised,
                VoicingTips = new List<string>
                {
                    "Anchor each change with guide-tone motion (3rds and 7ths) before adding altered tensions.",
                    "Keep the bass line smooth—slide between neighbouring roots where possible."
                },
                TechniqueTips = new List<string>
                {
                    "Hybrid pick the altered dominants to emphasise the #9/b13 colour.",
                    "Use gentle slides into borrowed chords to highlight the modal shift."
                }
            },
            new()
            {
                Focus = "Dominant approaches",
                Description = "Inserted dominant chords set up each arrival, heightening the suspense before release.",
                Chords = approachDominants,
                VoicingTips = new List<string>
                {
                    "Voice the altered dominant with the #9 on top to lead directly into the next chord.",
                    "Keep alterations within comfortable stretches—use thumb-over bass if it reduces position shifts."
                },
                TechniqueTips = new List<string>
                {
                    "Practice resolving altered tensions to chord tones in the following bar.",
                    "Layer a light slide or bend into the altered note to sell the tension and release effect."
                }
            }
        };

        var practiceIdeas = new List<string>
        {
            "Loop the reharmonised bridge, accenting the altered tones before resolving to the tonic.",
            "Play the bass line alone, then add upper structures to confirm the new functional flow.",
            "Improvise nylon-string chord melody phrases that outline each borrowed colour."
        };

        var warnings = new List<string>
        {
            "Fallback reharmonisation: double-check tension choices against the melody.",
            request.LockFirstChord
                ? "Opening chord left untouched per request."
                : "Opening chord reharmonised for additional colour.",
            request.LockLastChord
                ? "Cadence chord preserved for a stable landing."
                : "Cadence chord altered—resolve the added tensions cleanly."
        };

        return new AgentPlanDto
        {
            Title = "Heuristic reharmonisation sketch",
            Summary =
                $"Applied modal interchange and altered dominants to achieve the requested {request.TargetFeel ?? "heightened"} feel.",
            Progression = reharmonised,
            Sections = sections,
            PracticeIdeas = practiceIdeas,
            Warnings = warnings
        };
    }

    private static AgentPlanDto BuildComposerFallback(CreateProgressionRequest request)
    {
        var (tonicRoot, preferFlats) = ParseKeyRoot(request.Key);
        var moodDescriptor = request.Mood ?? "dreamy";
        var genreDescriptor = request.Genre ?? "modern groove";

        var progression = new List<string>
        {
            $"{tonicRoot}m9",
            $"{TransposeRoot(tonicRoot, 5, preferFlats)}13sus4",
            $"{TransposeRoot(tonicRoot, 10, preferFlats)}maj9",
            $"{TransposeRoot(tonicRoot, 3, preferFlats)}maj9(#11)",
            $"{TransposeRoot(tonicRoot, 8, preferFlats)}ø7",
            $"{TransposeRoot(tonicRoot, 11, preferFlats)}7b9",
            $"{TransposeRoot(tonicRoot, 2, preferFlats)}m11",
            $"{TransposeRoot(tonicRoot, 7, preferFlats)}13sus4"
        };

        var sections = new List<AgentPlanSectionDto>
        {
            new()
            {
                Focus = "A section – establishing the vibe",
                Description =
                    "Intro bars keep the tonic minor sound while pivoting through suspensions to create a floating texture.",
                Chords = progression.Take(4).ToList(),
                VoicingTips = new List<string>
                {
                    "Voice the tonic m9 with the 9th on top for an immediate lofi pad feel.",
                    "Slide parallel sus voicings up the neck to keep the motion gentle."
                },
                TechniqueTips = new List<string>
                {
                    "Use palm-muted pick strums mixed with fingerstyle for dynamic contrast.",
                    "Add subtle vibrato on held tensions to emulate tape-warped lofi ambience."
                }
            },
            new()
            {
                Focus = "B section – tension and release",
                Description =
                    "Halfway point brings borrowed Ø7 and altered dominants before gliding back to the tonic.",
                Chords = progression.Skip(4).ToList(),
                VoicingTips = new List<string>
                {
                    "Keep the Ø7 compact; target the minor 3rd and flat 5 for clear voice-leading.",
                    "Resolve the altered 7th chord by keeping the same upper voices and only shifting the bass."
                },
                TechniqueTips = new List<string>
                {
                    "Loop the final four bars and experiment with triplet slides into the altered chord.",
                    "Layer harmonics or light swell effects over the return to the tonic to highlight the release."
                }
            }
        };

        var practiceIdeas = new List<string>
        {
            "Play along with a slow lofi beat, focusing on letting the extensions ring longer than usual.",
            "Create a bass ostinato on strings 6 and 5 while comping upper-structure tensions softly.",
            "Compose a simple melody that lands on the 9th or #11 of each chord to hear the modal colour."
        };

        if (request.ReferenceArtists is { Count: > 0 })
        {
            practiceIdeas.Add(
                $"Reference the vibe of {string.Join(", ", request.ReferenceArtists)} for phrasing inspiration.");
        }

        if (!string.IsNullOrWhiteSpace(request.SkillLevel) &&
            request.SkillLevel.Equals("beginner", StringComparison.OrdinalIgnoreCase))
        {
            practiceIdeas.Add(
                "Break each voicing into two-note fragments before assembling the full chord to keep stretches manageable.");
        }
        else
        {
            practiceIdeas.Add("Experiment with two-hand tapping on sustained tensions to emulate synth-like textures.");
        }

        var warnings = new List<string>
        {
            "Fallback composition: adapt chord qualities if the melody requires different tensions.",
            $"Progression voiced for a {moodDescriptor} mood—adjust dynamics to taste."
        };

        return new AgentPlanDto
        {
            Title = $"{moodDescriptor.ToUpperInvariant()} {genreDescriptor} blueprint",
            Summary =
                $"Eight-bar roadmap in {request.Key ?? "E minor"} balancing suspended pads and altered resolutions.",
            Progression = progression,
            Sections = sections,
            PracticeIdeas = practiceIdeas,
            Warnings = warnings
        };
    }

    private static string AddColorTension(string chord)
    {
        var trimmed = chord.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        if (ContainsIgnoreCase(trimmed, "maj9"))
        {
            return trimmed;
        }

        if (ContainsIgnoreCase(trimmed, "maj7"))
        {
            return ReplaceOrdinalIgnoreCase(trimmed, "maj7", "maj9");
        }

        if (ContainsIgnoreCase(trimmed, "m7b5"))
        {
            return ReplaceOrdinalIgnoreCase(trimmed, "m7b5", "m9b5");
        }

        if (ContainsIgnoreCase(trimmed, "m7"))
        {
            return ReplaceOrdinalIgnoreCase(trimmed, "m7", "m9");
        }

        if (ContainsIgnoreCase(trimmed, "7sus"))
        {
            return trimmed.Contains("9", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "9";
        }

        if (ContainsIgnoreCase(trimmed, "7"))
        {
            return trimmed.Contains("alt", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "b9";
        }

        return trimmed.EndsWith("add9", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "add9";
    }

    private static string ConvertReharmChord(string chord)
    {
        var trimmed = chord.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        if (ContainsIgnoreCase(trimmed, "maj7"))
        {
            return ReplaceOrdinalIgnoreCase(trimmed, "maj7", "maj7(#11)");
        }

        if (ContainsIgnoreCase(trimmed, "m7b5"))
        {
            return ReplaceOrdinalIgnoreCase(trimmed, "m7b5", "m11b5");
        }

        if (ContainsIgnoreCase(trimmed, "m7"))
        {
            return ReplaceOrdinalIgnoreCase(trimmed, "m7", "m9");
        }

        if (ContainsIgnoreCase(trimmed, "7"))
        {
            return trimmed.Contains("alt", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "alt";
        }

        return trimmed + "add9";
    }

    private static string AddCadenceColour(string chord)
    {
        var trimmed = chord.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return trimmed;
        }

        if (ContainsIgnoreCase(trimmed, "maj7"))
        {
            return ReplaceOrdinalIgnoreCase(trimmed, "maj7", "maj9(6)");
        }

        if (ContainsIgnoreCase(trimmed, "maj9"))
        {
            return trimmed.EndsWith("6", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "(6)";
        }

        return trimmed + "6/9";
    }

    private static string ExtractRoot(string chord)
    {
        if (string.IsNullOrWhiteSpace(chord))
        {
            return string.Empty;
        }

        var trimmed = chord.Trim();
        if (trimmed.Length >= 2 && (trimmed[1] == '#' || trimmed[1] == 'b'))
        {
            return trimmed[..2];
        }

        return trimmed[..1];
    }

    private static string TransposeRoot(string root, int semitoneChange, bool preferFlats)
    {
        var normalized = NormalizeRoot(root);
        if (!NoteToIndex.TryGetValue(normalized, out var index))
        {
            index = 0;
        }

        var transposedIndex = (index + semitoneChange) % 12;
        if (transposedIndex < 0)
        {
            transposedIndex += 12;
        }

        var noteSet = preferFlats ? FlatNotes : SharpNotes;
        return noteSet[transposedIndex];
    }

    private static (string Root, bool PreferFlats) ParseKeyRoot(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return ("C", false);
        }

        var trimmed = key.Trim();
        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var rootToken = tokens.Length > 0 ? tokens[0] : trimmed;

        var normalizedRoot = NormalizeRoot(rootToken);
        var preferFlats = trimmed.IndexOf('b', StringComparison.OrdinalIgnoreCase) >= 0
                          || trimmed.Contains("flat", StringComparison.OrdinalIgnoreCase)
                          || normalizedRoot.IndexOf('b', StringComparison.OrdinalIgnoreCase) >= 0;

        return (normalizedRoot, preferFlats);
    }

    private static string NormalizeRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return "C";
        }

        var trimmed = root.Trim();
        if (NoteToIndex.ContainsKey(trimmed))
        {
            return trimmed;
        }

        var capitalised = char.ToUpperInvariant(trimmed[0]) +
                          (trimmed.Length > 1 ? trimmed[1..].ToLowerInvariant() : string.Empty);
        if (NoteToIndex.ContainsKey(capitalised))
        {
            return capitalised;
        }

        var upper = trimmed.ToUpperInvariant();
        if (NoteToIndex.ContainsKey(upper))
        {
            return upper;
        }

        if (trimmed.Length >= 2 && (trimmed[1] == '#' || trimmed[1] == 'b'))
        {
            var candidate = $"{char.ToUpperInvariant(trimmed[0])}{trimmed[1]}";
            if (NoteToIndex.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        return "C";
    }

    private static bool ContainsIgnoreCase(string text, string value)
    {
        return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string ReplaceOrdinalIgnoreCase(string text, string search, string replacement)
    {
        var index = text.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? text
            : text[..index] + replacement + text[(index + search.Length)..];
    }

    private static string[] NormalizeList(IEnumerable<string>? values)
    {
        return values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToArray() ?? [];
    }

    private static class AgentInstructions
    {
        public const string SpiceUp =
            """
            You are "Progression Colourist", a guitarist/arranger agent that embellishes chords while respecting fingerboard logic.
            Analyse the supplied progression, highlight the harmonic function, and propose tasteful substitutions, approach chords, and tensions.
            Honour the output schema strictly:
            {
              "title": string,
              "summary": string,
              "progression": string[],
              "sections": [
                {
                  "focus": string,
                  "description": string,
                  "chords": string[],
                  "voicingTips": string[],
                  "techniqueTips": string[]
                }
              ],
              "practiceIdeas": string[],
              "warnings": string[]
            }
            Populate every property, using empty arrays instead of omitting values.
            Use pragmatic guitar-centric language, reference interval relationships, and note specific fretboard locations or voicing types in voicingTips.
            The "progression" array must list the final recommended chord sequence in order.
            """;

        public const string Reharmonize =
            """
            You are "Guitar Reharmonizer", specialising in alternate changes and chord substitutions. Replace or extend chords to achieve the requested feel while maintaining playability.
            Output strictly follows the schema:
            {
              "title": string,
              "summary": string,
              "progression": string[],
              "sections": [
                {
                  "focus": string,
                  "description": string,
                  "chords": string[],
                  "voicingTips": string[],
                  "techniqueTips": string[]
                }
              ],
              "practiceIdeas": string[],
              "warnings": string[]
            }
            Explain how each section supports the target mood, reference borrowed harmony, secondary dominants, or voice-leading anchors.
            Use chord nomenclature guitarists expect (e.g. F#m7b5, G13, Db7alt).
            """;

        public const string Compose =
            """
            You are "Guitar Progression Composer". Craft a fresh chord sequence for the provided brief with practical guitar voicings.
            Output must be pure JSON with the schema:
            {
              "title": string,
              "summary": string,
              "progression": string[],
              "sections": [
                {
                  "focus": string,
                  "description": string,
                  "chords": string[],
                  "voicingTips": string[],
                  "techniqueTips": string[]
                }
              ],
              "practiceIdeas": string[],
              "warnings": string[]
            }
            Provide at least one section per phrase or harmonic idea. Mention cadences, pedal points, or rhythmic feels where appropriate.
            """;

        public const string QualityReview =
            """
            You are the quality reviewer for guitar chord progressions.
            Input will be the JSON payload produced by another agent using the schema:
            {
              "title": string,
              "summary": string,
              "progression": string[],
              "sections": [...],
              "practiceIdeas": string[],
              "warnings": string[]
            }
            Evaluate it for:
            - Hand stretch and position shifts between successive chords.
            - Functional coherence in the quoted key/modal context.
            - Clear rationale explaining why each substitution works.
            - Presence of unresolved tensions or ambiguous notation.
            Respond in JSON:
            {
              "additionalWarnings": string[]
            }
            Only add new warnings if necessary; otherwise return an empty array.
            """;
    }

    private sealed record AgentPlanDto
    {
        public string Title { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public List<string>? Progression { get; init; } = [];
        public List<AgentPlanSectionDto>? Sections { get; init; } = [];
        public List<string>? PracticeIdeas { get; init; } = [];
        public List<string>? Warnings { get; init; } = [];
    }

    private sealed record AgentPlanSectionDto
    {
        public string Focus { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public List<string>? Chords { get; init; } = [];
        public List<string>? VoicingTips { get; init; } = [];
        public List<string>? TechniqueTips { get; init; } = [];
    }

    private sealed record QualityReviewDto
    {
        public List<string>? AdditionalWarnings { get; init; }
    }
}
