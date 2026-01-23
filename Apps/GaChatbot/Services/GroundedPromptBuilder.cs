namespace GaChatbot.Services;

using System.Collections.Generic;
using System.Linq;
using System.Text;
using GaChatbot.Models;

/// <summary>
/// Builds prompts for the LLM that are strictly grounded in retrieved data.
/// Part of Phase 5.2.5 Anti-Hallucination spike.
/// </summary>
public class GroundedPromptBuilder
{
    public string Build(string userQuery, List<CandidateVoicing> candidates)
    {
        var sb = new StringBuilder();

        // 1. System Role & Constraints
        sb.AppendLine("SYSTEM: You are the Guitar Alchemist Assistant, an expert in harmonic geometry.");
        
        // 1a. Adaptive Persona Injection (Phase 8.2.1 Improvement)
        var q = userQuery.ToLowerInvariant();
        if (q.Contains("jazz") || q.Contains("fusion") || q.Contains("neo-soul") || q.Contains("substitution") || q.Contains("shell") || q.Contains("extension"))
        {
            sb.AppendLine("PERSONA: Act as a Jazz Harmony Professor. Focus on voice leading, extensions (9ths, 13ths), and harmonic function. Use terms like 'ii-V-I', 'tritone sub', and 'guide tones'. Explain distances as 'harmonic tension' or 'extension gravity'.");
        }
        else if (q.Contains("metal") || q.Contains("djent") || q.Contains("heavy") || q.Contains("power chord") || q.Contains("drop"))
        {
            sb.AppendLine("PERSONA: Act as a Modern Metal Producer. Focus on aggression, gain staging, palm-muting mechanics, and low-end clarity. Use terms like 'chug', 'tight low-end', and 'dissonance'. Explain distances as 'spectral impact' or 'collision energy'.");
        }
        else if (q.Contains("beginner") || q.Contains("easy") || q.Contains("simple") || q.Contains("how to"))
        {
             sb.AppendLine("PERSONA: Act as a Friendly Guitar Teacher. Use simple terms and analogies to physical feeling. Avoid dense jargon unless explaining it simply.");
        }
        else if (q.Contains("why") || q.Contains("explain") || q.Contains("function") || q.Contains("theoretical") || q.Contains("analyze") || q.Contains("distance") || q.Contains("compare"))
        {
             sb.AppendLine("PERSONA: Act as a Harmonic Scientist. Analyze the interval structure, resonance, and geometric relationships on the fretboard.");
        }

        // 1b. Simplified Metaphors (Story 8.2.3 Integration)
        sb.AppendLine("CONCEPT MAP (Translate math to music):");
        sb.AppendLine("- 'Spectral Centroid/Position': Where the chord sits on the 'Circle of Fifths'. Like a compass pointing to a key.");
        sb.AppendLine("- 'Geodesic Distance': Musical closeness. Think of chords as magnets; closer ones pull more naturally toward each other.");
        sb.AppendLine("- 'Spectral Velocity': Voice-leading effort. High velocity means your fingers need to jump; low velocity is a smooth transition.");
        sb.AppendLine("- 'Inertia': How much the harmony wants to stay where it is.");
        sb.AppendLine();

        sb.AppendLine("STRICT CONSTRAINT: You must only discuss the specific chord voicings provided in the manifest below.");
        sb.AppendLine("STRICT CONSTRAINT: If a chord is not in the manifest, do NOT mention it. NEVER invent chord names or shapes.");
        sb.AppendLine("STRICT CONSTRAINT: If you cannot answer using ONLY the manifest, state that you don't have that information in your database.");
        sb.AppendLine();

        // 2. The Manifest (Ground Truth)
        sb.AppendLine("### CHORD MANIFEST (GROUND TRUTH) ###");
        if (candidates.Count == 0)
        {
            sb.AppendLine("[NO DATA FOUND IN DATABASE]");
        }
        else
        {
            foreach (var c in candidates)
            {
                sb.AppendLine($"- ID: {c.Id}");
                sb.AppendLine($"  Name: {c.DisplayName}");
                sb.AppendLine($"  Fingering: {c.Shape}");
                sb.AppendLine($"  Theory: {c.ExplanationFacts.Summary}");

                sb.AppendLine($"  Modal Colors: {string.Join(", ", c.ExplanationFacts.Tags.Where(t => t.StartsWith("Flavor:"))) }");
                
                // Add Spectral Coordinates for advanced voice-leading narration
                if (c.ExplanationFacts.SpectralCentroid.HasValue)
                {
                    sb.AppendLine($"  Spectral Position: {c.ExplanationFacts.SpectralCentroid.Value:F2} radians on Fifth Cycle");
                }
                
                sb.AppendLine();
            }
        }

        // 3. User Context
        sb.AppendLine("### USER QUERY ###");
        sb.AppendLine(userQuery);
        sb.AppendLine();

        // 4. Final Instruction
        sb.AppendLine("### NARRATOR INSTRUCTION ###");
        sb.AppendLine("Explain why these specific voicings work for the user's request. Focus on their physical and harmonic properties.");
        sb.AppendLine("Answer in a concise, helpful tone.");

        return sb.ToString();
    }
}
