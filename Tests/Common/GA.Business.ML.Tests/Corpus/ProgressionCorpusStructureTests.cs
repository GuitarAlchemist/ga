namespace GA.Business.ML.Tests.Corpus;

/// <summary>
///     Coverage and self-consistency invariants for the held-out progression
///     corpus (GuitarAlchemist/ga#627).
/// </summary>
/// <remarks>
///     Everything here is checkable without running any product code, which is
///     what makes it a gate: the corpus must be structurally sound and
///     musically self-consistent even while the product still fails several of
///     its cases. Product results live in
///     <see cref="ProgressionCorpusMatrixTests" />.
/// </remarks>
[TestFixture]
public class ProgressionCorpusStructureTests
{
    private static ProgressionCorpusFile Corpus => Loaded.Value;

    private static readonly Lazy<ProgressionCorpusFile> Loaded = new(ProgressionCorpus.Load);

    private static IEnumerable<ProgressionCase> Cases => Corpus.Cases;

    private static IEnumerable<TestCaseData> CaseSource() =>
        ProgressionCorpus.Load().Cases.Select(c => new TestCaseData(c).SetName($"{{m}}({c.Id})"));

    // ── Coverage: the twelve categories #627 enumerates ──────────────────────

    [Test]
    public void Corpus_HasAtLeastTwelveCases() =>
        Assert.That(Cases.Count(), Is.GreaterThanOrEqualTo(12));

    [Test]
    public void CaseIds_AreUnique()
    {
        var duplicates = Cases.GroupBy(c => c.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.That(duplicates, Is.Empty, "duplicate case ids: " + string.Join(", ", duplicates));
    }

    [Test]
    public void Corpus_CoversEveryRequiredCategory()
    {
        var present = Cases.Select(c => c.Category).ToHashSet(StringComparer.Ordinal);
        var missing = ProgressionCorpus.RequiredCategories.Where(c => !present.Contains(c)).ToList();

        Assert.That(missing, Is.Empty, "#627 requires these categories: " + string.Join(", ", missing));
    }

    // ── Regression pins required by #627 ─────────────────────────────────────

    [TestCase(614)]
    [TestCase(567)]
    public void Corpus_PinsRequiredRegressionIssue(int issue)
    {
        var pinning = Cases.Where(c => c.Pins.Issues.Contains(issue)).Select(c => c.Id).ToList();

        Assert.That(pinning, Is.Not.Empty,
            $"#627 requires at least one case pinning GuitarAlchemist/ga#{issue}");
    }

    [Test]
    public void Corpus_PinsAtLeastTwoCasesAcrossIssues614And567()
    {
        var pinning = Cases
            .Where(c => c.Pins.Issues.Contains(614) || c.Pins.Issues.Contains(567))
            .Select(c => c.Id).ToList();

        Assert.That(pinning, Has.Count.GreaterThanOrEqualTo(2),
            "'at least two cases must directly pin regressions from #614 and #567'; got " +
            string.Join(", ", pinning));
    }

    [Test]
    public void Corpus_PinsTheSpelledOutAccidentalRegression()
    {
        var pinning = Cases.Where(c => c.Pins.Issues.Contains(554)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(pinning, Is.Not.Empty, "no case pins GuitarAlchemist/ga#554");
            Assert.That(pinning.SelectMany(c => c.Input.SpellingEquivalenceGroups).Count(),
                Is.GreaterThanOrEqualTo(3),
                "#554 names E-flat, B-flat and F-sharp; each needs a spelling group");
        });
    }

    [Test]
    public void EveryPinnedCase_ExplainsWhatItPins()
    {
        var unexplained = Cases
            .Where(c => c.Pins.Issues.Count > 0 && string.IsNullOrWhiteSpace(c.Pins.Rationale))
            .Select(c => c.Id).ToList();

        Assert.That(unexplained, Is.Empty, "pins without rationale: " + string.Join(", ", unexplained));
    }

    [Test]
    public void UnpinnedCases_CarryNoStrayRationale()
    {
        var stray = Cases
            .Where(c => c.Pins.Issues.Count == 0 && c.Pins.Rationale is not null)
            .Select(c => c.Id).ToList();

        Assert.That(stray, Is.Empty, "rationale without pins: " + string.Join(", ", stray));
    }

    // ── Category-specific obligations ────────────────────────────────────────

