namespace GenerateNatData.Tests;

using GenerateNatData;
using GenerateNatData.Phase1;

/// <summary>Tests Phase 1 scratch file enumeration reproducibility and format.</summary>
[TestFixture]
public sealed class VoicingEnumeratorTests
{
    private static readonly ConstraintConfig SmallConfig = new()
    {
        MinNotesPlayed = 2,
        MaxFretSpan    = 2,
        FretCount      = 5,
        TuningId       = "EADGBE"
    };

    [Test]
    public async Task EnumerateAsync_DryRun_ReturnsPositiveCount()
    {
        var count = await VoicingEnumerator.EnumerateAsync(SmallConfig, "/dev/null", dryRun: true);
        Assert.That(count, Is.GreaterThan(0));
    }

    [Test]
    public async Task EnumerateAsync_SameConfig_ProducesSameCount()
    {
        var count1 = await VoicingEnumerator.EnumerateAsync(SmallConfig, "/dev/null", dryRun: true);
        var count2 = await VoicingEnumerator.EnumerateAsync(SmallConfig, "/dev/null", dryRun: true);
        Assert.That(count1, Is.EqualTo(count2), "Same constraints must always yield the same voicing count");
    }

    [Test]
    public async Task EnumerateAsync_WrittenFile_ByteIdenticalOnTwoRuns()
    {
        var tmpA = Path.Combine(Path.GetTempPath(), $"gn-test-a-{Guid.NewGuid():N}.bin");
        var tmpB = Path.Combine(Path.GetTempPath(), $"gn-test-b-{Guid.NewGuid():N}.bin");

        try
        {
            await VoicingEnumerator.EnumerateAsync(SmallConfig, tmpA, dryRun: false);
            await VoicingEnumerator.EnumerateAsync(SmallConfig, tmpB, dryRun: false);

            var bytesA = await File.ReadAllBytesAsync(tmpA);
            var bytesB = await File.ReadAllBytesAsync(tmpB);

            Assert.That(bytesA, Is.EqualTo(bytesB), "Scratch file must be byte-identical for the same constraints");
        }
        finally
        {
            if (File.Exists(tmpA)) File.Delete(tmpA);
            if (File.Exists(tmpB)) File.Delete(tmpB);
        }
    }

    [Test]
    public async Task ReadHeader_AfterWrite_MatchesEnumeratedCount()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"gn-test-hdr-{Guid.NewGuid():N}.bin");
        try
        {
            var expectedCount = await VoicingEnumerator.EnumerateAsync(SmallConfig, tmp, dryRun: false);
            var (headerCount, stringCount, _) = VoicingEnumerator.ReadHeader(tmp);

            Assert.Multiple(() =>
            {
                Assert.That(headerCount, Is.EqualTo(expectedCount), "Header count must match returned count");
                Assert.That(stringCount, Is.EqualTo(6), "Standard guitar has 6 strings");
            });
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Test]
    public void ConstraintConfig_GetStableHash_IsDeterministic()
    {
        var h1 = SmallConfig.GetStableHash();
        var h2 = new ConstraintConfig
        {
            MinNotesPlayed = SmallConfig.MinNotesPlayed,
            MaxFretSpan    = SmallConfig.MaxFretSpan,
            FretCount      = SmallConfig.FretCount,
            TuningId       = SmallConfig.TuningId
        }.GetStableHash();

        Assert.That(h1, Is.EqualTo(h2));
    }

    [Test]
    public void ConstraintConfig_DifferentConfigs_HaveDifferentHashes()
    {
        var other = SmallConfig with { MinNotesPlayed = 3 };
        Assert.That(SmallConfig.GetStableHash(), Is.Not.EqualTo(other.GetStableHash()));
    }
}
