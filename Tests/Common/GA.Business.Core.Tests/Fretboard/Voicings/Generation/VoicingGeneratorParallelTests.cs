namespace GA.Business.Core.Tests.Fretboard.Voicings.Generation;

using Domain.Core.Instruments.Primitives;
using Domain.Services.Fretboard.Voicings.Generation;

/// <summary>
///     The parallel path used to emit windows in completion order, so the stream changed from run to run
///     (the learn site's csharp-advanced and ga-lab courses saw OPTIC-K exports of one commit differ), and it
///     was slower than the sequential path because the single consumer built every diagram.
/// </summary>
[TestFixture]
public class VoicingGeneratorParallelTests
{
    [Test]
    public void GenerateAllVoicings_Parallel_EmitsTheSequentialStream()
    {
        var fretboard = Fretboard.Default;
        var sequential = VoicingGenerator.GenerateAllVoicings(fretboard, parallel: false)
            .Select(v => v.Diagram)
            .ToArray();

        for (var run = 0; run < 2; run++)
        {
            var parallel = VoicingGenerator.GenerateAllVoicings(fretboard, parallel: true)
                .Select(v => v.Diagram)
                .ToArray();

            Assert.That(parallel, Is.EqualTo(sequential), $"run {run}");
        }
    }

    [Test]
    public async Task GenerateAllVoicingsAsync_ConsumerStopsEarly_Completes()
    {
        var taken = 0;
        await foreach (var _ in VoicingGenerator.GenerateAllVoicingsAsync(Fretboard.Default)
                           .WithCancellation(CancellationToken.None))
        {
            if (++taken == 100)
            {
                break;
            }
        }

        Assert.That(taken, Is.EqualTo(100));
    }
}
