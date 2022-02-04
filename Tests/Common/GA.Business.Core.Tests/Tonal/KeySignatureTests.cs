using GA.Business.Core.Tonal;
using NUnit.Framework;

namespace GA.Business.Core.Tests.Tonal
{
    public class KeySignatureTests
    {
        [Test]
        public void KeySignatureTest_Sharp()
        {
            Assert.AreEqual("", KeySignature.Sharp(0).SignatureNotes.ToString());
            Assert.AreEqual("F#", KeySignature.Sharp(1).SignatureNotes.ToString());
            Assert.AreEqual("F# C#", KeySignature.Sharp(2).SignatureNotes.ToString());
            Assert.AreEqual("F# C# G#", KeySignature.Sharp(3).SignatureNotes.ToString());
            Assert.AreEqual("F# C# G# D#", KeySignature.Sharp(4).SignatureNotes.ToString());
            Assert.AreEqual("F# C# G# D# A#", KeySignature.Sharp(5).SignatureNotes.ToString());
            Assert.AreEqual("F# C# G# D# A# E#", KeySignature.Sharp(6).SignatureNotes.ToString());
            Assert.AreEqual("F# C# G# D# A# E# B#", KeySignature.Sharp(7).SignatureNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Flat()
        {
            Assert.AreEqual("Bb", KeySignature.Flat(1).SignatureNotes.ToString());
            Assert.AreEqual("Bb Eb", KeySignature.Flat(2).SignatureNotes.ToString());
            Assert.AreEqual("Bb Eb Ab", KeySignature.Flat(3).SignatureNotes.ToString());
            Assert.AreEqual("Bb Eb Ab Db", KeySignature.Flat(4).SignatureNotes.ToString());
            Assert.AreEqual("Bb Eb Ab Db Gb", KeySignature.Flat(5).SignatureNotes.ToString());
            Assert.AreEqual("Bb Eb Ab Db Gb Cb", KeySignature.Flat(6).SignatureNotes.ToString());
            Assert.AreEqual("Bb Eb Ab Db Gb Cb Fb", KeySignature.Flat(7).SignatureNotes.ToString());
        }
    }
}