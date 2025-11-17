namespace GA.Business.Core.Chords.Parsing;

using System;
using System.Text.RegularExpressions;
using GA.Business.Core.Chords;
using Notes;

/// <summary>
///     Parses chord symbols into Chord objects
/// </summary>
public class ChordSymbolParser
{
    private static readonly Regex _chordSymbolRegex = new(
        @"^([A-G][#b]?)(.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    ///     Parses a chord symbol string into a Chord object
    /// </summary>
    public Chord Parse(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Chord symbol cannot be null or empty", nameof(symbol));
        }

        var match = _chordSymbolRegex.Match(symbol.Trim());
        if (!match.Success)
        {
            throw new ArgumentException($"Invalid chord symbol: {symbol}", nameof(symbol));
        }

        var rootName = match.Groups[1].Value;
        var suffix = match.Groups[2].Value;

        var root = Note.Accidented.Parse(rootName, null);
        var formula = ParseChordSuffix(suffix);

        return new Chord(root, formula, symbol);
    }

    /// <summary>
    ///     Tries to parse a chord symbol string into a Chord object
    /// </summary>
    public bool TryParse(string symbol, out Chord? chord)
    {
        try
        {
            chord = Parse(symbol);
            return true;
        }
        catch
        {
            chord = null;
            return false;
        }
    }

    private ChordFormula ParseChordSuffix(string suffix)
    {
        if (string.IsNullOrEmpty(suffix))
        {
            return CommonChordFormulas.Major;
        }

        // Normalize the suffix
        suffix = suffix.ToLowerInvariant().Replace(" ", "");

        return suffix switch
        {
            "" or "maj" or "major" => CommonChordFormulas.Major,
            "m" or "min" or "minor" or "-" => CommonChordFormulas.Minor,
            "dim" or "°" => CommonChordFormulas.Diminished,
            "aug" or "+" => CommonChordFormulas.Augmented,
            "sus2" => CreateSus2Formula(),
            "sus4" or "sus" => CreateSus4Formula(),
            "6" => CreateSixthFormula(),
            "m6" => CreateMinorSixthFormula(),
            "7" => CommonChordFormulas.Dominant7,
            "maj7" or "△7" => CommonChordFormulas.Major7,
            "m7" or "min7" or "-7" => CommonChordFormulas.Minor7,
            "dim7" or "°7" => CreateDiminished7Formula(),
            "m7b5" or "ø7" => CreateHalfDiminished7Formula(),
            "9" => CreateDominant9Formula(),
            "maj9" or "△9" => CreateMajor9Formula(),
            "m9" or "min9" or "-9" => CreateMinor9Formula(),
            "11" => CreateDominant11Formula(),
            "maj11" or "△11" => CreateMajor11Formula(),
            "m11" or "min11" or "-11" => CreateMinor11Formula(),
            "13" => CreateDominant13Formula(),
            "maj13" or "△13" => CreateMajor13Formula(),
            "m13" or "min13" or "-13" => CreateMinor13Formula(),
            "add9" => CreateAdd9Formula(),
            "madd9" => CreateMinorAdd9Formula(),
            "6/9" or "69" => CreateSixNineFormula(),
            "m6/9" or "m69" => CreateMinorSixNineFormula(),
            _ => ParseComplexSuffix(suffix)
        };
    }

    private ChordFormula ParseComplexSuffix(string suffix)
    {
        // Handle more complex chord symbols
        // This is a simplified implementation - a full parser would be much more complex

        if (suffix.Contains("alt"))
        {
            return CreateAlteredDominantFormula();
        }

        if (suffix.Contains("b5"))
        {
            return CreateFlatFiveFormula(suffix);
        }

        if (suffix.Contains("#5"))
        {
            return CreateSharpFiveFormula(suffix);
        }

        if (suffix.Contains("b9"))
        {
            return CreateFlatNineFormula(suffix);
        }

        if (suffix.Contains("#9"))
        {
            return CreateSharpNineFormula(suffix);
        }

        if (suffix.Contains("#11"))
        {
            return CreateSharpElevenFormula(suffix);
        }

        if (suffix.Contains("b13"))
        {
            return CreateFlatThirteenFormula(suffix);
        }

        // Default to major if unknown
        return CommonChordFormulas.Major;
    }

