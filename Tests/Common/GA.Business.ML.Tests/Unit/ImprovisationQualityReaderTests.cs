namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;

/// <summary>
///     <see cref="ImprovisationSkill.InferQuality"/> reads the quality suffix through
///     <see cref="GA.Business.ML.Agents.ChordVocabulary.NormalizeQuality"/> first, so every alias of a quality
///     classifies like its canonical spelling. Suffixes outside the shared vocabulary keep the classifier's own
///     heuristic.
/// </summary>
/// <remarks>
///     The old classifier lowercased the suffix and matched prefixes, so <c>CM7</c> read as minor 7, and
///     <c>Cmaj</c> / <c>Cmaj6</c> / <c>Cmaj11</c> read as minor (the <c>StartsWith("m")</c> branch ran before
///     the <c>"maj"</c> one). The golden table was generated from the classifier before the change. Rows marked
///     "was" are the intended fixes; every other row is unchanged.
/// </remarks>
[TestFixture]
public class ImprovisationQualityReaderTests
{
    [TestCase("C", "Major")]
    [TestCase("CΔ", "Major7")]
    [TestCase("CΔ7", "Major7")]
    [TestCase("Cmaj7", "Major7")]
    [TestCase("CMaj7", "Major7")]
    [TestCase("C6", "Major")]
    [TestCase("C69", "Major")]
    [TestCase("C6/9", "Major")]
    [TestCase("Cm", "Minor")]
    [TestCase("Cmin", "Minor")]
    [TestCase("Cminor", "Minor")]
    [TestCase("C-", "Minor")]
    [TestCase("Cm7", "Minor7")]
    [TestCase("Cmin7", "Minor7")]
    [TestCase("C-7", "Minor7")]
    [TestCase("Cm6", "MinorMajor7")]
    [TestCase("Cm9", "Minor7")]
    [TestCase("Cm11", "Minor7")]
    [TestCase("Cm13", "Minor7")]
    [TestCase("Cdim", "Diminished")]
    [TestCase("Cdiminished", "Diminished")]
    [TestCase("C°", "Diminished")]
    [TestCase("Co", "Unknown")]
    [TestCase("Cdim7", "Diminished7")]
    [TestCase("C°7", "Diminished7")]
    [TestCase("Co7", "Diminished7")]
    [TestCase("Cm7b5", "HalfDiminished")]
    [TestCase("Cø", "HalfDiminished")]
    [TestCase("Cø7", "HalfDiminished")]
    [TestCase("Cm7(b5)", "HalfDiminished")]
    [TestCase("Caug", "Augmented")]
    [TestCase("Caugmented", "Augmented")]
    [TestCase("C+", "Augmented")]
    [TestCase("C+7", "Augmented")]
    [TestCase("Caug7", "Augmented")]
    [TestCase("C7#5", "Augmented")]
    [TestCase("C5", "Unknown")]
    [TestCase("Cno3", "Unknown")]
    [TestCase("Cpower", "Unknown")]
    [TestCase("Csus", "Unknown")]
    [TestCase("Csus2", "Unknown")]
    [TestCase("Csus4", "Unknown")]
    [TestCase("C7sus4", "SuspendedDominant")]
    [TestCase("C7sus", "SuspendedDominant")]
    [TestCase("C9sus4", "Dominant7")]
    [TestCase("Cadd9", "Unknown")]
    [TestCase("Cadd11", "Unknown")]
    [TestCase("Cmadd9", "Minor")]
    [TestCase("C7", "Dominant7")]
    [TestCase("C9", "Dominant7")]
    [TestCase("C11", "Dominant7")]
    [TestCase("C13", "Dominant7")]
    [TestCase("C7b5", "AlteredDominant")]
    [TestCase("C7b9", "AlteredDominant")]
    [TestCase("C7#9", "AlteredDominant")]
    [TestCase("C7#11", "Dominant7")]
    [TestCase("C7b13", "Dominant7")]
    [TestCase("C13b9", "AlteredDominant")]
    [TestCase("C13#9", "Dominant7")]
    [TestCase("C9#11", "Dominant7")]
    [TestCase("C7(b9)", "AlteredDominant")]
    [TestCase("C7(#9)", "AlteredDominant")]
    [TestCase("C7alt", "Altered")]
    [TestCase("Calt", "Altered")]
    [TestCase("Caltered", "Altered")]
    [TestCase("Cmaj9", "Major7")]
    [TestCase("Cmaj13", "Major7")]
    [TestCase("Cmaj7#11", "LydianMaj7")]
    [TestCase("Cmaj7+11", "LydianMaj7")]
    [TestCase("Cmaj9#11", "Major7")]
    [TestCase("Cmaj7#5", "Major7")]
    [TestCase("Cmaj7b5", "Major7")]
    [TestCase("Cmmaj7", "MinorMajor7")]
    [TestCase("CmMaj7", "MinorMajor7")]
    [TestCase("Cminmaj7", "MinorMajor7")]
    [TestCase("Cm(maj7)", "MinorMajor7")]
    [TestCase("Cm7#5", "Augmented")]
    [TestCase("Cxyz", "Unknown")]
    [TestCase("CDIM7", "Diminished7")]
    [TestCase("CDOM7", "Dominant7")]
    [TestCase("CMAJ7", "Major7")]
    [TestCase("CMIN7", "Minor7")]
    public void InferQuality_KeepsClassification(string chord, string expectedKind) =>
        Assert.That(ImprovisationSkill.InferQuality(chord).Kind.ToString(), Is.EqualTo(expectedKind));

