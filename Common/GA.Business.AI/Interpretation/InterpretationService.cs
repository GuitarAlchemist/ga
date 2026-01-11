namespace GA.Business.AI.Interpretation;

using GA.Business.Core.Fretboard.Voicings.Analysis;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Service for generating semantic tags and contextual insights for musical objects.
/// Moves interpretive logic out of the core engine and into a structured nomenclature.
/// </summary>
public static class InterpretationService
{
    public static List<string> GenerateSemanticTags(
        ChordIdentification chordId,
        VoicingCharacteristics voicingChars,
        ModeInfo? modeInfo,
        PhysicalLayout layout,
        PlayabilityInfo playability,
        PerceptualQualities? perceptualQualities = null)
    {
        var tags = new HashSet<string>();

        // Position & Physical
        tags.Add(layout.HandPosition.ToLower().Replace(" ", "-"));
        if (layout.OpenStrings.Length > 0) tags.Add(InterpretationTags.Structure.OpenVoicing);

        // Register
        if (perceptualQualities != null)
        {
            tags.Add($"register:{perceptualQualities.Register.ToLower().Replace(" ", "-")}");
        }

        // Difficulty
        if (playability.Difficulty == "Beginner") tags.Add(InterpretationTags.Playability.Beginner);
        if (playability.HandStretch >= 5) tags.Add(InterpretationTags.Playability.WideStretch);
        if (playability.BarreRequired) tags.Add(InterpretationTags.Playability.Barre);

        // Voicing Structure
        if (playability.ShellFamily != null) tags.Add(InterpretationTags.Structure.Shell);
        
        if (voicingChars.DropVoicing != null)
        {
            var dv = voicingChars.DropVoicing.ToLower();
            if (dv.Contains("drop 2")) tags.Add(InterpretationTags.Structure.Drop2);
            else if (dv.Contains("drop 3")) tags.Add(InterpretationTags.Structure.Drop3);
        }
        
        if (voicingChars.IsRootless) tags.Add(InterpretationTags.Structure.Rootless);
        
        if (voicingChars.IsOpenVoicing) tags.Add(InterpretationTags.Structure.OpenVoicing);
        else tags.Add(InterpretationTags.Structure.ClosedVoicing);

        // Moods & Genres
        var name = chordId.ChordName?.ToLowerInvariant() ?? "";
        
        if (name.Contains("maj7") || name.Contains("major-7") || name.Contains("major 7"))
        {
            tags.Add(InterpretationTags.Mood.Dreamy);
            tags.Add(InterpretationTags.Mood.Stable);
            tags.Add(InterpretationTags.Genre.Jazz);
        }
        else if (name.Contains("m7") || name.Contains("min7") || name.Contains("minor 7"))
        {
            tags.Add(InterpretationTags.Mood.Soulful);
            tags.Add(InterpretationTags.Genre.NeoSoul);
        }
        else if (name.Contains("major") || name.Contains("maj"))
        {
            tags.Add(InterpretationTags.Mood.Bright);
            tags.Add(InterpretationTags.Mood.Stable);
        }
        else if (name.Contains("minor") || name.Contains("min"))
        {
            tags.Add(InterpretationTags.Mood.Melancholy);
            tags.Add(InterpretationTags.Mood.Sad);
        }
        
        if (name.Contains("maj9") || name.Contains("major 9") || name.Contains("maj13") || name.Contains("13"))
        {
            tags.Add(InterpretationTags.Mood.Soulful);
            tags.Add(InterpretationTags.Genre.Jazz);
        }

        if (name.Contains("dim"))
        {
            tags.Add(InterpretationTags.Mood.Tense);
            tags.Add(InterpretationTags.Mood.Tragic);
        }
        else if (name.EndsWith("5") || name.Contains("power"))
        {
            tags.Add(InterpretationTags.Mood.Aggressive);
            tags.Add(InterpretationTags.Genre.Rock);
        }

        // Famous Chords
        if (name.Contains("7#9")) tags.Add(InterpretationTags.Famous.Hendrix);
        if (name.Contains("mmaj9")) tags.Add(InterpretationTags.Famous.JamesBond);
        if (name.Contains("add9") && playability.Difficulty == "Beginner") tags.Add(InterpretationTags.Famous.MuMajor);

        // Mode Information
        if (modeInfo != null)
        {
            if (modeInfo.ModeName.Contains("Lydian")) tags.Add(InterpretationTags.Mood.Floating);
            if (modeInfo.ModeName.Contains("Phrygian")) tags.Add(InterpretationTags.Genre.Flamenco);
        }

        return tags.ToList();
    }
}
