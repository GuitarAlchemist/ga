using GA.Business.Core.Fretboard.Config;
using NUnit.Framework;

namespace GA.Business.Core.Tests.Fretboard
{
    public class FretboardTests
    {
        [Test]
        public void Test1()
        {
            var a = Core.Fretboard.Fretboard.Default;
            var openPositions = a.OpenPositions;
        }
    }
}