    [Test]
    public void AlternateTuningCase_UsesANonStandardTuning()
    {
        var alternates = Cases.Where(c => c.Category == "alternate-tuning").ToList();

        Assert.That(alternates, Is.Not.Empty, "#627 requires an alternate-tuning case");
        Assert.Multiple(() =>
        {
            foreach (var c in alternates)
            {
                Assert.That(c.Input.Tuning.Id, Is.Not.EqualTo("standard"), c.Id);
                Assert.That(c.Expected.VoicingSequence.TuningId, Is.EqualTo(c.Input.Tuning.Id),
                    $"{c.Id}: the voicing must be realised in the case's own tuning");
            }
        });
    }

    /// <summary>
    ///     "Ambiguous cases allow alternatives or abstention rather than one
    ///     fabricated answer." A single confident reading must be a failing
    ///     answer for this case, not a passing one.
    /// </summary>
    [Test]
    public void AmbiguousCase_RefusesASingleConfidentAnswer()
    {
        var ambiguous = Cases.Where(c => c.Category == "ambiguous").ToList();

        Assert.That(ambiguous, Is.Not.Empty, "#627 requires an intentionally ambiguous case");
        Assert.Multiple(() =>
        {
            foreach (var c in ambiguous)
            {
                Assert.That(c.Uncertainty.ExpectedBehavior,
                    Is.AnyOf("alternatives", "warn", "abstain"),
                    $"{c.Id}: an ambiguous case cannot expect a confident single answer");

                if (c.Uncertainty.ExpectedBehavior == "alternatives")
                {
                    Assert.That(c.Uncertainty.MinAlternatives, Is.GreaterThanOrEqualTo(2), c.Id);
                    Assert.That(c.Expected.AcceptableTonalCenters, Has.Count.GreaterThanOrEqualTo(2),
                        $"{c.Id}: at least two tonal centres must be acceptable");
                    Assert.That(c.Uncertainty.MaxConfidence, Is.Not.Null.And.LessThan(1.0),
                        $"{c.Id}: an ambiguous reading needs a confidence ceiling");
                }
            }
        });
    }

    [Test]
    public void ConfidentCases_ExpectExactlyOneTonalCentre()
    {
        var overloaded = Cases
            .Where(c => c.Uncertainty.ExpectedBehavior == "confident" &&
                        c.Expected.AcceptableTonalCenters.Count != 1)
            .Select(c => c.Id).ToList();

        Assert.That(overloaded, Is.Empty,
            "a case cannot be both 'confident' and multi-answer: " + string.Join(", ", overloaded));
    }

