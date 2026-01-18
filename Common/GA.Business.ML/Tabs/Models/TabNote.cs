namespace GA.Business.ML.Tabs.Models;

/// <summary>
/// Represents a single note event on a specific string.
/// </summary>
/// <param name="StringIndex">0-based index from lowest string (6th string in standard view, e.g. Low E = 0).</param>
/// <param name="Fret">Fret number (0 = open).</param>
/// <param name="Effect">Optional effect symbol (h, p, b, etc.).</param>
public record TabNote(int StringIndex, int Fret, string Effect = "");
