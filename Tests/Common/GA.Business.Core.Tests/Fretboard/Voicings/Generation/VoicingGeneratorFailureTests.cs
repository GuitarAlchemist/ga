namespace GA.Business.Core.Tests.Fretboard.Voicings.Generation;

using Domain.Core.Instruments.Primitives;
using Domain.Services.Fretboard.Voicings.Generation;

[TestFixture]
public class VoicingGeneratorFailureTests
{
    [Test]
    public void GenerateAllVoicingsAsync_ProducerFailure_PropagatesToConsumer() =>
        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            var fretboard = Fretboard.Default;
            var invalidWindowSize = fretboard.FretCount + 2;

            await VoicingGenerator
                .GenerateAllVoicingsAsync(fretboard, invalidWindowSize)
                .ToListAsync()
                .WaitAsync(TimeSpan.FromSeconds(2));
        });
}
