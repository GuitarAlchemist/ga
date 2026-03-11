namespace GaApi.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.ML.Agents;
using GaApi.Services;

/// <summary>
/// Suggests low-stretch, beginner-friendly voicings when a user mentions hand pain, barre chords,
/// or difficulty with stretching — zero LLM calls, pure domain data.
/// </summary>
/// <remarks>
/// Registered by <see cref="GaApiPlugin"/> and runs at the orchestrator fast-path.
/// When triggered, it parses a chord name from the message (if present) or defaults to "C",
/// calls <see cref="VoicingComfortService.GetComfortRankedAsync"/>, and presents
/// the three lowest-stretch alternatives with fret diagrams and stretch metrics.
/// </remarks>
public sealed class VoicingComfortSkill(VoicingComfortService comfortService) : IOrchestratorSkill
{
    public string Name        => "VoicingComfort";
    public string Description => "Suggests low-stretch voicings when the user mentions hand pain or barre chord difficulty";

    // ── Triggers ──────────────────────────────────────────────────────────────

    private static readonly Regex PainTrigger =
        new(@"\b(hurt[s]?|pain|ache|strain|sore|cramp|barre|hand\s+position|stretch|hard\s+to\s+play|too\s+difficult|hurt[s]?\s+my\s+hand|my\s+hand\s+hurt)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ChordSymbol =
        new(@"\b(?<root>[A-G])(?<acc>[b#]?)(?<qual>m7b5|dim7|maj7|m7|7|min|m|dim|aug|\+)?\b",
            RegexOptions.Compiled);

    public bool CanHandle(string message) => PainTrigger.IsMatch(message);

    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        // Extract chord name if present; default to "C" for generic hand-pain queries
        var chordMatch = ChordSymbol.Match(message);
        var chordName  = chordMatch.Success ? NormalizeChord(chordMatch) : "C";

        var result = await comfortService.GetComfortRankedAsync(
            chordName, excludeFullBarre: true, ct: cancellationToken);

        if (result.IsFailure)
        {
            // Chord not found — still offer generic advice
            return ComfortAdvice(chordName, []);
        }

        var voicings = result.GetValueOrDefault([]).Take(3).ToList();
        return ComfortAdvice(chordName, voicings);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AgentResponse ComfortAdvice(
        string chordName,
        IReadOnlyList<ComfortRankedVoicing> voicings)
    {
        var sb = new StringBuilder();

        if (voicings.Count == 0)
        {
            sb.AppendLine($"I couldn't find open or partial voicings for **{chordName}**, but here are some general tips for reducing hand strain:");
        }
        else
        {
            sb.AppendLine($"Here are the {voicings.Count} most comfortable voicings for **{chordName}** (ranked by fret stretch, lowest first):");
            sb.AppendLine();

            for (var i = 0; i < voicings.Count; i++)
            {
                var v      = voicings[i].Voicing;
                var fretDiagram = FormatFrets(v.Frets);
                var stretchStr  = voicings[i].Stretch == 0 ? "open" : $"{voicings[i].Stretch} fret stretch";
                sb.AppendLine($"**{i + 1}. {v.ChordName}** ({v.Difficulty} · {stretchStr})");
                sb.AppendLine($"   Frets: `{fretDiagram}` · {v.HandPosition}");
                if (v.CagedShape is not null)
                    sb.AppendLine($"   CAGED shape: **{v.CagedShape}**");
                sb.AppendLine();
            }
        }

        sb.AppendLine("**General tips for hand comfort:**");
        sb.AppendLine("- Use your fingertips, not the pads, to press the strings.");
        sb.AppendLine("- Keep your thumb behind the neck, roughly opposite your middle finger.");
        sb.AppendLine("- Press just behind the fret wire — not in the middle of the fret.");
        sb.AppendLine("- Warm up and take frequent breaks, especially when learning new positions.");

        var evidence = voicings
            .Select(v => $"{v.Voicing.ChordName}: frets [{FormatFrets(v.Voicing.Frets)}], stretch={v.Stretch}")
            .ToArray();

        return new AgentResponse
        {
            AgentId    = AgentIds.Technique,
            Result     = sb.ToString().TrimEnd(),
            Confidence = 1.0f,
            Evidence   = evidence,
            Assumptions = ["Voicings sorted by fret stretch ascending; full barre chords excluded"]
        };
    }

    private static string FormatFrets(int[] frets) =>
        string.Join("-", frets.Select(f => f < 0 ? "x" : f.ToString()));

    private static string NormalizeChord(Match m)
    {
        var root = m.Groups["root"].Value + m.Groups["acc"].Value;
        var qual = m.Groups["qual"].Value;
        return qual is "min" ? $"{root}m" : $"{root}{qual}";
    }
}
