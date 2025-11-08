namespace GuitarAlchemistChatbot.Services;

using GA.Business.Core.Atonal;

/// <summary>
///     Provides common chord progression templates organized by genre
/// </summary>
public class ChordProgressionTemplates
{
    /// <summary>
    ///     Gets all available progression templates
    /// </summary>
    public static Dictionary<string, ProgressionTemplate> GetAllTemplates()
    {
        return new Dictionary<string, ProgressionTemplate>
        {
            // Pop/Rock Progressions
            ["pop-axis"] = new(
                "I-V-vi-IV (Axis of Awesome)",
                "Pop/Rock",
                "The most popular progression in modern music. Used in thousands of hit songs.",
                ["C", "G", "Am", "F"],
                ["I", "V", "vi", "IV"],
                "Uplifting, anthemic, singalong quality"),

            ["pop-50s"] = new(
                "I-vi-IV-V (50s Progression)",
                "Pop/Doo-Wop",
                "Classic 1950s progression, nostalgic and romantic.",
                ["C", "Am", "F", "G"],
                ["I", "vi", "IV", "V"],
                "Nostalgic, romantic, classic"),

            ["pop-sensitive"] = new(
                "vi-IV-I-V (Sensitive Female Chord)",
                "Pop/Ballad",
                "Emotional progression often used in ballads and introspective songs.",
                ["Am", "F", "C", "G"],
                ["vi", "IV", "I", "V"],
                "Emotional, introspective, vulnerable"),

            // Jazz Progressions
            ["jazz-251"] = new(
                "ii-V-I (Two-Five-One)",
                "Jazz",
                "The fundamental jazz progression. The building block of jazz harmony.",
                ["Dm7", "G7", "Cmaj7"],
                ["ii7", "V7", "Imaj7"],
                "Sophisticated, resolved, jazzy"),

            ["jazz-rhythm"] = new(
                "I-vi-ii-V (Rhythm Changes)",
                "Jazz/Bebop",
                "Based on 'I Got Rhythm' by Gershwin. A jazz standard progression.",
                ["Cmaj7", "Am7", "Dm7", "G7"],
                ["Imaj7", "vi7", "ii7", "V7"],
                "Swinging, upbeat, classic jazz"),

            ["jazz-minor251"] = new(
                "ii°-V7-i (Minor Two-Five-One)",
                "Jazz",
                "Minor key version of the classic ii-V-I progression.",
                ["Dm7b5", "G7", "Cm7"],
                ["iiø7", "V7", "i7"],
                "Dark, sophisticated, minor jazz"),

            // Blues Progressions
            ["blues-12bar"] = new(
                "12-Bar Blues",
                "Blues",
                "The classic 12-bar blues progression. Foundation of blues, rock, and jazz.",
                ["C7", "C7", "C7", "C7", "F7", "F7", "C7", "C7", "G7", "F7", "C7", "G7"],
                ["I7", "I7", "I7", "I7", "IV7", "IV7", "I7", "I7", "V7", "IV7", "I7", "V7"],
                "Bluesy, groovy, foundational"),

            ["blues-quick"] = new(
                "Quick Change Blues",
                "Blues",
                "Blues variation with early IV chord for more movement.",
                ["C7", "F7", "C7", "C7", "F7", "F7", "C7", "C7", "G7", "F7", "C7", "G7"],
                ["I7", "IV7", "I7", "I7", "IV7", "IV7", "I7", "I7", "V7", "IV7", "I7", "V7"],
                "Bluesy, dynamic, traditional"),

            // Rock Progressions
            ["rock-classic"] = new(
                "I-IV-V (Classic Rock)",
                "Rock",
                "The three-chord rock progression. Simple, powerful, timeless.",
                ["C", "F", "G"],
                ["I", "IV", "V"],
                "Powerful, straightforward, rock"),

            ["rock-double"] = new(
                "I-IV-I-V (Double Plagal)",
                "Rock",
                "Rock variation with repeated I-IV movement.",
                ["C", "F", "C", "G"],
                ["I", "IV", "I", "V"],
                "Driving, energetic, rock"),

            // Modal/Alternative Progressions
            ["andalusian"] = new(
                "i-VII-VI-V (Andalusian Cadence)",
                "Flamenco/Rock",
                "Dramatic progression from Spanish music. Used in rock and metal.",
                ["Am", "G", "F", "E"],
                ["i", "VII", "VI", "V"],
                "Dramatic, Spanish, intense"),

            ["royal-road"] = new(
                "IV-V-iii-vi (Royal Road)",
                "J-Pop",
                "Popular in Japanese pop music. Smooth, flowing progression.",
                ["F", "G", "Em", "Am"],
                ["IV", "V", "iii", "vi"],
                "Smooth, flowing, J-Pop"),

            ["dorian"] = new(
                "i-IV (Dorian Vamp)",
                "Modal/Funk",
                "Simple modal progression. Great for funk and modal jazz.",
                ["Dm", "G"],
                ["i", "IV"],
                "Funky, modal, groovy"),

            // Circle of Fifths
            ["circle-fifths"] = new(
                "vi-ii-V-I (Circle Progression)",
                "Jazz/Standards",
                "Follows the circle of fifths. Strong harmonic movement.",
                ["Am7", "Dm7", "G7", "Cmaj7"],
                ["vi7", "ii7", "V7", "Imaj7"],
                "Flowing, resolved, sophisticated"),

            // Minor Key Progressions
            ["minor-pop"] = new(
                "i-VI-III-VII (Minor Pop)",
                "Pop/Rock",
                "Popular minor key progression. Dark but accessible.",
                ["Am", "F", "C", "G"],
                ["i", "VI", "III", "VII"],
                "Dark, accessible, modern"),

            ["minor-classic"] = new(
                "i-iv-V (Minor Classic)",
                "Classical/Rock",
                "Classic minor progression with dominant resolution.",
                ["Am", "Dm", "E"],
                ["i", "iv", "V"],
                "Classical, resolved, minor"),

            // Extended Progressions
            ["pachelbel"] = new(
                "I-V-vi-iii-IV-I-IV-V (Pachelbel)",
                "Classical/Pop",
                "Based on Pachelbel's Canon. Elegant and flowing.",
                ["C", "G", "Am", "Em", "F", "C", "F", "G"],
                ["I", "V", "vi", "iii", "IV", "I", "IV", "V"],
                "Elegant, flowing, classical"),

            ["descending-bass"] = new(
                "I-V/7-vi-IV (Descending Bass)",
                "Pop/Rock",
                "Progression with descending bass line. Smooth and melodic.",
                ["C", "G/B", "Am", "F"],
                ["I", "V/7", "vi", "IV"],
                "Smooth, melodic, descending")
        };
    }