    // Implementation via ChordFormula.FromSemitones to avoid direct Interval construction here
    private static ChordFormula CreateSus2Formula() => ChordFormula.FromSemitones("Sus2", 2, 7);
    private static ChordFormula CreateSus4Formula() => ChordFormula.FromSemitones("Sus4", 5, 7);
    private static ChordFormula CreateSixthFormula() => ChordFormula.FromSemitones("Sixth", 4, 7, 9);
    private static ChordFormula CreateMinorSixthFormula() => ChordFormula.FromSemitones("Minor Sixth", 3, 7, 9);
    private static ChordFormula CreateDiminished7Formula() => ChordFormula.FromSemitones("Diminished 7th", 3, 6, 9);
    private static ChordFormula CreateHalfDiminished7Formula() => ChordFormula.FromSemitones("Half Diminished 7th", 3, 6, 10);
    private static ChordFormula CreateDominant9Formula() => ChordFormula.FromSemitones("Dominant 9th", 4, 7, 10, 14);
    private static ChordFormula CreateMajor9Formula() => ChordFormula.FromSemitones("Major 9th", 4, 7, 11, 14);
    private static ChordFormula CreateMinor9Formula() => ChordFormula.FromSemitones("Minor 9th", 3, 7, 10, 14);
    private static ChordFormula CreateDominant11Formula() => ChordFormula.FromSemitones("Dominant 11th", 4, 7, 10, 14, 17);
    private static ChordFormula CreateMajor11Formula() => ChordFormula.FromSemitones("Major 11th", 4, 7, 11, 14, 17);
    private static ChordFormula CreateMinor11Formula() => ChordFormula.FromSemitones("Minor 11th", 3, 7, 10, 14, 17);
    private static ChordFormula CreateDominant13Formula() => ChordFormula.FromSemitones("Dominant 13th", 4, 7, 10, 14, 17, 21);
    private static ChordFormula CreateMajor13Formula() => ChordFormula.FromSemitones("Major 13th", 4, 7, 11, 14, 17, 21);
    private static ChordFormula CreateMinor13Formula() => ChordFormula.FromSemitones("Minor 13th", 3, 7, 10, 14, 17, 21);
    private static ChordFormula CreateAdd9Formula() => ChordFormula.FromSemitones("Add9", 4, 7, 14);
    private static ChordFormula CreateMinorAdd9Formula() => ChordFormula.FromSemitones("Minor Add9", 3, 7, 14);
    private static ChordFormula CreateSixNineFormula() => ChordFormula.FromSemitones("6/9", 4, 7, 9, 14);
    private static ChordFormula CreateMinorSixNineFormula() => ChordFormula.FromSemitones("Minor 6/9", 3, 7, 9, 14);
    private static ChordFormula CreateAlteredDominantFormula() => ChordFormula.FromSemitones("Altered Dominant", 4, 7, 10, 13, 15);
    private static ChordFormula CreateFlatFiveFormula(string _) => ChordFormula.FromSemitones("Flat5", 4, 6);
    private static ChordFormula CreateSharpFiveFormula(string _) => ChordFormula.FromSemitones("Sharp5", 4, 8);
    private static ChordFormula CreateFlatNineFormula(string _) => ChordFormula.FromSemitones("Flat9", 4, 7, 10, 13);
    private static ChordFormula CreateSharpNineFormula(string _) => ChordFormula.FromSemitones("Sharp9", 4, 7, 10, 15);
    private static ChordFormula CreateSharpElevenFormula(string _) => ChordFormula.FromSemitones("Sharp11", 4, 7, 11, 18);
    private static ChordFormula CreateFlatThirteenFormula(string _) => ChordFormula.FromSemitones("Flat13", 4, 7, 10, 20);
}
