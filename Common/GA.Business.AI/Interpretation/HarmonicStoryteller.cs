namespace GA.Business.AI.Interpretation;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using GA.Business.Config;

/// <summary>
/// Combines raw music theory data with semantic insights to create
/// natural language narratives for the chatbot.
/// </summary>
public static class HarmonicStoryteller
{
    /// <summary>
    /// Generates a rich story for a specific guitar voicing or chord.
    /// </summary>
    public static HarmonicStory TellVoicingStory(
        string chordName,
        IEnumerable<string> semanticTags,
        string? keyContext = null)
    {
        var tagIds = semanticTags.Select(t => t.ToLowerInvariant()).ToHashSet();
        var metas = tagIds
            .Select(InterpretationTags.GetMetadata)
            .Where(m => m != null)
            .OrderByDescending(m => m.Priority)
            .ToList();

        // 1. Determine Title/Theme - Pick highest priority TitleTemplate
        var primaryMeta = metas.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.TitleTemplate));
        var title = primaryMeta?.TitleTemplate ?? $"Exploring the Character of {chordName}";

        // 2. Build Narrative from Fragments
        var narrative = new StringBuilder();
        narrative.Append($"This {chordName} voicing ");
        if (keyContext != null) narrative.Append($"set in the context of {keyContext} ");

        var fragments = metas
            .Where(m => !string.IsNullOrWhiteSpace(m.NarrativeFragment))
            .Select(m => m.NarrativeFragment)
            .ToList();

        if (fragments.Any())
        {
            // Join with punctuation and linking pronouns
            for (var i = 0; i < fragments.Count; i++)
            {
                var frag = fragments[i].Trim();
                // Ensure proper punctuation
                if (!frag.EndsWith('.') && !frag.EndsWith('!') && !frag.EndsWith('?'))
                    frag += ".";

                if (i == 0) narrative.Append(frag);
                else narrative.Append(" It " + frag);
            }
        }
        else
        {
            narrative.Append("is a unique harmonic structure with a distinct character.");
        }

        // 3. Extract Deep Insights
        var insights = metas
            .Where(m => !string.IsNullOrWhiteSpace(m.Description))
            .Select(m => $"{m.Name}: {m.Description}")
            .ToList();

        // 4. Suggest Contexts - Data-driven from nomenclature
        var contexts = metas
            .Where(m => m.SuggestedContexts != null)
            .SelectMany(m => m.SuggestedContexts)
            .Distinct()
            .ToList();

        return new(
            chordName,
            title,
            narrative.ToString().Trim(),
            insights,
            contexts);
    }

    /// <summary>
    /// Narrates the relationship between two modes (Modal Interchange).
    /// </summary>
    public static string NarrateTransformation(ModalInterchangeAnalysis analysis)
    {
        if (analysis.BorrowedDegrees.Count == 0)
            return "These two modes are harmonically identical; no substitution is needed.";

        var sb = new StringBuilder();
        sb.Append($"By shifting from {analysis.SourceMode} to {analysis.ParallelMode}, ");
        sb.Append($"you are borrowing color from another harmonic family. ");
        sb.Append($"The primary changes happen on the {string.Join(" and ", analysis.BorrowedDegrees)} degrees.");

        if (analysis.SuggestedBorrowedChords.Count > 0)
        {
            sb.Append($" This allows you to use chords like {string.Join(", ", analysis.SuggestedBorrowedChords)} ");
            sb.Append("to create modal interchange.");
        }

        return sb.ToString();
    }
}
