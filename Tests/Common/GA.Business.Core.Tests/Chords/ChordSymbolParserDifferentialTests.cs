using System;
using System.Collections.Generic;
using GA.Business.Core.Chords;
using GA.Business.Core.Chords.Parsing;
using GA.Business.Core.Chords.Parsing.Legacy;
using GA.Business.Core.Notes;
using NUnit.Framework;

namespace GA.Business.Core.Tests.Chords;

/// <summary>
/// Differential tests to compare the canonical chord symbol parser against the legacy parser.
/// Once parity is confirmed in CI over time, the legacy parser file can be removed and these
/// tests can be simplified to assert only the canonical behavior.
/// </summary>
[Category("ChordParsing")]
[Category("Differential")]
public class ChordSymbolParserDifferentialTests
{
    private static readonly string[] ValidSymbols =
    {
        "C", "Am", "F#dim", "G7", "Cmaj7", "Dm7", "Bdim7", "Fø7", "Bb△9",
        "Dsus2", "Esus4", "A6", "Em6", "C9", "Cmaj11", "Dm11", "G13",
        "Cmaj13", "Em13", "Cadd9", "Emadd9", "C6/9", "Em6/9",
        "Galt", "C7b5", "C7#5", "C7b9", "C7#9", "Cmaj7#11", "C7b13",
        // Slash and quartal naming are covered by naming services; parser should still accept symbols
        "C/E"
    };

    // Subset of symbols where legacy and canonical are expected to match exactly on PitchClassSet
    private static readonly HashSet<string> StrictParitySymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "C", "Am", "F#dim", "G7", "Cmaj7", "Cadd9", "Dsus2", "Esus4", "A6", "Em6"
    };

    private static readonly string[] InvalidSymbols =
    {
        // Intentionally only include cases that truly violate the root token or are empty
        "", " ", "Hmaj7", "R7", "7", "#C", "Z"
    };

    // Known legacy divergences due to historic parser bugs (e.g., interpreting "m7" as "maj7")
    private static readonly HashSet<string> LegacyDivergences = new(StringComparer.OrdinalIgnoreCase)
    {
        "Dm7"
    };

    [Test]
    public void Canonical_And_Legacy_Parsers_Produce_Equivalent_Core_Results_On_Valid_Symbols()
    {
        var canonical = new ChordSymbolParser();
        var legacy = new LegacyChordSymbolParser();

        foreach (var symbol in ValidSymbols)
        {
            // Canonical
            var chordA = canonical.Parse(symbol);
            // Legacy
            var chordB = legacy.Parse(symbol);

            // Core equivalences (root should always align)
            Assert.That(chordA.Root.PitchClass.Value, Is.EqualTo(chordB.Root.PitchClass.Value),
                $"Root pitch class differs for '{symbol}'");

            // Full PitchClassSet parity only for curated subset where legacy behavior is known to match
            if (StrictParitySymbols.Contains(symbol))
            {
                Assert.That(chordA.PitchClassSet, Is.EqualTo(chordB.PitchClassSet),
                    $"PitchClassSet differs for '{symbol}'");
            }

            // TryParse parity (skip strict comparison for non-strict symbols)
            var okA = canonical.TryParse(symbol, out var chordATry);
            var okB = legacy.TryParse(symbol, out var chordBTry);
            Assert.That(okA, Is.True, $"Canonical TryParse failed for '{symbol}'");
            Assert.That(okB, Is.True, $"Legacy TryParse failed for '{symbol}'");
            Assert.That(chordATry, Is.Not.Null);
            Assert.That(chordBTry, Is.Not.Null);

            if (StrictParitySymbols.Contains(symbol))
            {
                Assert.That(chordATry!.PitchClassSet, Is.EqualTo(chordBTry!.PitchClassSet),
                    $"TryParse PitchClassSet differs for '{symbol}'");
            }
        }
    }

    [Test]
    public void Both_Parsers_Reject_Invalid_Symbols()
    {
        var canonical = new ChordSymbolParser();
        var legacy = new LegacyChordSymbolParser();

        foreach (var symbol in InvalidSymbols)
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => canonical.Parse(symbol), Throws.Exception,
                    $"Canonical should throw for invalid symbol '{symbol}'");
                Assert.That(canonical.TryParse(symbol, out var chordA), Is.False);
                Assert.That(chordA, Is.Null);

                Assert.That(() => legacy.Parse(symbol), Throws.Exception,
                    $"Legacy should throw for invalid symbol '{symbol}'");
                Assert.That(legacy.TryParse(symbol, out var chordB), Is.False);
                Assert.That(chordB, Is.Null);
            });
        }
    }
}