    [TestCase("CM", "Major")] // was Minor
    [TestCase("CM7", "Major7")] // was Minor7
    [TestCase("CM6", "Major")] // was MinorMajor7
    [TestCase("CM9", "Major7")] // was Minor7
    [TestCase("CM11", "Major7")] // was Minor7
    [TestCase("CM13", "Major7")] // was Minor7
    [TestCase("CmM7", "MinorMajor7")] // was Minor
    [TestCase("CmM9", "MinorMajor7")] // was Minor
    [TestCase("CM7#11", "LydianMaj7")] // was Minor7
    [TestCase("CM9#11", "Major7")] // was Minor7
    [TestCase("CM7b5", "Major7")] // was HalfDiminished
    [TestCase("CM7#5", "Major7")] // was Augmented
    [TestCase("CM7sus4", "Major7")] // was SuspendedDominant
    [TestCase("CM69", "Major")] // was MinorMajor7
    [TestCase("CM6/9", "Major")] // was MinorMajor7
    [TestCase("Cmaj", "Major")] // was Minor
    [TestCase("CMaj", "Major")] // was Minor
    [TestCase("Cmajor", "Major")] // was Minor
    [TestCase("Cmajor 7", "Major7")] // was Minor
    [TestCase("Cma7", "Major7")] // was Minor
    [TestCase("Cmaj6", "Major")] // was Minor
    [TestCase("Cmajor 6", "Major")] // was Minor
    [TestCase("Cminor 7", "Minor7")] // was Minor
    [TestCase("Cmin6", "MinorMajor7")] // was Minor
    [TestCase("Cminor 6", "MinorMajor7")] // was Minor
    [TestCase("Cmin9", "Minor7")] // was Minor
    [TestCase("Cminor 9", "Minor7")] // was Minor
    [TestCase("Cmin11", "Minor7")] // was Minor
    [TestCase("Cmin13", "Minor7")] // was Minor
    [TestCase("Cdiminished 7", "Diminished7")] // was Diminished
    [TestCase("Cmin7b5", "HalfDiminished")] // was AlteredDominant
    [TestCase("Chalf-diminished", "HalfDiminished")] // was Unknown
    [TestCase("Chalf diminished", "HalfDiminished")] // was Unknown
    [TestCase("C7+5", "Augmented")] // was Dominant7
    [TestCase("Cdominant", "Dominant7")] // was Unknown
    [TestCase("Cdom7", "Dominant7")] // was Unknown
    [TestCase("Cdominant 7", "Dominant7")] // was Unknown
    [TestCase("Cdominant 9", "Dominant7")] // was Unknown
    [TestCase("Cmaj11", "Major7")] // was Minor
    public void InferQuality_ClassifiesAliasesLikeTheirCanonicalSpelling(string chord, string expectedKind) =>
        Assert.That(ImprovisationSkill.InferQuality(chord).Kind.ToString(), Is.EqualTo(expectedKind));
}