    /// <summary>
    ///     Gets templates filtered by genre
    /// </summary>
    public static IEnumerable<ProgressionTemplate> GetByGenre(string genre)
    {
        var allTemplates = GetAllTemplates();
        var genreLower = genre.ToLower();

        return allTemplates.Values
            .Where(t => t.Genre.ToLower().Contains(genreLower))
            .OrderBy(t => t.Name);
    }

    /// <summary>
    ///     Gets all unique genres
    /// </summary>
    public static IEnumerable<string> GetGenres()
    {
        return GetAllTemplates().Values
            .Select(t => t.Genre)
            .Distinct()
            .OrderBy(g => g);
    }

    /// <summary>
    ///     Searches templates by name or description
    /// </summary>
    public static IEnumerable<ProgressionTemplate> Search(string query)
    {
        var allTemplates = GetAllTemplates();
        var queryLower = query.ToLower();

        return allTemplates.Values
            .Where(t =>
                t.Name.ToLower().Contains(queryLower) ||
                t.Description.ToLower().Contains(queryLower) ||
                t.Mood.ToLower().Contains(queryLower))
            .OrderBy(t => t.Name);
    }
}

/// <summary>
///     Represents a chord progression template
/// </summary>
public record ProgressionTemplate(
    string Name,
    string Genre,
    string Description,
    string[] Chords,
    string[] RomanNumerals,
    string Mood)
{
    /// <summary>
    ///     Gets the progression as a formatted string
    /// </summary>
    public string GetProgressionString()
    {
        return string.Join(" → ", Chords);
    }

    /// <summary>
    ///     Gets the roman numeral analysis
    /// </summary>
    public string GetAnalysisString()
    {
        return string.Join(" → ", RomanNumerals);
    }

    /// <summary>
    ///     Transposes the progression to a different key
    /// </summary>
    public ProgressionTemplate TransposeToKey(string newKey)
    {
        try
        {
            // Parse the target key
            var targetRoot = ParseChordRoot(newKey);

            // Assume current key is C (since most templates are in C)
            var currentRoot = PitchClass.FromValue(0); // C

            // Calculate the interval between current and target key
            var intervalSemitones = (targetRoot.Value - currentRoot.Value + 12) % 12;

            // Transpose all chords
            var transposedChords = Chords.Select(chord => TransposeChord(chord, intervalSemitones)).ToArray();

            return this with
            {
                Chords = transposedChords,
                Description = $"{Description} (Transposed to {newKey})"
            };
        }
        catch (Exception)
        {
            // Fallback to original behavior if transposition fails
            return this with
            {
                Description = $"{Description} (Transposed to {newKey})"
            };
        }
    }

    /// <summary>
    ///     Parses a chord symbol to extract the root note as a PitchClass
    /// </summary>
    private static PitchClass ParseChordRoot(string chordSymbol)
    {
        if (string.IsNullOrEmpty(chordSymbol))
        {
            throw new ArgumentException("Chord symbol cannot be null or empty", nameof(chordSymbol));
        }

        // Handle basic note names (C, D, E, F, G, A, B)
        var rootChar = char.ToUpper(chordSymbol[0]);
        var baseValue = rootChar switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => throw new ArgumentException($"Invalid root note: {rootChar}", nameof(chordSymbol))
        };

        // Handle accidentals (sharp/flat)
        var accidentalOffset = 0;
        if (chordSymbol.Length > 1)
        {
            var accidental = chordSymbol[1];
            accidentalOffset = accidental switch
            {
                '#' => 1,
                'b' => -1,
                _ => 0
            };
        }

        var finalValue = (baseValue + accidentalOffset + 12) % 12;
        return PitchClass.FromValue(finalValue);
    }

    /// <summary>
    ///     Transposes a single chord by the specified number of semitones
    /// </summary>
    private static string TransposeChord(string chord, int semitones)
    {
        if (string.IsNullOrEmpty(chord))
        {
            return chord;
        }

        try
        {
            // Parse the root note
            var originalRoot = ParseChordRoot(chord);

            // Transpose the root
            var newRootValue = (originalRoot.Value + semitones) % 12;
            var newRoot = PitchClass.FromValue(newRootValue);

            // Convert back to note name (prefer sharps for simplicity)
            var newRootName = newRoot.ToSharpNote().ToString();

            // Replace the root in the original chord symbol
            var rootLength = GetRootLength(chord);
            var chordSuffix = chord.Length > rootLength ? chord.Substring(rootLength) : "";

            return newRootName + chordSuffix;
        }
        catch (Exception)
        {
            // If parsing fails, return original chord
            return chord;
        }
    }

    /// <summary>
    ///     Gets the length of the root note portion of a chord symbol
    /// </summary>
    private static int GetRootLength(string chord)
    {
        if (string.IsNullOrEmpty(chord))
        {
            return 0;
        }

        var length = 1; // At least the root note letter

        // Check for accidental
        if (chord.Length > 1 && (chord[1] == '#' || chord[1] == 'b'))
        {
            length++;
        }

        return length;
    }

    /// <summary>
    ///     Gets a markdown-formatted display of the progression
    /// </summary>
    public string ToMarkdown()
    {
        return $@"### {Name}
**Genre:** {Genre}  
**Mood:** {Mood}

{Description}

**Progression:** {GetProgressionString()}  
**Analysis:** {GetAnalysisString()}

*Try playing this progression and experiment with different rhythms and voicings!*";
    }
}