    [Test]
    public void SpelledOutAccidentalCase_ActuallyWritesAccidentalsOut()
    {
        var cases = Cases.Where(c => c.Category == "spelled-out-accidentals").ToList();

        Assert.That(cases, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (var c in cases)
            {
                Assert.That(c.Input.Chords.Any(s =>
                        s.Contains("flat", StringComparison.OrdinalIgnoreCase) ||
                        s.Contains("sharp", StringComparison.OrdinalIgnoreCase)),
                    Is.True, $"{c.Id}: no chord is actually spelled out");

                Assert.That(c.Input.Chords, Is.Not.EqualTo(c.Input.CanonicalChords),
                    $"{c.Id}: spelled-out input must differ from its shorthand");
            }
        });
    }

    // ── Per-case internal consistency ────────────────────────────────────────

    [TestCaseSource(nameof(CaseSource))]
    public void Case_PerChordArraysAreIndexAligned(ProgressionCase c)
    {
        var n = c.Input.Chords.Count;

        Assert.Multiple(() =>
        {
            Assert.That(c.Input.CanonicalChords, Has.Count.EqualTo(n), "canonical_chords");
            Assert.That(c.Expected.RomanNumerals, Has.Count.EqualTo(n), "roman_numerals");
            Assert.That(c.Expected.Functions, Has.Count.EqualTo(n), "functions");
            Assert.That(c.Expected.ScaleFamilies, Has.Count.EqualTo(n), "scale_families");
            Assert.That(c.Expected.RequiredChordTones, Has.Count.EqualTo(n), "required_chord_tones");
            Assert.That(c.Expected.VoicingSequence.Frames, Has.Count.EqualTo(n), "voicing frames");

            foreach (var alternative in c.Expected.AlternativeRomanNumerals)
                Assert.That(alternative, Has.Count.EqualTo(n), "alternative_roman_numerals");

            for (var i = 0; i < n; i++)
            {
                var chord = c.Input.CanonicalChords[i];
                Assert.That(c.Expected.ScaleFamilies[i].Chord, Is.EqualTo(chord), $"scale_families[{i}]");
                Assert.That(c.Expected.RequiredChordTones[i].Chord, Is.EqualTo(chord), $"required_chord_tones[{i}]");
                Assert.That(c.Expected.VoicingSequence.Frames[i].Chord, Is.EqualTo(chord), $"frames[{i}]");
            }
        });
    }

    [TestCaseSource(nameof(CaseSource))]
    public void Case_ChordTonesMatchTheDeclaredQuality(ProgressionCase c)
    {
        Assert.Multiple(() =>
        {
            foreach (var tones in c.Expected.RequiredChordTones)
            {
                var intervals = CorpusPitchMath.QualityIntervals[tones.Quality];
                var steps = CorpusPitchMath.QualityLetterSteps[tones.Quality];

                Assert.That(CorpusPitchMath.PitchClassOf(tones.Root), Is.EqualTo(tones.RootPitchClass),
                    $"{c.Id} {tones.Chord}: root_pitch_class");

                var expectedPcs = intervals.Select(s => (tones.RootPitchClass + s) % 12).ToList();
                Assert.That(tones.PitchClasses, Is.EqualTo(expectedPcs),
                    $"{c.Id} {tones.Chord}: pitch classes for {tones.Quality}");

                var expectedNotes = intervals
                    .Select((s, i) => CorpusPitchMath.Spell(tones.Root, s, steps[i])).ToList();
                Assert.That(tones.Notes, Is.EqualTo(expectedNotes),
                    $"{c.Id} {tones.Chord}: note spellings for {tones.Quality}");
            }
        });
    }

    /// <summary>
    ///     Fret arithmetic only: every fretted string is re-sounded against the
    ///     case's own tuning and must produce exactly the declared chord tones.
    ///     This is the check that makes the Drop C case fail loudly if anyone
    ///     ever reads it under standard tuning.
    /// </summary>
    [TestCaseSource(nameof(CaseSource))]
    public void Case_VoicingsSoundExactlyTheDeclaredChordTones(ProgressionCase c)
    {
        var openStrings = c.Input.Tuning.OpenStrings;

        Assert.Multiple(() =>
        {
            Assert.That(c.Expected.VoicingSequence.TuningId, Is.EqualTo(c.Input.Tuning.Id),
                $"{c.Id}: voicing tuning must be the case tuning");

            for (var i = 0; i < c.Expected.VoicingSequence.Frames.Count; i++)
            {
                var frame = c.Expected.VoicingSequence.Frames[i];
                var required = c.Expected.RequiredChordTones[i];

                Assert.That(frame.FretsLowToHigh, Has.Count.EqualTo(openStrings.Count),
                    $"{c.Id} {frame.Chord}: one fret entry per string");

                var sounded = CorpusPitchMath.SoundedPitchClasses(openStrings, frame.FretsLowToHigh);

                Assert.That(sounded.Order(), Is.EqualTo(frame.SoundedPitchClasses.Order()),
                    $"{c.Id} {frame.Chord}: sounded_pitch_classes disagrees with the frets");
                Assert.That(sounded.Order(), Is.EqualTo(required.PitchClasses.Distinct().Order()),
                    $"{c.Id} {frame.Chord}: the voicing does not sound the chord");
            }
        });
    }

    [TestCaseSource(nameof(CaseSource))]
    public void Case_VoicingsArePhysicallyPlayable(ProgressionCase c)
    {
        Assert.Multiple(() =>
        {
            foreach (var frame in c.Expected.VoicingSequence.Frames)
            {
                var fretted = frame.FretsLowToHigh.Where(f => f is > 0).Select(f => f!.Value).ToList();
                if (fretted.Count == 0) continue;

                Assert.That(fretted.Max() - fretted.Min(), Is.LessThanOrEqualTo(4),
                    $"{c.Id} {frame.Chord}: fret span exceeds a four-fret hand position");
                Assert.That(fretted.Max(), Is.LessThanOrEqualTo(12),
                    $"{c.Id} {frame.Chord}: above the twelfth fret");
            }
        });
    }

    [TestCaseSource(nameof(CaseSource))]
    public void Case_ForbiddenAnswersCannotAlsoBeAcceptable(ProgressionCase c)
    {
        var overlappingCentres = c.Expected.AcceptableTonalCenters
            .Intersect(c.Forbidden.TonalCenters, StringComparer.OrdinalIgnoreCase).ToList();

        var overlappingFacts = c.Expected.ExplanationFacts
            .Intersect(c.Forbidden.Facts, StringComparer.OrdinalIgnoreCase).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(overlappingCentres, Is.Empty,
                $"{c.Id}: tonal centre both acceptable and forbidden: {string.Join(", ", overlappingCentres)}");
            Assert.That(overlappingFacts, Is.Empty,
                $"{c.Id}: fact both required and forbidden: {string.Join(", ", overlappingFacts)}");
            Assert.That(c.Expected.AcceptableTonalCenters, Does.Contain(c.Expected.TonalCenter),
                $"{c.Id}: the primary tonal centre must be one of the acceptable answers");
        });
    }

    [TestCaseSource(nameof(CaseSource))]
    public void Case_CarriesProvenanceAndRationale(ProgressionCase c)
    {
        Assert.Multiple(() =>
        {
            Assert.That(c.Provenance.AddedIn, Is.EqualTo("GuitarAlchemist/ga#627"));
            Assert.That(c.Provenance.Source, Is.Not.Empty);
            Assert.That(c.Provenance.Rationale, Is.Not.Empty);
        });
    }

    [TestCaseSource(nameof(CaseSource))]
    public void Case_SpellingGroupsOfferARealAlternativeSpelling(ProgressionCase c)
    {
        Assert.Multiple(() =>
        {
            foreach (var group in c.Input.SpellingEquivalenceGroups)
            {
                Assert.That(group.Spellings, Does.Contain(group.Canonical),
                    $"{c.Id}: group '{group.Canonical}' must include its own canonical form");
                Assert.That(group.Spellings.Count(s => s != group.Canonical), Is.GreaterThanOrEqualTo(1),
                    $"{c.Id}: group '{group.Canonical}' has no alternative spelling to test");
            }
        });
    }

    // ── The hand-written case index beside the data ──────────────────────────

    /// <summary>
    ///     The README case table is the only part of this corpus that is written
    ///     by hand next to machine-generated data, which makes it the one place
    ///     the published evidence can drift from the data silently - and it did:
    ///     <c>pc-11</c>'s progression was reworked in the data commit while the
    ///     table kept publishing the removed one. This makes agreement structural
    ///     rather than clerical.
    /// </summary>
    [Test]
    public void ReadmeCaseTable_AgreesWithTheCorpus()
    {
        var rows = ReadmeCaseRows();

        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(r => r.Id), Is.EqualTo(Cases.Select(c => c.Id)),
                "the README case table must list every corpus case, in corpus order");

            foreach (var c in Cases)
            {
                var row = rows.FirstOrDefault(r => r.Id == c.Id);
                if (row is null) continue;

                Assert.That(row.Progression, Is.EqualTo(RenderProgression(c.Input.Chords)),
                    $"{c.Id}: README progression disagrees with input.chords");
                Assert.That(row.Pins, Is.EqualTo(RenderPins(c.Pins.Issues)),
                    $"{c.Id}: README pins column disagrees with pins.issues");
            }
        });

        static string RenderProgression(IReadOnlyList<string> chords) =>
            chords.Any(s => s.Contains(' '))
                ? string.Join(" ", chords.Select(s => $"\"{s}\""))
                : string.Join(" ", chords);

        static string RenderPins(IReadOnlyList<int> issues) =>
            issues.Count == 0 ? "-" : string.Join(", ", issues.Select(i => $"#{i}"));
    }

    /// <summary>Rows of the README's "twelve cases" table, in file order.</summary>
    private static IReadOnlyList<ReadmeRow> ReadmeCaseRows()
    {
        var rows = new List<ReadmeRow>();
        var inCaseTable = false;

        foreach (var line in File.ReadAllLines(ProgressionCorpus.ReadmePath))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inCaseTable = line.Contains("cases", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inCaseTable || !line.StartsWith("| `", StringComparison.Ordinal)) continue;

            var cells = line.Trim('|').Split('|').Select(x => x.Trim()).ToList();
            Assert.That(cells, Has.Count.EqualTo(6),
                $"unexpected README case-table row shape: {line}");

            rows.Add(new ReadmeRow(cells[0].Trim('`'), cells[2], cells[5]));
        }

        Assert.That(rows, Is.Not.Empty,
            $"no case rows found in {ProgressionCorpus.ReadmePath}");

        return rows;
    }

    private sealed record ReadmeRow(string Id, string Progression, string Pins);

    [Test]
    public void Tunings_AreDeclaredConsistentlyAcrossCases()
    {
        var byId = Cases.Select(c => c.Input.Tuning)
            .GroupBy(t => t.Id, StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            foreach (var group in byId)
            {
                var distinct = group.Select(t => string.Join(" ", t.OpenStrings)).Distinct().ToList();
                Assert.That(distinct, Has.Count.EqualTo(1),
                    $"tuning '{group.Key}' is declared with different open strings: " +
                    string.Join(" | ", distinct));
            }
        });
    }
}
