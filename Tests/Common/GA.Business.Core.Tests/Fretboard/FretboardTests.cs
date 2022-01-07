using GA.Business.Core.Fretboard.Config;
using NUnit.Framework;

namespace GA.Business.Core.Tests.Fretboard
{
    public class FretboardTests
    {
        [Test]
        public void TuningParserTest()
        {
            if (!FrettedFriendsTuningParser.Instance.TryParse("Baglama - Greek D4 D5 A4 A4 D5 D5", out var parsedTuning)) Assert.Fail();
            if (!FrettedFriendsTuningParser.Instance.TryParse("Bandurria G#3 G#3 C#4 C#4 F#4 F#4 B4 B4 E5 E5 A5 A5", out parsedTuning)) Assert.Fail();
            Assert.Pass();
        }
    }
}
